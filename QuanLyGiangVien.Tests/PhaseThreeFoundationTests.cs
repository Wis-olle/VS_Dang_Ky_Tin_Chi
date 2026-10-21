using Microsoft.Data.SqlClient;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class PhaseThreeSqlFactAttribute : FactAttribute
{
    public PhaseThreeSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHASE3_TEST_CONNECTION")))
            Skip = "Requires isolated phase three SQL fixture.";
    }
}

[Collection("Shared SQL fixture")]
public sealed class PhaseThreeFoundationTests
{
    private static async Task Run(Func<SqlConnection, Task> test, bool transactional = true)
    {
        var cs = Environment.GetEnvironmentVariable("PHASE3_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        if (transactional) await Sql(c, "BEGIN TRAN;");
        try { await test(c); }
        finally { await Sql(c, "IF @@TRANCOUNT>0 ROLLBACK;"); }
    }
    private static async Task Sql(SqlConnection c, string sql)
    { using var q = new SqlCommand(sql, c); await q.ExecuteNonQueryAsync(); }
    private static async Task<int> Count(SqlConnection c, string sql)
    { using var q = new SqlCommand(sql, c); return Convert.ToInt32(await q.ExecuteScalarAsync()); }

    [PhaseThreeSqlFact] public Task TrainingHasNoImplicitOperationalGrant() => Run(async c =>
    {
        await Sql(c, "UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId();");
        var e = await Assert.ThrowsAsync<SqlException>(() => Sql(c, "EXEC dbo.usp_Campus_RequirePermission 'CLASS_OPERATE';"));
        Assert.Equal(52801, e.Number);
    });
    [PhaseThreeSqlFact] public Task EmitRollsBackTogetherWithBusinessWrite() => Run(async c =>
    {
        await Sql(c, "SAVE TRAN test_emit; EXEC dbo.usp_Campus_Emit 'TEST','fixture',N'Kiểm tra',NULL,NULL,NULL,'SV2024002';");
        Assert.Equal(1, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE entity='fixture';"));
        await Sql(c, "ROLLBACK TRAN test_emit;");
        Assert.Equal(0, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE entity='fixture';"));
        Assert.Equal(0, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE entity='fixture';"));
    });
    [PhaseThreeSqlFact] public Task DeliveryRetryDoesNotDuplicateNotification() => Run(async c =>
    {
        await Sql(c, "IF NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='sv2024001') INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv) VALUES('sv2024001','SINH_VIEN','SV2024001');");
        try {
            await Sql(c, "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN; EXEC dbo.usp_Campus_Emit 'TEST','dedup',N'Kiểm tra',NULL,NULL,NULL,'SV2024001'; COMMIT; EXEC dbo.usp_Campus_Deliver; EXEC dbo.usp_Campus_Deliver;");
            Assert.Equal(1, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id JOIN dbo.TAI_KHOAN t ON t.ma_tk=n.account_id WHERE o.entity='dedup' AND t.ten_dang_nhap='sv2024001';"));
        } finally { await CleanupEvent(c, "dedup"); }
    }, false);
    [PhaseThreeSqlFact] public Task StudentCannotGrantPermissions() => Run(async c =>
    {
        await Sql(c, "UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
        var e = await Assert.ThrowsAsync<SqlException>(() => Sql(c, "EXEC dbo.usp_Campus_SetPermission 1,'CLASS_OPERATE',1,N'Không hợp lệ';"));
        Assert.Equal(52801, e.Number);
    });
    [PhaseThreeSqlFact] public Task NotificationsAreScopedToAuthenticatedAccount() => Run(async c =>
    {
        try {
            await Sql(c, "BEGIN TRAN; EXEC dbo.usp_Campus_Emit 'TEST','privacy',N'Riêng SV1',NULL,NULL,NULL,'SV2024001'; COMMIT; EXEC dbo.usp_Campus_Deliver;");
            Assert.True(await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity='privacy';") > 0);
            await Sql(c, "UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
            using var q = new SqlCommand("dbo.usp_Campus_Notifications", c) { CommandType = System.Data.CommandType.StoredProcedure };
            await using var r = await q.ExecuteReaderAsync();
            while (await r.ReadAsync()) Assert.NotEqual("privacy", Convert.ToString(r["entity"]));
        } finally { await Sql(c, "UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();"); await CleanupEvent(c,"privacy"); }
    }, false);

    [PhaseThreeSqlFact] public Task AmbientTransactionNeverRoutesUncommittedEvents() => Run(async c =>
    {
        await Sql(c, "EXEC dbo.usp_Campus_Emit 'TEST','uncommitted',N'Chưa commit'; EXEC dbo.usp_Campus_Deliver;");
        Assert.Equal(0, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity='uncommitted';"));
        Assert.Equal(1, await Count(c, "SELECT @@TRANCOUNT;"));
    });

    [PhaseThreeSqlFact] public Task DeliveryFailureRetainsCommittedEventAndRetriesOnce() => Run(async c =>
    {
        try {
            await Sql(c, "BEGIN TRAN; EXEC dbo.usp_Campus_Emit 'TEST','fault',N'Đã commit'; COMMIT;");
            await Sql(c, "CREATE TRIGGER dbo.tr_Phase3_DeliveryFault ON dbo.CAMPUS_NOTIFICATION AFTER INSERT AS THROW 59998,N'Injected routing failure',1;");
            await Sql(c, "EXEC dbo.usp_Campus_Deliver;");
            Assert.Equal(1, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE entity='fault' AND delivered_utc IS NULL AND attempts>0 AND last_error IS NOT NULL;"));
            Assert.Equal(1, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE entity='fault';"));
            await Sql(c, "DROP TRIGGER dbo.tr_Phase3_DeliveryFault; UPDATE dbo.CAMPUS_OUTBOX SET next_attempt_utc=SYSUTCDATETIME() WHERE entity='fault'; EXEC dbo.usp_Campus_Deliver; EXEC dbo.usp_Campus_Deliver;");
            Assert.Equal(1, await Count(c, "SELECT COUNT(*) FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity='fault' AND n.account_id=dbo.ufn_Campus_AccountId();"));
        } finally { await Sql(c, "DROP TRIGGER IF EXISTS dbo.tr_Phase3_DeliveryFault;"); await CleanupEvent(c,"fault"); }
    }, false);

    private static async Task CleanupEvent(SqlConnection c, string entity)
    {
        using var q = new SqlCommand("DELETE n FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity=@entity; DELETE dbo.CAMPUS_OUTBOX WHERE entity=@entity; DELETE dbo.CAMPUS_AUDIT WHERE entity=@entity;", c);
        q.Parameters.AddWithValue("@entity", entity); await q.ExecuteNonQueryAsync();
    }
}
