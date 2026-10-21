using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class ImportSqlFactAttribute : FactAttribute {
    public ImportSqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IMPORT_TEST_CONNECTION"))) Skip = "Requires isolated import SQL fixture."; }
}
[Collection("Shared SQL fixture")]
public sealed class DataImportIntegrationTests
{
    private static string Cs => Environment.GetEnvironmentVariable("IMPORT_TEST_CONNECTION")!;
    private static DataImportRepository Repo => new(Cs);
    private static string Text(Guid batch, params object[] rows) => JsonSerializer.Serialize(new { batchId = batch, source = "Fixture source, confirmed for tests only", rows });
    private static object Row(string id, string name = "Chương trình kiểm thử") => new { kind = "PROGRAM", id, payload = new { name, major = "CNTT", cohort = 2024, requiredCredits = 120 } };
    private static async Task Sql(string text) { await using var c = new SqlConnection(Cs); await c.OpenAsync(); using var q = new SqlCommand(text, c); await q.ExecuteNonQueryAsync(); }
    private static async Task<int> Count(string text) { await using var c = new SqlConnection(Cs); await c.OpenAsync(); using var q = new SqlCommand(text, c); return Convert.ToInt32(await q.ExecuteScalarAsync()); }
    private static async Task Run(Func<Task> test) {
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(Cs).InitialCatalog);
        await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='IMPORT_DATA') INSERT dbo.CAMPUS_PERMISSION VALUES(dbo.ufn_Campus_AccountId(),'IMPORT_DATA');");
        try { await test(); } finally {
            await Sql("DELETE n FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id JOIN dbo.CAMPUS_AUDIT a ON a.id=o.audit_id WHERE a.event_type='IMPORT'; DELETE dbo.CAMPUS_OUTBOX WHERE audit_id IN(SELECT id FROM dbo.CAMPUS_AUDIT WHERE event_type='IMPORT'); DELETE dbo.CAMPUS_AUDIT WHERE event_type='IMPORT'; DELETE dbo.DK4_IMPORT_BATCH; DELETE dbo.HV_HISTORY WHERE id LIKE 'I4[_]%'; DELETE dbo.HV_DOCUMENT WHERE id LIKE 'I4[_]%';");
        }
    }
    [ImportSqlFact] public Task DryRunResolvesDependenciesButWritesNothing() => Run(async () => {
        var text = Text(Guid.NewGuid(), Row("I4_P1"), new { kind = "GROUP", id = "I4_G1", payload = new { program = "I4_P1", name = "Nhóm", requiredCredits = 10 } });
        using var result = await Repo.PreviewAsync(text); Assert.Equal(2, result.Rows.Count); Assert.All(result.AsEnumerable(), r => Assert.True((bool)r["valid"]));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE id LIKE 'I4[_]%';"));
    });
    [ImportSqlFact] public Task InvalidRowsAreReportedAndWholeCommitRollsBack() => Run(async () => {
        var text = Text(Guid.NewGuid(), Row("I4_P1"), Row("I4_BAD1", ""), Row("I4_BAD2", ""), new { kind = "GROUP", id = "I4_G1", payload = new { program = "I4_P1", name = "Nhóm", requiredCredits = 10 } });
        using var result = await Repo.CommitAsync(text, true); Assert.Equal(4, result.Rows.Count);
        Assert.False((bool)result.Rows[1]["valid"]); Assert.False((bool)result.Rows[2]["valid"]); Assert.True((bool)result.Rows[3]["valid"]);
        Assert.All(result.AsEnumerable(), r => Assert.False((bool)r["committed"]));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE id LIKE 'I4[_]%';"));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM dbo.DK4_IMPORT_BATCH;"));
    });
    [ImportSqlFact] public Task CommitAndReplayProduceOneBatchOneAudit() => Run(async () => {
        var text = Text(Guid.NewGuid(), Row("I4_P1"));
        using var first = await Repo.CommitAsync(text, true); using var replay = await Repo.CommitAsync(text, true);
        Assert.True((bool)first.Rows[0]["committed"]); Assert.True((bool)replay.Rows[0]["committed"]);
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.DK4_IMPORT_BATCH;"));
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE event_type='IMPORT';"));
    });
    [ImportSqlFact] public Task ChangedPayloadCannotReuseBatchId() => Run(async () => {
        var id = Guid.NewGuid(); using var first = await Repo.CommitAsync(Text(id, Row("I4_P1")), true);
        var e = await Assert.ThrowsAsync<PersistenceException>(() => Repo.CommitAsync(Text(id, Row("I4_P2")), true));
        Assert.Equal(PersistenceFailure.DataConflict, e.Kind);
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE id='I4_P2';"));
    });
    [ImportSqlFact] public Task MissingPermissionCannotEvenDryRun() => Run(async () => {
        await Sql("DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='IMPORT_DATA';");
        await Assert.ThrowsAsync<PersistenceException>(() => Repo.PreviewAsync(Text(Guid.NewGuid(), Row("I4_P1"))));
    });
    [ImportSqlFact] public Task MissingSourceConfirmationCannotCommit() => Run(async () => {
        await Assert.ThrowsAsync<ArgumentException>(() => Repo.CommitAsync(Text(Guid.NewGuid(), Row("I4_P1")), false));
        Assert.Equal(0, await Count("SELECT COUNT(*) FROM dbo.DK4_IMPORT_BATCH;"));
    });
    [ImportSqlFact] public Task ConcurrentReplayDoesNotDuplicateRows() => Run(async () => {
        var text = Text(Guid.NewGuid(), Row("I4_P1")); var values = await Task.WhenAll(Repo.CommitAsync(text, true), Repo.CommitAsync(text, true));
        foreach (var value in values) { Assert.True((bool)value.Rows[0]["committed"]); value.Dispose(); }
        Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='I4_P1';"));
    });
}
