using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

/// <summary>Authentication is independent of whether catalogs and class schedules have been entered yet.</summary>
public sealed class SqlIdentityRepository
{
    private readonly string connectionString;
    public SqlIdentityRepository(string connectionString)
    {
        try
        {
            var settings = new SqlConnectionStringBuilder(connectionString) { PersistSecurityInfo = false };
            if (string.IsNullOrWhiteSpace(settings.DataSource) || string.IsNullOrWhiteSpace(settings.InitialCatalog)) throw new ArgumentException();
            settings.ConnectTimeout = Math.Clamp(settings.ConnectTimeout, 1, 15);
            this.connectionString = settings.ConnectionString;
        }
        catch (ArgumentException) { throw new PersistenceException("Cấu hình kết nối SQL không hợp lệ."); }
    }
    public static SqlCommand Command() => new("dbo.usp_LayNguoiDung") { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
    public async Task<DatabaseIdentity> LoadAsync(CancellationToken token = default)
    {
        try
        {
            using var connection = new SqlConnection(connectionString); await AppSqlConnection.OpenAsync(connection, token).ConfigureAwait(false);
            using var command = Command(); command.Connection = connection;
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return Read(reader);
        }
        catch (SqlException error) { throw new PersistenceException(SqlErrorMessages.ForNumber(error.Number)); }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể đọc tài khoản SQL. Kiểm tra kết nối và quyền tài khoản."); }
    }
    public static DatabaseIdentity Read(DbDataReader reader)
    {
        if (!reader.Read()) throw InvalidIdentity();
        string Text(string key) => Convert.ToString(reader[key], CultureInfo.InvariantCulture)?.Trim() ?? "";
        var role = Text("vai_tro"); var student = Text("ma_sv");
        if (role is not ("DAO_TAO" or "QUAN_TRI" or "SINH_VIEN") || role == "SINH_VIEN" && student.Length == 0)
            throw InvalidIdentity();
        var identity = new DatabaseIdentity(Text("ma_tk"), student.Length == 0 ? null : student, role, Text("ho_ten"), null);
        if (identity.AccountId.Length == 0 || identity.DisplayName.Length == 0 || reader.Read()) throw InvalidIdentity();
        return identity;
    }
    private static PersistenceException InvalidIdentity() => new("Không xác định được tài khoản có quyền sử dụng hệ thống.");
}
