using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class CampusSchemaIntegrationTests
{
    [SqlIntegrationFact]
    public async Task MissingPhaseThreeEntrypointCannotPassSchemaHealth()
    {
        var configured = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(configured).InitialCatalog);
        await using var c = new SqlConnection(configured); await c.OpenAsync();
        foreach (var procedure in new[] { "usp_DK3_Cart", "usp_Lifecycle_Calendar", "usp_DK3_ExceptionDecide", "usp_Campus_Permissions", "usp_Seat_Claim", "usp_AP4_Advise", "usp_Planner_SaveCart", "usp_Import_Batch", "usp_Operations_Run" })
        {
            using var drop = new SqlCommand($"BEGIN TRAN; DROP PROCEDURE dbo.{procedure};", c); await drop.ExecuteNonQueryAsync();
            try { var error = await Assert.ThrowsAsync<QuanLyGiangVien.Domain.PersistenceException>(() => CampusSchema.ValidateAsync(c)); Assert.Equal(QuanLyGiangVien.Domain.PersistenceFailure.SchemaMismatch, error.Kind); }
            finally { using var rollback = new SqlCommand("IF @@TRANCOUNT>0 ROLLBACK;", c); await rollback.ExecuteNonQueryAsync(); }
        }
    }

    [SqlIntegrationFact]
    public async Task ExecuteOnlyAppRoleCanValidateSchemaWithoutReadingTableMetadataOrPersonalData()
    {
        var configured = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(configured).InitialCatalog, StringComparison.Ordinal);
        await using var connection = new SqlConnection(configured);
        await connection.OpenAsync();
        var principal = "CampusSchemaTest_" + Guid.NewGuid().ToString("N");
        using var create = new SqlCommand($"CREATE USER [{principal}] WITHOUT LOGIN; ALTER ROLE app_minh ADD MEMBER [{principal}];", connection);
        await create.ExecuteNonQueryAsync();
        try
        {
            using var impersonate = new SqlCommand($"EXECUTE AS USER=N'{principal}';", connection);
            await impersonate.ExecuteNonQueryAsync();
            using var metadata = new SqlCommand("SELECT COL_LENGTH(N'dbo.GIANG_VIEN',N'phien_ban');", connection);
            Assert.IsType<DBNull>(await metadata.ExecuteScalarAsync());
            await CampusSchema.ValidateAsync(connection);
            using var privateTable = new SqlCommand("SELECT TOP(0) ngay_sinh,sdt FROM dbo.GIANG_VIEN;", connection);
            var denied = await Assert.ThrowsAsync<SqlException>(() => privateTable.ExecuteNonQueryAsync());
            Assert.Equal(229, denied.Number);
        }
        finally
        {
            using var cleanup = new SqlCommand($"REVERT; DROP USER [{principal}];", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
