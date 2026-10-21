using Microsoft.Data.SqlClient;
using System.Data;
using Xunit;
namespace QuanLyGiangVien.Tests;

public sealed class SeatSqlFactAttribute : FactAttribute
{
    public SeatSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SEAT_TEST_CONNECTION")))Skip="Requires isolated seat allocation fixture.";}
}
[Collection("Shared SQL fixture")]
public sealed class SeatAllocationIntegrationTests
{
    private sealed class Fixture:IAsyncDisposable
    {
        public SqlConnection Connection{get;}
        private Fixture(SqlConnection c)=>Connection=c;
        public static async Task<Fixture> Create()
        {
            var cs=Environment.GetEnvironmentVariable("SEAT_TEST_CONNECTION")!;Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(cs).InitialCatalog);
            var c=new SqlConnection(cs);await c.OpenAsync();var f=new Fixture(c);
            try
            {
                await f.Sql("BEGIN TRAN; DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002' OR d.ma_lhp='LHP0002'; DELETE dbo.DANG_KY WHERE ma_sv='SV2024002' OR ma_lhp='LHP0002'; UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY'); UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(day,-2,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),trang_thai='DANG_MO',lifecycle='REGISTRATION' WHERE ma_hk='HK1-2627';");
                return f;
            }
            catch {await f.DisposeAsync();throw;}
        }
        public async Task Sql(string sql){using var q=new SqlCommand(sql,Connection){CommandTimeout=60};await q.ExecuteNonQueryAsync();}
        public async Task<object?> Scalar(string sql){using var q=new SqlCommand(sql,Connection);return await q.ExecuteScalarAsync();}
        public Task Staff()=>Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); INSERT dbo.CAMPUS_PERMISSION SELECT dbo.ufn_Campus_AccountId(),p FROM (VALUES('SEAT_MANAGE'),('WAITLIST_OPERATE'))v(p) WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION x WHERE x.account_id=dbo.ufn_Campus_AccountId() AND x.permission=v.p);");
        public Task Student()=>Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
        public async Task<long> Offer()
        {
            await Student();await Sql("EXEC dbo.usp_Seat_Join 'HK1-2627',N'[\"LHP0002\"]';");
            var id=Convert.ToInt64(await Scalar("SELECT MAX(id) FROM dbo.SEAT_WAITLIST WHERE student='SV2024002';"));
            await Staff();await Sql("EXEC dbo.usp_Seat_Process 'HK1-2627';");return id;
        }
        public async ValueTask DisposeAsync(){try{await Sql("IF @@TRANCOUNT>0 ROLLBACK;");}finally{await Connection.DisposeAsync();}}
    }
    [SeatSqlFact] public async Task SchemaAndNoConfigurationKeepExistingRegistrationBehavior()
    {
        await using var f=await Fixture.Create();await f.Student();
        await f.Sql("EXEC dbo.usp_DK3_Submit '01010101-0101-0101-0101-010101010101','HK1-2627',N'[\"LHP0001\"]';");
        Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002';")));
    }
    [SeatSqlFact] public async Task StudentCannotConfigureSeatPolicies()
    {
        await using var f=await Fixture.Create();await f.Student();
        var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_Seat_Save 'GROUP','G1',NULL,N'{\"name\":\"Test\",\"source\":\"Fixture\"}';"));Assert.Equal(52801,e.Number);
    }
    [SeatSqlFact] public async Task FutureTicketRejectsEvenWhenSemesterIsOpen()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES('ROUND1','HK1-2627',N'Fixture',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),DATEADD(hour,1,SYSUTCDATETIME()),30,'ELAPSED',1);");
        await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_DK3_Submit '02020202-0202-0202-0202-020202020202','HK1-2627',N'[\"LHP0001\"]';"));Assert.Equal(52710,e.Number);
    }
    [SeatSqlFact] public async Task ReservationPreventsAnotherStudentFromTakingOfferedSeat()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET si_so_max=1 WHERE ma_lhp='LHP0002'; INSERT dbo.SEAT_WAITLIST(student,term,course,class_json,priority,status,offer_expires_utc) VALUES('SV2024001','HK1-2627','IT102',N'[\"LHP0002\"]',0,'Offered',DATEADD(hour,1,SYSUTCDATETIME())); INSERT dbo.SEAT_WAITLIST_ITEM(entry_id,class_id,group_id) SELECT SCOPE_IDENTITY(),'LHP0002',NULL;");
        await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_DK3_Submit '03030303-0303-0303-0303-030303030303','HK1-2627',N'[\"LHP0002\"]';"));Assert.Equal(52710,e.Number);
    }
    [SeatSqlFact] public async Task UnmatchedStudentCannotConsumeReservedGroupBucket()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET si_so_max=1 WHERE ma_lhp='LHP0002'; INSERT dbo.SEAT_GROUP(id,name,cohort,source,priority) VALUES('OTHER',N'Other',1990,N'Fixture profile',0); INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES('Q1','LHP0002','OTHER',1,DATEADD(day,1,SYSUTCDATETIME()));");
        await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_DK3_Submit '04040404-0404-0404-0404-040404040404','HK1-2627',N'[\"LHP0002\"]';"));Assert.Equal(52710,e.Number);
    }
    [SeatSqlFact] public async Task ExpiredGroupQuotaReturnsItsUnusedSeatToGeneralPool()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET si_so_max=1 WHERE ma_lhp='LHP0002'; INSERT dbo.SEAT_GROUP(id,name,cohort,source,priority) VALUES('OTHER',N'Other',1990,N'Fixture profile',0); INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES('Q1','LHP0002','OTHER',1,DATEADD(day,-1,SYSUTCDATETIME()));");
        await f.Student();await f.Sql("EXEC dbo.usp_DK3_Submit '05050505-0505-0505-0505-050505050505','HK1-2627',N'[\"LHP0002\"]';");
        Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0002' AND trang_thai='DANG_KY';")));
    }
    [SeatSqlFact] public async Task ProfileTicketAndQuotaAllocateTheCorrectGroupLedger()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET si_so_max=1 WHERE ma_lhp='LHP0002'; INSERT dbo.SEAT_GROUP(id,name,major,cohort,source,priority) VALUES('G1',N'CNTT 2024','CNTT',2024,N'Fixture profile',0); INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES('Q1','LHP0002','G1',1,DATEADD(day,1,SYSUTCDATETIME())); INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES('R1','HK1-2627',N'Fixture',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),NULL,30,'ELAPSED',1); INSERT dbo.SEAT_TICKET(id,round_id,group_id,opens_utc) VALUES('T1','R1','G1',DATEADD(hour,-1,SYSUTCDATETIME()));");
        await f.Student();await f.Sql("EXEC dbo.usp_DK3_Submit '06060606-0606-0606-0606-060606060606','HK1-2627',N'[\"LHP0002\"]';");
        Assert.Equal("G1",await f.Scalar("SELECT a.group_id FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002' AND d.ma_lhp='LHP0002';"));
    }
    [SeatSqlFact] public async Task WorkerOfferAndClaimAreDurableAndReplayDoesNotDuplicateEnrollment()
    {
        await using var f=await Fixture.Create();var id=await f.Offer();Assert.Equal("Offered",await f.Scalar($"SELECT status FROM dbo.SEAT_WAITLIST WHERE id={id};"));
        await f.Student();await f.Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.SEAT_WAITLIST WHERE id={id}); EXEC dbo.usp_Seat_Claim {id},@v,'07070707-0707-0707-0707-070707070707'; EXEC dbo.usp_Seat_Claim {id},@v,'07070707-0707-0707-0707-070707070707';");
        Assert.Equal("Enrolled",await f.Scalar($"SELECT status FROM dbo.SEAT_WAITLIST WHERE id={id};"));
        Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0002' AND trang_thai='DANG_KY';")));
    }
    [SeatSqlFact] public async Task HoldAddedAfterOfferIsRecheckedBeforeClaim()
    {
        await using var f=await Fixture.Create();var id=await f.Offer();
        await f.Sql("INSERT dbo.HV_DOCUMENT(kind,id,payload) VALUES('HOLD','SEAT_HOLD',N'{\"student\":\"SV2024002\",\"scope\":\"REGISTER\",\"authority\":\"Fixture\",\"reason\":\"Changed after offer\",\"expires\":\"2099-12-31\",\"active\":true}');");
        await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.SEAT_WAITLIST WHERE id={id}); EXEC dbo.usp_Seat_Claim {id},@v,'08080808-0808-0808-0808-080808080808';"));Assert.Equal(52710,e.Number);
    }
    [SeatSqlFact] public async Task ExpiryReturnsSeatAndDeclineOrCancellationReleaseReservations()
    {
        await using var f=await Fixture.Create();var id=await f.Offer();
        await f.Sql($"UPDATE dbo.SEAT_WAITLIST SET offer_expires_utc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE id={id}; EXEC dbo.usp_Seat_Process 'HK1-2627';");
        Assert.Equal("Expired",await f.Scalar($"SELECT status FROM dbo.SEAT_WAITLIST WHERE id={id};"));
        Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT reserved_count FROM dbo.v_Seat_Demand WHERE class_id='LHP0002';")));
        var next=await f.Offer();await f.Student();await f.Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.SEAT_WAITLIST WHERE id={next}); EXEC dbo.usp_Seat_Respond {next},@v,1;");
        Assert.Equal("Declined",await f.Scalar($"SELECT status FROM dbo.SEAT_WAITLIST WHERE id={next};"));
    }
    [SeatSqlFact] public async Task ClassCapacityCannotBeReducedBelowOutstandingOffer()
    {
        await using var f=await Fixture.Create();await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET si_so_max=2 WHERE ma_lhp='LHP0002';");var id=await f.Offer();
        await f.Sql("INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024001','LHP0002','DANG_KY');");
        var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("UPDATE dbo.LOP_HOC_PHAN SET si_so_max=1 WHERE ma_lhp='LHP0002';"));Assert.Equal(53003,e.Number);
    }
    [SeatSqlFact] public async Task CancelingClassReleasesItsEntireWaitlistOffer()
    {
        await using var f=await Fixture.Create();var id=await f.Offer();
        await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET lifecycle='CANCELED',trang_thai='HUY' WHERE ma_lhp='LHP0002';");
        Assert.Equal("Cancelled",await f.Scalar($"SELECT status FROM dbo.SEAT_WAITLIST WHERE id={id};"));
        Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT reserved_count FROM dbo.v_Seat_Demand WHERE class_id='LHP0002';")));
    }
    [SeatSqlFact] public async Task LinkedComponentsAreOfferedAndClaimedAsOneGroup()
    {
        await using var f=await Fixture.Create();await f.Sql("INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,trang_thai) VALUES('SEAT_LAB','IT102','GV0012','HK1-2627',1,'DANG_MO'); INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt) VALUES('SEAT_LAB','C101',7,10,12,'2026-11-07','2026-11-07'); INSERT dbo.DK3_COMBINATION(id,term,course) VALUES('SEAT_PAIR','HK1-2627','IT102'); INSERT dbo.DK3_COMBINATION_MEMBER(combination_id,class_id,component_type) VALUES('SEAT_PAIR','LHP0002','LT'),('SEAT_PAIR','SEAT_LAB','TH'); INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024001','SEAT_LAB','DANG_KY');");
        await f.Student();await f.Sql("EXEC dbo.usp_Seat_Join 'HK1-2627',N'[\"SEAT_LAB\",\"LHP0002\"]';");await f.Staff();await f.Sql("EXEC dbo.usp_Seat_Process 'HK1-2627';");
        Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.SEAT_WAITLIST WHERE status='Offered';")));
        // Raw fixture release bypasses the registration commit hook which normally wakes entries.
        await f.Sql("DELETE dbo.DANG_KY WHERE ma_lhp='SEAT_LAB'; UPDATE dbo.SEAT_WAITLIST SET next_attempt_utc=SYSUTCDATETIME() WHERE term='HK1-2627'; EXEC dbo.usp_Seat_Process 'HK1-2627';");
        Assert.Equal(2,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE w.status='Offered';")));
        await f.Student();await f.Sql("DECLARE @id bigint=(SELECT MAX(id) FROM dbo.SEAT_WAITLIST),@v binary(8)=(SELECT version FROM dbo.SEAT_WAITLIST WHERE id=(SELECT MAX(id) FROM dbo.SEAT_WAITLIST)); EXEC dbo.usp_Seat_Claim @id,@v,'09090909-0909-0909-0909-090909090909';");
        Assert.Equal(2,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002' AND d.trang_thai='DANG_KY';")));
    }
    [SeatSqlFact] public async Task AQueuedRequestCannotBeClaimedByAnotherStudent()
    {
        await using var f=await Fixture.Create();var id=await f.Offer();await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
        var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.SEAT_WAITLIST WHERE id={id}); EXEC dbo.usp_Seat_Claim {id},@v,'10101010-1010-1010-1010-101010101010';"));Assert.Equal(53004,e.Number);
    }
    [SeatSqlFact] public async Task TemporarilyBlockedPrefixCannotStarveAnEligibleLaterEntry()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("INSERT dbo.HV_DOCUMENT(kind,id,payload) VALUES('HOLD','SEAT_PREFIX',N'{\"student\":\"SV2024002\",\"scope\":\"REGISTER\",\"authority\":\"Fixture\",\"reason\":\"Temporary hold\",\"expires\":\"2099-12-31\",\"active\":true}'); INSERT dbo.SEAT_WAITLIST(student,term,course,class_json,priority) VALUES('SV2024002','HK1-2627','IT102',N'[\"LHP0002\"]',0),('SV2024001','HK1-2627','IT102',N'[\"LHP0002\"]',1); INSERT dbo.SEAT_WAITLIST_ITEM(entry_id,class_id) SELECT id,'LHP0002' FROM dbo.SEAT_WAITLIST WHERE term='HK1-2627'; EXEC dbo.usp_Seat_Process 'HK1-2627',1; UPDATE dbo.SEAT_WAITLIST SET next_attempt_utc=DATEADD(minute,-2,SYSUTCDATETIME()) WHERE status='Waiting'; EXEC dbo.usp_Seat_Process 'HK1-2627',1;");
        Assert.Equal("Offered",await f.Scalar("SELECT status FROM dbo.SEAT_WAITLIST WHERE student='SV2024001' AND term='HK1-2627';"));
        Assert.Equal("Waiting",await f.Scalar("SELECT status FROM dbo.SEAT_WAITLIST WHERE student='SV2024002' AND term='HK1-2627';"));
    }
    [SeatSqlFact] public async Task HistoricalPublicRoundCannotExposePositionInCurrentPrivateRound()
    {
        await using var f=await Fixture.Create();await f.Staff();
        await f.Sql("INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES('OLD','HK1-2627',N'Old',DATEADD(day,-3,SYSUTCDATETIME()),DATEADD(day,-2,SYSUTCDATETIME()),DATEADD(day,-3,SYSUTCDATETIME()),30,'ELAPSED',1),('CURRENT','HK1-2627',N'Current',DATEADD(hour,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),DATEADD(hour,-1,SYSUTCDATETIME()),30,'ELAPSED',0);");
        await f.Student();await f.Sql("EXEC dbo.usp_Seat_Join 'HK1-2627',N'[\"LHP0002\"]';");
        using var q=new SqlCommand("dbo.usp_Seat_Snapshot",f.Connection){CommandType=CommandType.StoredProcedure};q.Parameters.AddWithValue("@term","HK1-2627");
        using var adapter=new SqlDataAdapter(q);using var data=new DataSet();adapter.Fill(data);
        Assert.Equal(DBNull.Value,Assert.Single(data.Tables[3].Rows.Cast<DataRow>())["position"]);
    }
    [SeatSqlFact] public async Task EverySnapshotVersionIsAUsableSixteenDigitHexToken()
    {
        await using var f=await Fixture.Create();await f.Offer();await f.Staff();
        await f.Sql("INSERT dbo.SEAT_GROUP(id,name,source,priority) VALUES('HEX_GROUP',N'Hex',N'Fixture',0); INSERT dbo.SEAT_MEMBER(id,group_id,student,source,expires_utc) VALUES('HEX_MEMBER','HEX_GROUP','SV2024002',N'Fixture',DATEADD(day,1,SYSUTCDATETIME())); INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES('HEX_ROUND','HK1-2627',N'Hex',DATEADD(hour,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),DATEADD(hour,-1,SYSUTCDATETIME()),30,'ELAPSED',0); INSERT dbo.SEAT_TICKET(id,round_id,group_id,opens_utc) VALUES('HEX_TICKET','HEX_ROUND','HEX_GROUP',DATEADD(hour,-1,SYSUTCDATETIME())); INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES('HEX_QUOTA','LHP0002','HEX_GROUP',0,DATEADD(day,-1,SYSUTCDATETIME()));");
        using var q=new SqlCommand("dbo.usp_Seat_Snapshot",f.Connection){CommandType=CommandType.StoredProcedure};q.Parameters.AddWithValue("@term","HK1-2627");
        using var adapter=new SqlDataAdapter(q);using var data=new DataSet();adapter.Fill(data);
        Assert.Equal(6,data.Tables.Count);
        foreach(DataTable table in data.Tables)
        {
            Assert.NotEmpty(table.Rows.Cast<DataRow>());
            foreach(DataRow row in table.Rows)Assert.Matches("^[0-9A-F]{16}$",Assert.IsType<string>(row["version"]));
        }
    }
    [SeatSqlFact] public async Task MovingRoundCannotBypassLiveOffersInItsOriginalTerm()
    {
        await using var f=await Fixture.Create();await f.Offer();await f.Staff();
        await f.Sql("INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES('MOVING','HK1-2627',N'Fixture',DATEADD(hour,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),DATEADD(hour,-1,SYSUTCDATETIME()),30,'ELAPSED',0);");
        var error=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("DECLARE @v binary(8)=(SELECT version FROM dbo.SEAT_ROUND WHERE id='MOVING'),@json nvarchar(max)=(SELECT 'HK2-2627' term,N'Fixture' name,DATEADD(hour,-1,SYSUTCDATETIME()) startsUtc,DATEADD(day,1,SYSUTCDATETIME()) endsUtc,30 offerMinutes,'ELAPSED' calendarMode,CAST(0 AS bit) publishPosition FOR JSON PATH,WITHOUT_ARRAY_WRAPPER); EXEC dbo.usp_Seat_Save 'ROUND','MOVING',@v,@json;"));
        Assert.Equal(53003,error.Number);
    }
    [SeatSqlFact] public async Task MovingRoundCannotBypassLiveOffersInItsDestinationTerm()
    {
        await using var f=await Fixture.Create();await f.Offer();await f.Staff();
        await f.Sql("INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES('MOVING','HK2-2627',N'Fixture',DATEADD(hour,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),DATEADD(hour,-1,SYSUTCDATETIME()),30,'ELAPSED',0);");
        var error=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("DECLARE @v binary(8)=(SELECT version FROM dbo.SEAT_ROUND WHERE id='MOVING'),@json nvarchar(max)=(SELECT 'HK1-2627' term,N'Fixture' name,DATEADD(hour,-1,SYSUTCDATETIME()) startsUtc,DATEADD(day,1,SYSUTCDATETIME()) endsUtc,30 offerMinutes,'ELAPSED' calendarMode,CAST(0 AS bit) publishPosition FOR JSON PATH,WITHOUT_ARRAY_WRAPPER); EXEC dbo.usp_Seat_Save 'ROUND','MOVING',@v,@json;"));
        Assert.Equal(53003,error.Number);
    }
}
