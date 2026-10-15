// Password derivation adapted from QL_DANGKY_TINCHI/Helpers/PasswordHelper.cs.
using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

public enum LoginPortal { Student, Training }

public static class PasswordProof
{
    public static byte[] Create(string password, byte[] salt, byte[] nonce, int iterations)
    {
        if (salt.Length != 16 || nonce.Length != 32 || iterations is < 100_000 or > 2_000_000)
            throw new ArgumentException("Thông số xác thực không hợp lệ.");
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        try { return SHA256.HashData(nonce.Concat(key).ToArray()); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}

public sealed class PasswordLogin(string connectionString)
{
    public static bool Allows(LoginPortal portal, string role) => portal switch {
        LoginPortal.Student => role == "SINH_VIEN",
        LoginPortal.Training => role is "DAO_TAO" or "QUAN_TRI", _ => false
    };

    public async Task SignInAsync(string username, string password, LoginPortal portal, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 128 || string.IsNullOrEmpty(password) || password.Length > 256)
            throw new PersistenceException("Nhập tên đăng nhập và mật khẩu hợp lệ.");
        if (!Enum.IsDefined(portal)) throw new ArgumentException("Cổng đăng nhập không hợp lệ.");
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            using var begin = new SqlCommand("dbo.usp_Auth_Begin", connection) { CommandType = CommandType.StoredProcedure };
            begin.Parameters.Add("@username", SqlDbType.NVarChar, 128).Value = username.Trim();
            Guid challenge; byte[] salt, nonce; int iterations;
            await using (var reader = await begin.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new PersistenceException("Không thể bắt đầu đăng nhập.");
                challenge = reader.GetGuid(0); salt = (byte[])reader[1]; nonce = (byte[])reader[2]; iterations = reader.GetInt32(3);
            }
            // Keep expensive PBKDF2 off the UI thread.
            var proof = await Task.Run(() => PasswordProof.Create(password, salt, nonce, iterations), token).ConfigureAwait(false);
            try
            {
                using var finish = new SqlCommand("dbo.usp_Auth_Complete", connection) { CommandType = CommandType.StoredProcedure };
                finish.Parameters.Add("@challenge", SqlDbType.UniqueIdentifier).Value = challenge;
                finish.Parameters.Add("@proof", SqlDbType.VarBinary, 32).Value = proof;
                finish.Parameters.Add("@portal", SqlDbType.VarChar, 10).Value = portal == LoginPortal.Student ? "STUDENT" : "TRAINING";
                var session = (byte[]?)await finish.ExecuteScalarAsync(token).ConfigureAwait(false)
                    ?? throw new PersistenceException("Đăng nhập chưa hoàn tất.");
                AppSqlConnection.SetSession(connectionString, session);
                CryptographicOperations.ZeroMemory(session);
            }
            finally { CryptographicOperations.ZeroMemory(proof); }
        }
        catch (SqlException error)
        {
            throw new PersistenceException(error.Number switch {
                52101 => "Tên đăng nhập hoặc mật khẩu không chính xác.",
                52102 => "Tài khoản đang bị khóa hoặc hồ sơ không hoạt động. Vui lòng liên hệ phòng đào tạo.",
                52103 => "Tài khoản không thuộc cổng đã chọn. Hãy chọn đúng Sinh viên hoặc Phòng đào tạo.",
                52104 => "Tạm khóa đăng nhập trong 10 phút sau nhiều lần nhập sai.",
                _ => "Không thể đăng nhập. Kiểm tra kết nối và script thiết lập tài khoản."
            });
        }
    }

    public async Task SignOutAsync()
    {
        var session = AppSqlConnection.TakeSession(connectionString);
        if (session == null) return;
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            using var command = new SqlCommand("dbo.usp_Auth_Logout", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = session;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (SqlException) { /* Token is cleared locally even if SQL is unavailable; server expiry remains enforced. */ }
        finally { CryptographicOperations.ZeroMemory(session); }
    }
}
