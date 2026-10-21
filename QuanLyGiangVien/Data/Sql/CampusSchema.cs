using Microsoft.Data.SqlClient;
using System.Data;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

/// <summary>Deployment is explicit. A connection to an old database must not silently use old procedures.</summary>
public static class CampusSchema
{
    public const int RequiredVersion = 4;
    public static async Task ValidateAsync(SqlConnection connection, CancellationToken token = default)
    {
        try
        {
            // Table metadata is deliberately hidden from EXECUTE-only application users.
            // The owner-context procedure returns only a contract number, never table data.
            using var cmd = new SqlCommand("dbo.usp_Campus_KiemTraSchema", connection)
                { CommandType = CommandType.StoredProcedure, CommandTimeout = 10 };
            if (await cmd.ExecuteScalarAsync(token).ConfigureAwait(false) is not int result || result != RequiredVersion) throw Missing();
        }
        catch (SqlException error) when (error.Number is 208 or 207 or 229 or 2812) { throw Missing(); }
    }
    private static PersistenceException Missing() => new(
        "Database chưa có đủ bản cập nhật Campus. Kiểm tra đúng máy chủ/database và chạy scripts/Initialize-Database.ps1 -UpgradeExisting (có sao lưu) trước khi đăng nhập.",
        PersistenceFailure.SchemaMismatch);
}
