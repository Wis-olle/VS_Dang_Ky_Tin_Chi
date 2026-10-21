using System.Data;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

public sealed record StudentRecord(string Code, string Name, DateTime? BirthDate, string? Gender,
    string? Email, string? Phone, string? Address, string Major, string Class, bool Active, string? Version = null);

/// <summary>Student management adapted from the supplied QL_DANGKY_TINCHI source to the main schema.</summary>
public sealed class StudentRepository
{
    private readonly string connectionString;
    public StudentRepository(string connectionString) => this.connectionString = connectionString;

    public static void Validate(StudentRecord student)
    {
        if (!Regex.IsMatch(student.Code, "^[A-Za-z0-9_-]{1,20}$") || string.IsNullOrWhiteSpace(student.Name)
            || student.Name.Length > 100 || student.BirthDate > DateTime.Today || student.BirthDate < new DateTime(1900, 1, 1)
            || student.Gender is not (null or "Nam" or "Nữ" or "Khác")
            || string.IsNullOrWhiteSpace(student.Major) || student.Major.Length > 20
            || string.IsNullOrWhiteSpace(student.Class) || student.Class.Length > 20
            || student.Address?.Length > 250)
            throw new ArgumentException("Kiểm tra mã, họ tên, ngày sinh, ngành và lớp của sinh viên.");
        if (student.Email is { Length: > 0 } email && (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
            throw new ArgumentException("Email không đúng định dạng.");
        if (student.Phone is { Length: > 0 } phone && !Regex.IsMatch(phone, "^[+0-9 ()-]{6,20}$"))
            throw new ArgumentException("Số điện thoại không đúng định dạng.");
    }

    public static SqlCommand SaveCommand(StudentRecord student, bool isNew)
    {
        Validate(student);
        var command = Command("Luu");
        command.Parameters.AddRange([
            P("@la_moi", SqlDbType.Bit, isNew), P("@ma_sv", SqlDbType.VarChar, student.Code, 20),
            P("@ho_ten", SqlDbType.NVarChar, student.Name.Trim(), 100), P("@ngay_sinh", SqlDbType.Date, student.BirthDate?.Date),
            P("@gioi_tinh", SqlDbType.NVarChar, student.Gender, 10), P("@email", SqlDbType.VarChar, student.Email, 254),
            P("@sdt", SqlDbType.VarChar, student.Phone, 20), P("@dia_chi", SqlDbType.NVarChar, student.Address, 250),
            P("@ma_nganh", SqlDbType.VarChar, student.Major, 20), P("@ma_lop", SqlDbType.VarChar, student.Class, 20),
            P("@trang_thai", SqlDbType.Bit, student.Active), VersionParameter(student.Version, isNew)]);
        return command;
    }

    private static SqlParameter VersionParameter(string? version, bool isNew = false)
    {
        if (version is null && !isNew || version is not null && (version.Length != 64 || !version.All(Uri.IsHexDigit)))
            throw new ArgumentException("Thiếu phiên bản hồ sơ hợp lệ. Hãy tải lại sinh viên trước khi lưu hoặc đổi trạng thái.");
        return P("@phien_ban", SqlDbType.Binary, version is null ? null : Convert.FromHexString(version), 32);
    }

    public static SqlCommand StatusCommand(StudentRecord student, bool active)
    {
        var command = Command("TrangThai");
        command.Parameters.AddRange([P("@ma_sv", SqlDbType.VarChar, student.Code, 20),
            P("@trang_thai", SqlDbType.Bit, active), VersionParameter(student.Version)]);
        return command;
    }

    private static SqlParameter P(string name, SqlDbType type, object? value, int size = 0) => new(name, type, size) { Value = value ?? DBNull.Value };
    private static SqlCommand Command(string operation) => new("dbo.usp_SinhVien_" + operation) { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
    private async Task<DataTable> QueryAsync(string operation, params SqlParameter[] parameters)
    {
        using var command = Command(operation); command.Parameters.AddRange(parameters);
        return await RunAsync(command, true);
    }
    private async Task<DataTable> RunAsync(SqlCommand command, bool read)
    {
        try
        {
            using var connection = new SqlConnection(connectionString); command.Connection = connection;
            await AppSqlConnection.OpenAsync(connection).ConfigureAwait(false);
            var table = new DataTable();
            if (read) { using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false); table.Load(reader); }
            else await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            return table;
        }
        catch (SqlException error) { throw new PersistenceException(error.Number switch {
            52300 => "Thông tin sinh viên không hợp lệ. Kiểm tra các trường và ngày sinh.",
            52301 => "Mã sinh viên đã tồn tại.", 52302 => "Sinh viên không còn tồn tại. Hãy tải lại danh sách.",
            52303 => "Lớp phải thuộc ngành đã chọn; lớp, ngành và khoa cần đang hoạt động.",
            52304 => "Hồ sơ sinh viên đã thay đổi ở phiên khác. Bản nháp vẫn được giữ; hãy ghi lại nội dung cần giữ và bấm Tải lại trước khi sửa tiếp.",
            2812 => "Cần chạy migration 09_Students.sql để sử dụng quản lý sinh viên.",
            _ => SqlErrorMessages.ForNumber(error.Number) },
            error.Number == 52304 ? PersistenceFailure.DataConflict : PersistenceFailure.General); }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể truy cập danh sách sinh viên. Hãy thử tải lại."); }
    }
    public Task<DataTable> ListAsync(string keyword, bool includeInactive) => QueryAsync("DanhSach", P("@tu_khoa", SqlDbType.NVarChar, keyword.Trim(), 200), P("@gom_ngung", SqlDbType.Bit, includeInactive));
    public Task<DataTable> ChoicesAsync() => QueryAsync("DanhMuc");
    public async Task SaveAsync(StudentRecord student, bool isNew) { using var command = SaveCommand(student, isNew); await RunAsync(command, false); }
    public async Task SetStatusAsync(StudentRecord student, bool active)
    {
        using var command = StatusCommand(student, active);
        await RunAsync(command, false);
    }
    public static StudentRecord FromRow(DataRow row)
    {
        string? Optional(string field) => row.IsNull(field) ? null : Convert.ToString(row[field]);
        return new(Convert.ToString(row["ma_sv"])!, Convert.ToString(row["ho_ten"])!,
            row.IsNull("ngay_sinh") ? null : Convert.ToDateTime(row["ngay_sinh"]), Optional("gioi_tinh"),
            Optional("email"), Optional("sdt"), Optional("dia_chi"), Convert.ToString(row["ma_nganh"])!,
            Convert.ToString(row["ma_lop"])!, Convert.ToBoolean(row["trang_thai"]),
            row.Table.Columns.Contains("phien_ban") && !row.IsNull("phien_ban") ? Convert.ToHexString((byte[])row["phien_ban"]) : null);
    }
}
