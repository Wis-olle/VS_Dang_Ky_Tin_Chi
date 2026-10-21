using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class OperationsIntegrationTests
{
    private static string Cs => Environment.GetEnvironmentVariable("IMPORT_TEST_CONNECTION")!;
    private static async Task Sql(string text) { await using var c = new SqlConnection(Cs); await c.OpenAsync(); using var q = new SqlCommand(text, c); await q.ExecuteNonQueryAsync(); }
    private static async Task Run(Func<Task> test) {
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(Cs).InitialCatalog);
        await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission IN('OPERATIONS_READ','WAITLIST_OPERATE');");
        try { await test(); } finally { await Sql("IF OBJECT_ID('dbo.OP4_WORKER_RUN') IS NOT NULL DELETE dbo.OP4_WORKER_RUN;"); }
    }
    private static Task Grant(string name) => Sql($"INSERT dbo.CAMPUS_PERMISSION VALUES(dbo.ufn_Campus_AccountId(),'{name}');");
    [ImportSqlFact] public Task DiagnosticReadNeedsExplicitPermission() => Run(async () => {
        await Assert.ThrowsAsync<PersistenceException>(() => new OperationsRepository(Cs).SnapshotAsync("HK1-2627"));
    });
    [ImportSqlFact] public Task SnapshotSeparatesDemandRisksHealthAndWorkerHistory() => Run(async () => {
        await Grant("OPERATIONS_READ"); using var data = await new OperationsRepository(Cs).SnapshotAsync("HK1-2627");
        Assert.Equal(6, data.Tables.Count); Assert.True(data.Tables.Contains("Health"));
        Assert.Contains("available", data.Tables["Demand"]!.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        Assert.All(data.Tables["Demand"]!.AsEnumerable(), r => Assert.True(Convert.ToInt32(r["available"]) >= 0));
    });
    [ImportSqlFact] public Task RunningWorkerIsNotImpliedByDiagnosticPermission() => Run(async () => {
        await Grant("OPERATIONS_READ"); await Assert.ThrowsAsync<PersistenceException>(() => new OperationsRepository(Cs).RunOnceAsync("HK1-2627"));
    });
    [ImportSqlFact] public Task AuthenticatedWorkerRecordsServerTimingAndDoesNotInventOffers() => Run(async () => {
        await Grant("WAITLIST_OPERATE"); await Grant("OPERATIONS_READ");
        using var result = await new OperationsRepository(Cs).RunOnceAsync("HK1-2627");
        Assert.Equal("SUCCEEDED", result.Tables["Worker"]!.Rows[0]["status"]);
        using var data = await new OperationsRepository(Cs).SnapshotAsync("HK1-2627"); var row = data.Tables["Worker"]!.AsEnumerable().First();
        Assert.Equal("SUCCEEDED", row["status"]); Assert.True(Convert.ToInt64(row["duration_ms"]) >= 0); Assert.Equal(DBNull.Value, row["error_code"]);
    });
    [ImportSqlFact] public Task WorkerRejectsUnknownSemesterBeforeWritingDiagnosticRecord() => Run(async () => {
        await Grant("WAITLIST_OPERATE"); var error = await Assert.ThrowsAsync<PersistenceException>(() => new OperationsRepository(Cs).RunOnceAsync("MISSING"));
        Assert.Contains("Học kỳ", error.Message);
    });
}
