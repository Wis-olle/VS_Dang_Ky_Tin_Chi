using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class ExceptionWorkflowIntegrationTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public SqlConnection Connection { get; }
        private Fixture(SqlConnection connection) => Connection = connection;
        public static async Task<Fixture> Create()
        {
            var cs = Environment.GetEnvironmentVariable("PHASE3_TEST_CONNECTION")!;
            Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
            var c = new SqlConnection(cs); await c.OpenAsync(); var fixture = new Fixture(c);
            await fixture.Sql("BEGIN TRAN;"); return fixture;
        }
        public async Task Sql(string sql) { using var q = new SqlCommand(sql, Connection); await q.ExecuteNonQueryAsync(); }
        public async Task<object?> Scalar(string sql) { using var q = new SqlCommand(sql, Connection); return await q.ExecuteScalarAsync(); }
        public Task Student() => Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
        public Task Reviewer() => Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='EXCEPTION_REVIEW') INSERT dbo.CAMPUS_PERMISSION VALUES(dbo.ufn_Campus_AccountId(),'EXCEPTION_REVIEW');");
        public async Task<long> Submit(string kind = "MAX_CREDITS", string extra = "")
        {
            if (kind == "REQUIREMENT")
                await Sql("INSERT dbo.HV_DOCUMENT(kind,id,payload) VALUES('ASSIGNMENT','EX_TEST_ASSIGN',N'{\"student\":\"SV2024002\",\"program\":\"EX_TEST_PROGRAM\"}'),('REQUIREMENT','EX_TEST_RULE',N'{\"program\":\"EX_TEST_PROGRAM\",\"course\":\"IT101\",\"group\":1,\"requiredCourse\":\"IT102\",\"kind\":\"PRE\",\"minGrade\":5}');");
            await Student(); await Sql($"EXEC dbo.usp_DK3_ExceptionSubmit @kind='{kind}',@term='HK1-2627',@reason=N'Hồ sơ kiểm thử'{extra};");
            return Convert.ToInt64(await Scalar("SELECT MAX(id) FROM dbo.DK3_EXCEPTION_REQUEST WHERE student='SV2024002';"));
        }
        public async Task Approve(long id, int uses = 1, int? credits = 30)
        {
            await Reviewer(); await Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id}); EXEC dbo.usp_DK3_ExceptionDecide {id},@v,'APPROVE',N'Duyệt hồ sơ',@expires_utc='2099-12-31',@uses={uses},@max_credits={(credits.HasValue ? credits.Value.ToString() : "NULL")};");
        }
        public async Task<long> RequirementEnrollment(int uses = 1)
        {
            await Sql("DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002'; DELETE dbo.DANG_KY WHERE ma_sv='SV2024002'; UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY');");
            await Sql("INSERT dbo.HV_DOCUMENT(kind,id,payload,published) VALUES('POLICY','EX_TEST_POLICY',N'{\"name\":\"Fixture\",\"source\":\"Test only\",\"term\":\"HK1-2627\",\"standing\":\"*\",\"minCredits\":0,\"maxCredits\":30,\"passGrade\":5,\"allowImprove\":true,\"maxAttempts\":0,\"enforceCurriculum\":false}',1); UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(day,-2,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),trang_thai='DANG_MO',lifecycle='REGISTRATION' WHERE ma_hk='HK1-2627';");
            var id = await Submit("REQUIREMENT", ",@course='IT101',@rule='GROUP_1'");
            await Approve(id, uses, credits: null); await Student();
            await Sql("EXEC dbo.usp_DK3_Submit '55555555-5555-5555-5555-555555555555','HK1-2627',N'[\"LHP0001\"]';");
            return id;
        }
        public Task Scope(string code = "MAX_CREDITS", int credits = 27, string course = "NULL", string cls = "NULL", string rule = "NULL") => Sql($"CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit); CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20),class_id varchar(20),rule_code varchar(30)); CREATE TABLE #DK3_Removed(class_id varchar(20),course varchar(20)); CREATE TABLE #DK3_Context(total_credits int); INSERT #HV_Issues VALUES('{code}',N'Blocked',1),('SCHEDULE',N'Conflict remains',1); INSERT #DK3_IssueScope VALUES('{code}',{course},{cls},{rule}); INSERT #DK3_Context VALUES({credits});");
        public Task Filter(bool consume = false, string student = "SV2024002", string term = "HK1-2627", string request = "11111111-1111-1111-1111-111111111111") => Sql($"EXEC dbo.usp_DK3_FilterIssues '{student}','{term}',N'[]',{(consume ? 1 : 0)},'{request}';");
        public async ValueTask DisposeAsync() { await Sql("IF @@TRANCOUNT>0 ROLLBACK;"); await Connection.DisposeAsync(); }
    }

    [PhaseThreeSqlFact] public async Task StudentOwnsRequestAndAuditIsTransactional()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit();
        Assert.Equal("SV2024002", await f.Scalar($"SELECT student FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};"));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE entity='EXCEPTION:{id}' AND event_type='EXCEPTION_SUBMITTED';")));
    }
    [PhaseThreeSqlFact] public async Task TrainingCannotApproveWithoutExplicitPermission()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit();
        await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId();");
        var e = await Assert.ThrowsAsync<SqlException>(() => f.Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id}); EXEC dbo.usp_DK3_ExceptionDecide {id},@v,'REJECT',N'Từ chối';"));
        Assert.Equal(52801, e.Number);
    }
    [PhaseThreeSqlFact] public async Task ApprovalMustMatchCurrentVersion()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit(); await f.Reviewer();
        var e = await Assert.ThrowsAsync<SqlException>(() => f.Sql($"EXEC dbo.usp_DK3_ExceptionDecide {id},0x0000000000000000,'APPROVE',N'Duyệt','2099-12-31',1,30;"));
        Assert.Equal(52904, e.Number);
    }
    [PhaseThreeSqlFact] public async Task ExactCreditGrantFiltersOnlyCreditsAndConsumesOnce()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit(); await f.Approve(id, 2); await f.Student(); await f.Scope();
        await f.Filter(); Assert.Equal(2, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
        Assert.Equal(0, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='MAX_CREDITS';")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='SCHEDULE';")));
        await f.Sql("DELETE #HV_Issues WHERE code='SCHEDULE'; INSERT #HV_Issues VALUES('MAX_CREDITS',N'Blocked',1);"); await f.Filter(true);
        await f.Sql("INSERT #HV_Issues VALUES('MAX_CREDITS',N'Blocked',1);"); await f.Filter(true);
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id={id};")));
    }
    [PhaseThreeSqlFact] public async Task GrantCannotApplyToDifferentStudentOrTermOrHigherCredits()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit(); await f.Approve(id, 1, 24); await f.Student(); await f.Scope();
        await f.Filter(); await f.Filter(student: "SV2024001"); await f.Filter(term: "HK2-2627");
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='MAX_CREDITS';")));
    }
    [PhaseThreeSqlFact] public async Task ExpiredRevokedAndExhaustedGrantsCannotFilterIssues()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit(); await f.Approve(id); await f.Student(); await f.Scope();
        await f.Sql($"UPDATE dbo.DK3_EXCEPTION_REQUEST SET expires_utc='2000-01-01' WHERE id={id};"); await f.Filter();
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='MAX_CREDITS';")));
        await f.Sql($"UPDATE dbo.DK3_EXCEPTION_REQUEST SET expires_utc='2099-01-01',status='REVOKED' WHERE id={id};"); await f.Filter();
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='MAX_CREDITS';")));
        await f.Sql($"UPDATE dbo.DK3_EXCEPTION_REQUEST SET status='APPLIED',remaining_uses=0 WHERE id={id};"); await f.Filter();
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='MAX_CREDITS';")));
    }
    [PhaseThreeSqlFact] public async Task RequirementGrantNeverCoversAnotherRuleGroup()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit("REQUIREMENT", ",@course='IT101',@rule='GROUP_1'"); await f.Approve(id, credits: null); await f.Student();
        await f.Scope("REQUIREMENT", course: "'IT101'", rule: "'GROUP_2'"); await f.Filter();
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='REQUIREMENT';")));
        await f.Sql("UPDATE #DK3_IssueScope SET rule_code='GROUP_1';"); await f.Filter();
        Assert.Equal(0, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='REQUIREMENT';")));
    }
    [PhaseThreeSqlFact] public async Task RequirementGrantCannotMigrateToDifferentCurriculumWithSameGroupNumber()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit("REQUIREMENT", ",@course='IT101',@rule='GROUP_1'"); await f.Approve(id, credits: null); await f.Student();
        await f.Sql("UPDATE dbo.HV_DOCUMENT SET payload=JSON_MODIFY(payload,'$.program','EX_OTHER_PROGRAM') WHERE kind='ASSIGNMENT' AND id='EX_TEST_ASSIGN';");
        await f.Scope("REQUIREMENT", course: "'IT101'", rule: "'GROUP_1'"); await f.Filter();
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='REQUIREMENT';")));
    }
    [PhaseThreeSqlFact] public async Task LateGrantMustCoverEveryClassWhoseDeadlineIsClosed()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit("LATE_REGISTER", ",@class='LHP0001'"); await f.Approve(id, credits: null); await f.Student();
        await f.Scope("LATE_REGISTER", cls: "'LHP0001'"); await f.Sql("INSERT #DK3_IssueScope VALUES('LATE_REGISTER',NULL,'LHP0002',NULL);"); await f.Filter();
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='LATE_REGISTER';")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
    }
    [PhaseThreeSqlFact] public async Task RejectAndRevokeHaveAuditAndNeverApplyTwice()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit(); await f.Approve(id); await f.Reviewer();
        await f.Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id}); EXEC dbo.usp_DK3_ExceptionDecide {id},@v,'REVOKE',N'Thu hồi quyết định';");
        Assert.Equal("REVOKED", await f.Scalar($"SELECT status FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};"));
        Assert.Equal(3, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE entity='EXCEPTION:{id}';")));
    }
    [PhaseThreeSqlFact] public async Task LegacySensitiveAcademicWritesRequireNewPermission()
    {
        await using var f = await Fixture.Create(); await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId();");
        var e = await Assert.ThrowsAsync<SqlException>(() => f.Sql("EXEC dbo.usp_HocVu_Save 'HOLD','X',NULL,N'{}';"));
        Assert.Equal(52801, e.Number);
    }
    [PhaseThreeSqlFact] public async Task RequestListDoesNotRevealOtherStudents()
    {
        await using var f = await Fixture.Create(); await f.Submit(); await f.Sql("UPDATE dbo.TAI_KHOAN SET ma_sv='SV2024001' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
        using var command = new SqlCommand("dbo.usp_DK3_ExceptionList", f.Connection) { CommandType = CommandType.StoredProcedure };
        using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) Assert.Equal("SV2024001", reader["student"]);
    }
    [PhaseThreeSqlFact] public async Task OtherBlockingRulesNeverConsumeAnOtherwiseValidGrant()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit(); await f.Approve(id); await f.Student(); await f.Scope();
        await f.Filter(true);
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
        Assert.Equal(0, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id={id};")));
    }
    [PhaseThreeSqlFact] public async Task WithdrawalRequestRequiresStudentsOwnActiveRegistration()
    {
        await using var f = await Fixture.Create(); await f.Student();
        var e = await Assert.ThrowsAsync<SqlException>(() => f.Sql("EXEC dbo.usp_DK3_ExceptionSubmit 'WITHDRAW','HK1-2627',N'Không thuộc đăng ký',@class='LHP0001';"));
        Assert.Equal(52902, e.Number);
    }
    [PhaseThreeSqlFact] public async Task ProfileWriteRequiresDetailedPermissionEvenForTrainingRole()
    {
        await using var f = await Fixture.Create(); await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId();");
        var e = await Assert.ThrowsAsync<SqlException>(() => f.Sql("EXEC dbo.usp_SinhVien_TrangThai 'SV2024002',1;"));
        Assert.Equal(52801, e.Number);
    }
    [PhaseThreeSqlFact] public async Task ApprovedLateRegistrationCommitsAndSpendsOneUseInSameRequest()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit("LATE_REGISTER", ",@class='LHP0001'"); await f.Approve(id, credits: null); await f.Student();
        await f.Sql("UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(day,-3,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,-2,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,-1,DATEADD(hour,7,SYSUTCDATETIME())),trang_thai='DANG_MO',lifecycle='REGISTRATION' WHERE ma_hk='HK1-2627'; EXEC dbo.usp_DK3_Submit '33333333-3333-3333-3333-333333333333','HK1-2627',N'[\"LHP0001\"]';");
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY';")));
        Assert.Equal("APPLIED", await f.Scalar($"SELECT status FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};"));
        await f.Sql("EXEC dbo.usp_DK3_Submit '33333333-3333-3333-3333-333333333333','HK1-2627',N'[\"LHP0001\"]';");
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id={id};")));
    }
    [PhaseThreeSqlFact] public async Task ApprovedWithdrawalRecordsDistinctActionWithoutInventingGradeOrFee()
    {
        await using var f = await Fixture.Create();
        await f.Sql("INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024002','LHP0001','DANG_KY'); UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY') WHERE ma_lhp='LHP0001'; UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(day,-2,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(hour,-1,DATEADD(hour,7,SYSUTCDATETIME())),trang_thai='DANG_MO',lifecycle='REGISTRATION' WHERE ma_hk='HK1-2627';");
        var id = await f.Submit("WITHDRAW", ",@class='LHP0001'"); await f.Approve(id, credits: null); await f.Student();
        await f.Sql("DECLARE @remove nvarchar(max)=(SELECT N'['+CONVERT(nvarchar(30),ma_dk)+N']' FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'); EXEC dbo.usp_DK3_Submit '44444444-4444-4444-4444-444444444444','HK1-2627',N'[]',@remove;");
        Assert.Equal("WITHDRAW", await f.Scalar("SELECT operation FROM dbo.DK3_REQUEST WHERE student='SV2024002' AND request_id='44444444-4444-4444-4444-444444444444';"));
        Assert.Equal(0, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY';")));
        Assert.Equal(0, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
    }
    [PhaseThreeSqlFact] public async Task ExistingEnrollmentRetainsItsConsumedRequirementAuthorizationWhenAddingAnotherCourse()
    {
        await using var f = await Fixture.Create(); var id = await f.RequirementEnrollment();
        await f.Sql("EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[\"LHP0002\"]';");
        Assert.Equal(2, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY';")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id={id};")));
    }
    [PhaseThreeSqlFact] public async Task ExistingEnrollmentDoesNotConsumeAnotherAvailableRequirementUse()
    {
        await using var f = await Fixture.Create(); var id = await f.RequirementEnrollment(2);
        await f.Sql("EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[\"LHP0002\"]';");
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id={id};")));
    }
    [PhaseThreeSqlFact] public async Task DroppingThenReregisteringSameClassNeedsANewRequirementUse()
    {
        await using var f = await Fixture.Create(); await f.RequirementEnrollment();
        await f.Sql("DECLARE @remove nvarchar(max)=(SELECT N'['+CONVERT(nvarchar(30),ma_dk)+N']' FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'); EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[]',@remove;");
        var error = await Assert.ThrowsAsync<SqlException>(() => f.Sql("EXEC dbo.usp_DK3_Submit '77777777-7777-7777-7777-777777777777','HK1-2627',N'[\"LHP0001\"]';"));
        Assert.Equal(52710, error.Number);
    }
    [PhaseThreeSqlFact] public async Task ExistingEnrollmentAuthorizationDoesNotCarryAcrossCurriculumAssignments()
    {
        await using var f = await Fixture.Create(); await f.RequirementEnrollment();
        await f.Sql("UPDATE dbo.HV_DOCUMENT SET payload=JSON_MODIFY(payload,'$.program','EX_OTHER_PROGRAM') WHERE kind='ASSIGNMENT' AND id='EX_TEST_ASSIGN'; INSERT dbo.HV_DOCUMENT(kind,id,payload) VALUES('REQUIREMENT','EX_OTHER_RULE',N'{\"program\":\"EX_OTHER_PROGRAM\",\"course\":\"IT101\",\"group\":1,\"requiredCourse\":\"IT102\",\"kind\":\"PRE\",\"minGrade\":5}');");
        var error = await Assert.ThrowsAsync<SqlException>(() => f.Sql("EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[\"LHP0002\"]';"));
        Assert.Equal(52710, error.Number);
    }
    [PhaseThreeSqlFact] public async Task ExpiryAndRevocationStopNewUseWithoutUndoingExistingRequirementEnrollment()
    {
        await using var f = await Fixture.Create(); var id = await f.RequirementEnrollment(2);
        await f.Sql($"UPDATE dbo.DK3_EXCEPTION_REQUEST SET status='REVOKED',expires_utc='2000-01-01' WHERE id={id}; EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[\"LHP0002\"]';");
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
        Assert.Equal(2, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY';")));
    }
    [PhaseThreeSqlFact] public async Task SwappingTheWaivedCourseToAnotherClassRequiresANewUse()
    {
        await using var f = await Fixture.Create(); await f.RequirementEnrollment();
        await f.Sql("INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,si_so_hien_tai,trang_thai,lifecycle) VALUES('EX_REQ_ALT','IT101','GV0012','HK1-2627',20,0,'DANG_MO','REGISTRATION'); INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt) VALUES('EX_REQ_ALT','C101',3,10,12,'2026-11-03','2026-11-03');");
        var error = await Assert.ThrowsAsync<SqlException>(() => f.Sql("DECLARE @remove nvarchar(max)=(SELECT N'['+CONVERT(nvarchar(30),ma_dk)+N']' FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'); EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[\"EX_REQ_ALT\"]',@remove;"));
        Assert.Equal(52710, error.Number); Assert.Contains("điều kiện", error.Message);
    }
    [PhaseThreeSqlFact] public async Task RetainedPrerequisiteAuthorizationDoesNotWaiveAnotherCorequisiteRule()
    {
        await using var f = await Fixture.Create(); await f.RequirementEnrollment();
        await f.Sql("EXEC dbo.usp_DK3_Submit '66666666-6666-6666-6666-666666666666','HK1-2627',N'[\"LHP0002\"]'; INSERT dbo.HV_DOCUMENT(kind,id,payload) VALUES('REQUIREMENT','EX_TEST_CO',N'{\"program\":\"EX_TEST_PROGRAM\",\"course\":\"IT101\",\"group\":2,\"requiredCourse\":\"IT102\",\"kind\":\"CO\",\"minGrade\":0}');");
        var error = await Assert.ThrowsAsync<SqlException>(() => f.Sql("DECLARE @remove nvarchar(max)=(SELECT N'['+CONVERT(nvarchar(30),ma_dk)+N']' FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0002' AND trang_thai='DANG_KY'); EXEC dbo.usp_DK3_Submit '77777777-7777-7777-7777-777777777777','HK1-2627',N'[]',@remove;"));
        Assert.Equal(52710, error.Number); Assert.Contains("điều kiện", error.Message);
    }
    [PhaseThreeSqlFact] public async Task RetainedOnlyHookNeverAppliesAnUnusedApproval()
    {
        await using var f = await Fixture.Create(); var id = await f.Submit("REQUIREMENT", ",@course='IT101',@rule='GROUP_1'");
        await f.Approve(id, credits: null); await f.Student(); await f.Scope("REQUIREMENT", course: "'IT101'", rule: "'GROUP_1'");
        await f.Sql("EXEC dbo.usp_DK3_FilterIssues 'SV2024002','HK1-2627',N'[]',0,@retained_only=1;");
        Assert.Equal(1, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='REQUIREMENT';")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
    }
    [PhaseThreeSqlFact] public async Task RetainedOnlyHookRecognizesCommittedEnrollmentWithoutSpendingAnotherUse()
    {
        await using var f = await Fixture.Create(); var id = await f.RequirementEnrollment(2);
        await f.Scope("REQUIREMENT", course: "'IT101'", rule: "'GROUP_1'");
        await f.Sql("EXEC dbo.usp_DK3_FilterIssues 'SV2024002','HK1-2627',N'[]',0,@retained_only=1;");
        Assert.Equal(0, Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM #HV_Issues WHERE code='REQUIREMENT';")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id={id};")));
        Assert.Equal(1, Convert.ToInt32(await f.Scalar($"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE exception_id={id};")));
    }
}
