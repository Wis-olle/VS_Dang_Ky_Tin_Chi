using Microsoft.Data.SqlClient;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class ExceptionWorkflowConcurrencyTests
{
    [PhaseThreeSqlFact]
    public async Task TwoAtomicRequestsCannotSpendTheSameLastExceptionUse()
    {
        var cs = Environment.GetEnvironmentVariable("PHASE3_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
        await using var setup = new SqlConnection(cs); await setup.OpenAsync(); var id = 0L;
        try
        {
            using (var insert = new SqlCommand("INSERT dbo.DK3_EXCEPTION_REQUEST(student,term,kind,reason,submitted_by,status,reviewed_by,reviewed_utc,decision_reason,expires_utc,granted_uses,remaining_uses,max_credits) VALUES('SV2024002','HK1-2627','MAX_CREDITS',N'Consumption race',dbo.ufn_Campus_AccountId(),'APPROVED',dbo.ufn_Campus_AccountId(),SYSUTCDATETIME(),N'Fixture decision','2099-01-01',1,1,30); SELECT CONVERT(bigint,SCOPE_IDENTITY());", setup))
                id = Convert.ToInt64(await insert.ExecuteScalarAsync());
            async Task<int> Apply()
            {
                await using var c = new SqlConnection(cs); await c.OpenAsync();
                using var q = new SqlCommand("CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit); CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20),class_id varchar(20),rule_code varchar(30)); CREATE TABLE #DK3_Context(total_credits int); INSERT #HV_Issues VALUES('MAX_CREDITS',N'Blocked',1); INSERT #DK3_IssueScope VALUES('MAX_CREDITS',NULL,NULL,NULL); INSERT #DK3_Context VALUES(27); BEGIN TRAN; EXEC dbo.usp_HocVu_Lock; EXEC dbo.usp_DK3_FilterIssues 'SV2024002','HK1-2627',N'[]',1,@request; DECLARE @blocked int=(SELECT COUNT(*) FROM #HV_Issues WHERE blocking=1); COMMIT; SELECT @blocked;", c);
                q.Parameters.AddWithValue("@request", Guid.NewGuid()); return Convert.ToInt32(await q.ExecuteScalarAsync());
            }
            var outcomes = await Task.WhenAll(Apply(), Apply());
            Assert.Equal(1, outcomes.Count(x => x == 0)); Assert.Equal(1, outcomes.Count(x => x == 1));
            using var check = new SqlCommand("SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id=@id;", setup);
            check.Parameters.AddWithValue("@id", id); Assert.Equal(1, Convert.ToInt32(await check.ExecuteScalarAsync()));
        }
        finally
        {
            if (id != 0)
            {
                using var cleanup = new SqlCommand("DELETE n FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity=@entity; DELETE dbo.CAMPUS_OUTBOX WHERE entity=@entity; DELETE dbo.CAMPUS_AUDIT WHERE entity=@entity; DELETE dbo.DK3_EXCEPTION_USE WHERE exception_id=@id; DELETE dbo.DK3_EXCEPTION_REQUEST WHERE id=@id;", setup);
                cleanup.Parameters.AddWithValue("@entity", "EXCEPTION:" + id); cleanup.Parameters.AddWithValue("@id", id); await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    [PhaseThreeSqlFact]
    public async Task TwoDecisionsOnSameVersionProduceOnlyOneApproval()
    {
        var cs = Environment.GetEnvironmentVariable("PHASE3_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
        await using var setup = new SqlConnection(cs); await setup.OpenAsync();
        var id = 0L; var account = 0; string? role = null, student = null; var hadPermission = false;
        async Task Sql(string sql) { using var q = new SqlCommand(sql, setup); await q.ExecuteNonQueryAsync(); }
        try
        {
            using (var accountQuery = new SqlCommand("SELECT ma_tk,vai_tro,ma_sv FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN();", setup))
            using (var reader = await accountQuery.ExecuteReaderAsync())
            { Assert.True(await reader.ReadAsync()); account = reader.GetInt32(0); role = reader.GetString(1); student = reader.IsDBNull(2) ? null : reader.GetString(2); }
            using (var permissionQuery = new SqlCommand("SELECT COUNT(*) FROM dbo.CAMPUS_PERMISSION WHERE account_id=@a AND permission='EXCEPTION_REVIEW';", setup))
            { permissionQuery.Parameters.AddWithValue("@a", account); hadPermission = Convert.ToInt32(await permissionQuery.ExecuteScalarAsync()) > 0; }
            await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='EXCEPTION_REVIEW') INSERT dbo.CAMPUS_PERMISSION VALUES(dbo.ufn_Campus_AccountId(),'EXCEPTION_REVIEW');");
            using (var insert = new SqlCommand("INSERT dbo.DK3_EXCEPTION_REQUEST(student,term,kind,reason,submitted_by) VALUES('SV2024002','HK1-2627','MAX_CREDITS',N'Concurrency fixture',@a); SELECT CONVERT(bigint,SCOPE_IDENTITY());", setup))
            { insert.Parameters.AddWithValue("@a", account); id = Convert.ToInt64(await insert.ExecuteScalarAsync()); }
            byte[] version;
            using (var query = new SqlCommand("SELECT version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id", setup))
            { query.Parameters.AddWithValue("@id", id); version = (byte[])(await query.ExecuteScalarAsync())!; }
            async Task<int> Decide()
            {
                await using var c = new SqlConnection(cs); await c.OpenAsync();
                using var q = new SqlCommand("dbo.usp_DK3_ExceptionDecide", c) { CommandType = System.Data.CommandType.StoredProcedure };
                q.Parameters.AddWithValue("@id", id); q.Parameters.AddWithValue("@version", version); q.Parameters.AddWithValue("@decision", "APPROVE");
                q.Parameters.AddWithValue("@reason", "Concurrency approval"); q.Parameters.AddWithValue("@expires_utc", new DateTime(2099, 1, 1));
                q.Parameters.AddWithValue("@uses", 1); q.Parameters.AddWithValue("@max_credits", 30);
                try { await q.ExecuteNonQueryAsync(); return 0; } catch (SqlException e) { return e.Number; }
            }
            var outcomes = await Task.WhenAll(Decide(), Decide());
            Assert.Equal(1, outcomes.Count(x => x == 0)); Assert.Equal(1, outcomes.Count(x => x == 52904));
            using var count = new SqlCommand("SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE entity=@entity AND event_type='EXCEPTION_APPROVE';", setup);
            count.Parameters.AddWithValue("@entity", "EXCEPTION:" + id); Assert.Equal(1, Convert.ToInt32(await count.ExecuteScalarAsync()));
        }
        finally
        {
            if (id != 0)
            {
                using var cleanup = new SqlCommand("DELETE n FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity=@entity; DELETE dbo.CAMPUS_OUTBOX WHERE entity=@entity; DELETE dbo.CAMPUS_AUDIT WHERE entity=@entity; DELETE dbo.DK3_EXCEPTION_USE WHERE exception_id=@id; DELETE dbo.DK3_EXCEPTION_REQUEST WHERE id=@id;", setup);
                cleanup.Parameters.AddWithValue("@entity", "EXCEPTION:" + id); cleanup.Parameters.AddWithValue("@id", id); await cleanup.ExecuteNonQueryAsync();
            }
            if (role is not null)
            {
                using var restore = new SqlCommand("UPDATE dbo.TAI_KHOAN SET vai_tro=@role,ma_sv=@student WHERE ma_tk=@a; IF @had=0 DELETE dbo.CAMPUS_PERMISSION WHERE account_id=@a AND permission='EXCEPTION_REVIEW';", setup);
                restore.Parameters.AddWithValue("@role", role); restore.Parameters.AddWithValue("@student", (object?)student ?? DBNull.Value); restore.Parameters.AddWithValue("@a", account); restore.Parameters.AddWithValue("@had", hadPermission); await restore.ExecuteNonQueryAsync();
            }
        }
    }
}
