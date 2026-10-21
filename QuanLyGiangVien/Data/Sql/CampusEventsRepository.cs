using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

public sealed class CampusEventsRepository(string connectionString)
{
    public Task<DataTable> NotificationsAsync(long? readId = null) => Table("usp_Campus_Notifications",
        new SqlParameter("@read_id", SqlDbType.BigInt) { Value = (object?)readId ?? DBNull.Value });
    public Task<DataTable> OperationsAsync() => Table("usp_Campus_Operations");
    public Task<DataTable> PermissionsAsync() => Table("usp_Campus_Permissions");
    public Task<DataTable> PermissionAssignmentsAsync() => Table("usp_Campus_Permissions", new SqlParameter("@assignments", SqlDbType.Bit) { Value = true });
    public Task<DataTable> SetPermissionAsync(int account, string permission, bool enabled, string reason) => Table("usp_Campus_SetPermission",
        new("@account_id", SqlDbType.Int) { Value = account }, new("@permission", SqlDbType.VarChar, 40) { Value = permission },
        new("@enabled", SqlDbType.Bit) { Value = enabled }, new("@reason", SqlDbType.NVarChar, 500) { Value = reason });

    private async Task<DataTable> Table(string procedure, params SqlParameter[] parameters)
    {
        try
        {
            await using var c = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(c).ConfigureAwait(false);
            using var q = new SqlCommand("dbo." + procedure, c) { CommandType = CommandType.StoredProcedure };
            q.Parameters.AddRange(parameters);
            await using var r = await q.ExecuteReaderAsync().ConfigureAwait(false);
            var result = new DataTable(); result.Load(r); return result;
        }
        catch (SqlException e) { throw new PersistenceException(e.Number is >= 52800 and <= 52999 ? e.Message : SqlErrorMessages.ForNumber(e.Number)); }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể tải thông báo. Hãy kiểm tra kết nối và thử lại."); }
    }
}
