using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

public sealed record AccountDraft(int? Id, string Username, string Role, string? StudentId, bool Active, string? Version, string Reason);

public static class AccountValidation
{
    public static void Password(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 12 or > 256)
            throw new ArgumentException("Mật khẩu phải có từ 12 đến 256 ký tự.");
    }

    public static AccountDraft Validate(AccountDraft value)
    {
        var username = value.Username.Trim(); var reason = value.Reason.Trim();
        if (username.Length is < 1 or > 128 || username.Any(char.IsControl) || reason.Length is < 1 or > 500
            || value.Role is not ("SINH_VIEN" or "DAO_TAO" or "QUAN_TRI") || value.Id is <= 0)
            throw new ArgumentException("Kiểm tra tên đăng nhập, vai trò và lý do (tối đa 500 ký tự).");
        if (value.Id != null && (value.Version?.Length != 18 || !value.Version.StartsWith("0x", StringComparison.Ordinal)))
            throw new ArgumentException("Thiếu phiên bản tài khoản. Hãy tải lại trước khi sửa.");
        var student = value.Role == "SINH_VIEN" ? Domain.Registration.RegistrationValidation.RequiredCode(value.StudentId) : null;
        return value with { Username = username, StudentId = student, Reason = reason };
    }
}

public sealed class AccountRepository(string connectionString)
{
    public Task<DataTable> ListAsync() => ExecuteAsync(Command("DanhSach"));
    public Task<DataTable> AuditAsync() => ExecuteAsync(Command("NhatKy"));

    public async Task SaveAsync(AccountDraft value, string? newPassword = null)
    {
        var draft = AccountValidation.Validate(value);
        using var command = Command("Luu");
        Add(command, "@ma_tk", SqlDbType.Int, draft.Id);
        Add(command, "@ten_dang_nhap", SqlDbType.NVarChar, draft.Username, 128);
        Add(command, "@vai_tro", SqlDbType.VarChar, draft.Role, 20);
        Add(command, "@ma_sv", SqlDbType.VarChar, draft.StudentId, 20);
        Add(command, "@trang_thai", SqlDbType.Bit, draft.Active);
        Add(command, "@phien_ban", SqlDbType.VarChar, draft.Version, 18);
        Add(command, "@ly_do", SqlDbType.NVarChar, draft.Reason, 500);
        if (draft.Id == null)
        {
            AccountValidation.Password(newPassword ?? "");
            await WithCredentialAsync(newPassword!, command).ConfigureAwait(false);
        }
        else using (await ExecuteAsync(command).ConfigureAwait(false)) { }
    }

    public async Task ResetAsync(int id, string version, string password, string reason)
    {
        if (id <= 0 || version.Length != 18 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ArgumentException("Chọn tài khoản, tải lại phiên bản và nhập lý do đặt lại mật khẩu.");
        AccountValidation.Password(password);
        using var command = Command("Reset");
        Add(command, "@ma_tk", SqlDbType.Int, id); Add(command, "@phien_ban", SqlDbType.VarChar, version, 18);
        Add(command, "@ly_do", SqlDbType.NVarChar, reason.Trim(), 500);
        await WithCredentialAsync(password, command).ConfigureAwait(false);
    }

    public async Task ChangeOwnPasswordAsync(string username, string currentPassword, string newPassword)
    {
        AccountValidation.Password(newPassword);
        if (string.IsNullOrWhiteSpace(username) || username.Length > 128 || string.IsNullOrEmpty(currentPassword) || currentPassword.Length > 256)
            throw new ArgumentException("Nhập tên đăng nhập và mật khẩu hiện tại.");
        using var begin = new SqlCommand("dbo.usp_Auth_Begin") { CommandType = CommandType.StoredProcedure };
        Add(begin, "@username", SqlDbType.NVarChar, username.Trim(), 128);
        using var challenge = await ExecuteAsync(begin).ConfigureAwait(false);
        var row = challenge.Rows[0];
        var proof = await Task.Run(() => PasswordProof.Create(currentPassword, (byte[])row["salt"], (byte[])row["nonce"], (int)row["iterations"])).ConfigureAwait(false);
        try
        {
            using var command = Command("DoiMatKhau");
            Add(command, "@challenge", SqlDbType.UniqueIdentifier, row["challenge"]);
            Add(command, "@proof", SqlDbType.VarBinary, proof, 32);
            await WithCredentialAsync(newPassword, command).ConfigureAwait(false);
            // All server sessions were revoked. Fail closed locally and let shell request reauthentication.
            AppSqlConnection.BlockSession(connectionString);
        }
        finally { CryptographicOperations.ZeroMemory(proof); }
    }

    private async Task WithCredentialAsync(string password, SqlCommand command)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var verifier = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32)).ConfigureAwait(false);
        try
        {
            Add(command, "@salt", SqlDbType.VarBinary, salt, 16); Add(command, "@verifier", SqlDbType.VarBinary, verifier, 32);
            using (await ExecuteAsync(command).ConfigureAwait(false)) { }
        }
        finally { CryptographicOperations.ZeroMemory(verifier); CryptographicOperations.ZeroMemory(salt); }
    }

    private async Task<DataTable> ExecuteAsync(SqlCommand command)
    {
        using (command)
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(connection).ConfigureAwait(false);
            command.Connection = connection;
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };
            if (reader.FieldCount > 0) table.Load(reader); return table;
        }
        catch (SqlException error)
        {
            throw new PersistenceException(error.Number switch {
                52701 => "Bạn không có quyền quản lý tài khoản này.",
                52702 => "Thông tin chưa hợp lệ. Kiểm tra hồ sơ sinh viên, mật khẩu và lý do.",
                52703 => "Tên đăng nhập đã tồn tại.",
                52704 => "Tài khoản đã thay đổi. Bản nháp được giữ; tải lại trước khi tiếp tục.",
                52705 => "Không thể sửa danh tính, tài khoản Windows, chính mình hoặc khóa quản trị cuối cùng.",
                52706 => "Hệ thống bận. Hãy thử lại.",
                52707 => "Mật khẩu hiện tại không đúng hoặc xác thực đã hết hạn.",
                52404 => "Tạm khóa xác thực trong 10 phút sau nhiều lần nhập sai.",
                _ => SqlErrorMessages.ForNumber(error.Number)
            }, error.Number == 52704 ? PersistenceFailure.DataConflict : PersistenceFailure.General);
        }
    }

    private static SqlCommand Command(string suffix) => new("dbo.usp_TaiKhoan_" + suffix) { CommandType = CommandType.StoredProcedure };
    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0) =>
        command.Parameters.Add(new SqlParameter(name, type, size) { Value = value ?? DBNull.Value });
}
