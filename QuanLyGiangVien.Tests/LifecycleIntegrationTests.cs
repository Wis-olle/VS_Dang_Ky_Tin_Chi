using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
namespace QuanLyGiangVien.Tests;
[Collection("Shared SQL fixture")]
public sealed class LifecycleIntegrationTests
{
    private static string Connection {
        get {
            var value=Environment.GetEnvironmentVariable("LIFECYCLE_TEST_CONNECTION")!;
            Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(value).InitialCatalog,StringComparison.Ordinal);
            return value;
        }
    }
    private static async Task<SqlConnection> Open()
    {
        var settings=new SqlConnectionStringBuilder(Connection);
        Assert.StartsWith("DangKyTinChi_Minh_Test_",settings.InitialCatalog,StringComparison.Ordinal);
        var cn=new SqlConnection(Connection);await cn.OpenAsync();
        await Execute(cn,"BEGIN TRAN; IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE') INSERT dbo.CAMPUS_PERMISSION VALUES(dbo.ufn_Campus_AccountId(),'CLASS_OPERATE');");
        return cn;
    }
    private static async Task Execute(SqlConnection cn,string sql)
    {using var cmd=new SqlCommand(sql,cn){CommandTimeout=60};await cmd.ExecuteNonQueryAsync();}
    private static async Task<int> Scalar(SqlConnection cn,string sql)
    {using var cmd=new SqlCommand(sql,cn){CommandTimeout=60};return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static Task Cleanup(SqlConnection cn)=>Execute(cn,"IF @@TRANCOUNT>0 ROLLBACK;");
    private static string Change(DateOnly date) => $"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='REPLACE',@source='{date:yyyyMMdd}',@target='{date.AddDays(1):yyyyMMdd}',@room='C101',@lecturer='GV0012',@start=10,@end=12,@version=@v,@reason=N'Phòng học cần sửa chữa';";
    // Select a real future seeded Monday using the SQL clock; history guards stay enabled.
    private static async Task<DateOnly> FutureDate(SqlConnection cn)
    {
        using var command=new SqlCommand("SELECT MIN(m.ngay_hoc) FROM dbo.ufn_Campus_ActualMeetings() m JOIN dbo.HOC_KY h ON h.ma_hk=m.ma_hk WHERE m.ma_lhp='LHP0001' AND m.tiet_bd=1 AND m.ngay_hoc>CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) AND DATEADD(day,2,m.ngay_hoc)<=h.ngay_hoc_kt;",cn);
        var value=await command.ExecuteScalarAsync();
        Assert.True(value is DateTime,"The seeded term must contain a future LHP0001 meeting plus two days; refresh demo seed dates when its term ends.");
        return DateOnly.FromDateTime((DateTime)value!);
    }
    [LifecycleIntegrationFact]
    public async Task ActualCalendarAndAuditCommitTogetherWithoutRewritingBaseSchedule()
    {
        await using var cn=await Open();try{
            var date=await FutureDate(cn);
            var before=await Scalar(cn,"SELECT COUNT(*) FROM dbo.LICH_HOC WHERE ma_lhp='LHP0001';");
            await Execute(cn,Change(date));
            Assert.Equal(0,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date:yyyyMMdd}';"));
            Assert.Equal(1,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date.AddDays(1):yyyyMMdd}' AND ma_gv='GV0012' AND ma_phong='C101';"));
            Assert.Equal(before,await Scalar(cn,"SELECT COUNT(*) FROM dbo.LICH_HOC WHERE ma_lhp='LHP0001';"));
            Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE entity='CLASS:LHP0001' AND event_type='SCHEDULE_CHANGED';")>0);
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task PreviewReturnsStudentConflictForEveryEnrolledStudentAndRollsBack()
    {
        await using var cn=await Open();
        // Second student has another class; this conflict is invisible if only the operator or first student is checked.
        await Execute(cn,"INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024002','LHP0001','DANG_KY'); UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=2 WHERE ma_lhp='LHP0001';");
        using var cmd=new SqlCommand("DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); DECLARE @date date,@start int,@end int; SELECT TOP(1) @date=ngay_hoc,@start=tiet_bd,@end=tiet_kt FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0004' AND ngay_hoc>CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) ORDER BY ngay_hoc; EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='MAKEUP',@target=@date,@room='C101',@lecturer='GV0012',@start=@start,@end=@end,@version=@v,@reason=N'Buổi bù kiểm tra tác động',@preview=1;",cn){CommandTimeout=60};
        using var reader=await cmd.ExecuteReaderAsync();var students=new List<string>();
        while(await reader.ReadAsync())students.Add(Convert.ToString(reader["student"])??"");
        Assert.Contains("SV2024002",students);await reader.DisposeAsync();
        Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.CAMPUS_SCHEDULE_CHANGE WHERE class_id='LHP0001';"));
    }
    [LifecycleIntegrationFact]
    public async Task StudentConflictRejectsAndKeepsOriginalCalendar()
    {
        await using var cn=await Open();
        await Execute(cn,"INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024002','LHP0001','DANG_KY');");
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'),@date date,@start int,@end int; SELECT TOP(1) @date=ngay_hoc,@start=tiet_bd,@end=tiet_kt FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0004' AND ngay_hoc>CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) ORDER BY ngay_hoc; EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='MAKEUP',@target=@date,@room='C101',@lecturer='GV0012',@start=@start,@end=@end,@version=@v,@reason=N'Thay đổi lịch xung đột';"));
        Assert.Equal(52604,error.Number);
        Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.CAMPUS_SCHEDULE_CHANGE WHERE class_id='LHP0001';"));
    }
    [LifecycleIntegrationFact]
    public async Task StaleVersionAndHistoryMutationAreRejected()
    {
        await using var cn=await Open();
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='CANCEL',@source='20261019',@version=0x0000000000000000,@reason=N'Phiên bản quá cũ';"));
        Assert.Equal(52601,error.Number);
        await Execute(cn,"BEGIN TRAN; INSERT dbo.CAMPUS_PERMISSION SELECT dbo.ufn_Campus_AccountId(),'CLASS_OPERATE' WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE');");
        error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='CANCEL',@source='20260907',@version=@v,@reason=N'Không sửa lịch đã học';"));
        Assert.Equal(52605,error.Number);
    }
    [LifecycleIntegrationFact]
    public async Task CompletedClassesReleaseRoomButRetainHistoryAndCalendar()
    {
        await using var cn=await Open();try{
            await Execute(cn,"DECLARE @v binary(8); SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CLOSED',@v,N'Đóng cửa sổ đăng ký'; SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_Transition 'LHP0001','TEACHING',@v,N'Bắt đầu giảng dạy'; SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_Transition 'LHP0001','COMPLETED',@v,N'Hoàn tất giảng dạy';");
            Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001';")>0);
            Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='LHP0001' AND trang_thai='DANG_KY';")>0);
            Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001' AND lifecycle NOT IN('COMPLETED','CANCELED','ARCHIVED');"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task HolidayChangesActualCalendarAndVersionWithoutDeletingBase()
    {
        await using var cn=await Open();try{
            var date=await FutureDate(cn);
            await Execute(cn,$"DECLARE @v binary(8)=(SELECT lifecycle_version FROM dbo.HOC_KY WHERE ma_hk='HK1-2627'); EXEC dbo.usp_Lifecycle_Holiday 'HK1-2627','{date:yyyyMMdd}',1,N'Ngày nghỉ toàn học kỳ',@v;");
            Assert.Equal(0,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_hk='HK1-2627' AND ngay_hoc='{date:yyyyMMdd}';"));
            Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.LICH_HOC WHERE ma_lhp='LHP0001';")>0);
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task MissingOperatePermissionIsNotAutoGranted()
    {
        await using var cn=await Open();var date=await FutureDate(cn);await Execute(cn,"DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE';");
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,Change(date)));Assert.Equal(52801,error.Number);
        Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.CAMPUS_PERMISSION WHERE permission='CLASS_OPERATE';"));
    }
    [LifecycleIntegrationFact]
    public async Task LinkedClassCannotBeCanceledLeavingHalfCombination()
    {
        await using var cn=await Open();
        await Execute(cn,"INSERT dbo.DK3_COMBINATION(id,term,course) VALUES('LIFECYCLE_TEST','HK1-2627','IT101'); INSERT dbo.DK3_COMBINATION_MEMBER(combination_id,class_id,component_type) VALUES('LIFECYCLE_TEST','LHP0001','LT');");
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CANCELED',@v,N'Hủy lớp thành phần';"));
        Assert.Equal(52603,error.Number);
        Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='LHP0001' AND trang_thai='DANG_KY';")>0);
    }
    [LifecycleIntegrationFact]
    public async Task CancellationPreservesRegistrationHistoryAndNotifiesItsStudents()
    {
        await using var cn=await Open();try{
            await Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CANCELED',@v,N'Hủy lớp vì lý do vận hành';");
            Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='LHP0001' AND trang_thai='DANG_KY';"));
            Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='LHP0001' AND trang_thai='DA_HUY';")>0);
            Assert.Equal(1,await Scalar(cn,"SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE event_type='CLASS_CANCELED' AND student='SV2024001';"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task StudentProjectionIncludesSubstituteLecturerAndActualRoom()
    {
        await using var cn=await Open();try{
            var date=await FutureDate(cn);
            await Execute(cn,Change(date));
            await Execute(cn,"INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai,ma_sv) VALUES('lifecycle-student-fixture','SINH_VIEN',1,'SV2024001'); DECLARE @id int=SCOPE_IDENTITY(),@token varbinary(32)=CRYPT_GEN_RANDOM(32); INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires) VALUES(HASHBYTES('SHA2_256',@token),@id,ORIGINAL_LOGIN(),DATEADD(hour,1,SYSUTCDATETIME())); EXEC sys.sp_set_session_context @key=N'campus_token',@value=@token;");
            using var command=new SqlCommand("dbo.usp_SinhVien_ThoiKhoaBieu",cn){CommandType=System.Data.CommandType.StoredProcedure,CommandTimeout=60};
            using var reader=await command.ExecuteReaderAsync();var data=StudentTimetableRepository.Read(reader);
            Assert.Contains(data.Teachers,t=>t.Id=="GV0012");
            var occurrence=Assert.Single(SchedulingQuery.Occurrences(data.CreateStore(),date.AddDays(1),date.AddDays(1),lecturerId:"GV0012"));
            Assert.Equal("C101",occurrence.RoomId);
            await reader.DisposeAsync();
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task LegacySemesterEditorCannotReviveCompletedTerm()
    {
        await using var cn=await Open();
        await Execute(cn,"UPDATE dbo.HOC_KY SET lifecycle='COMPLETED',trang_thai='DONG' WHERE ma_hk='HK1-2627';");
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(32)=(SELECT phien_ban FROM dbo.vw_Phong_HocKy WHERE ma_hk='HK1-2627'); EXEC dbo.usp_Phong_HocKy_DoiTrangThai 'HK1-2627','DANG_MO',@v;"));
        Assert.Equal(52602,error.Number);
    }
    [LifecycleIntegrationFact]
    public async Task CompletionReleasesRealRoomAndAllowsOverlappingNewClass()
    {
        await using var cn=await Open();try{
            await Execute(cn,"INSERT dbo.PHONG_HOC(ma_phong,ten_phong,toa_nha,suc_chua,loai_phong,trang_thai) VALUES('LIFECYCLE_ROOM',N'Kiểm thử',N'Tòa kiểm thử',60,N'Lý thuyết',1); UPDATE dbo.LICH_HOC SET ma_phong='LIFECYCLE_ROOM' WHERE ma_lhp='LHP0001'; DECLARE @v binary(8); SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CLOSED',@v,N'Đóng đăng ký để kiểm thử'; SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_Transition 'LHP0001','TEACHING',@v,N'Bắt đầu giảng dạy'; SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_Transition 'LHP0001','COMPLETED',@v,N'Hoàn tất giảng dạy'; SELECT @v=phien_ban FROM dbo.PHONG_HOC WHERE ma_phong='LIFECYCLE_ROOM'; EXEC dbo.usp_Minh_LuuPhong 'LIFECYCLE_ROOM',N'Tòa kiểm thử',60,N'Lý thuyết',0,0,@v;");
            Assert.Equal(0,await Scalar(cn,"SELECT CONVERT(int,trang_thai) FROM dbo.PHONG_HOC WHERE ma_phong='LIFECYCLE_ROOM';"));
            await Execute(cn,"UPDATE dbo.PHONG_HOC SET trang_thai=1 WHERE ma_phong='LIFECYCLE_ROOM'; EXEC dbo.usp_Minh_LuuLop @ma_lhp='LIFECYCLE_NEW',@ma_hp='IT102',@ma_gv='GV0012',@ma_hk='HK1-2627',@si_so_max=40,@trang_thai='DANG_MO',@lich_json=N'[{\"RoomId\":\"LIFECYCLE_ROOM\",\"Thu\":2,\"StartPeriod\":1,\"EndPeriod\":3,\"StartDate\":\"2026-09-07\",\"EndDate\":\"2026-12-27\"}]',@la_moi=1;");
            Assert.Equal(1,await Scalar(cn,"SELECT COUNT(*) FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LIFECYCLE_NEW';"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task ConcurrentSameVersionTransitionAppliesOnlyOnce()
    {
        await using var first=await Open();await using var second=new SqlConnection(Connection);await second.OpenAsync();
        try{
            using var versionCommand=new SqlCommand("SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'",first);
            var version=Convert.ToHexString((byte[])(await versionCommand.ExecuteScalarAsync())!);
            await Execute(first,$"EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CLOSED',0x{version},N'Đóng đăng ký đồng thời';");
            var competing=Execute(second,$"EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CLOSED',0x{version},N'Yêu cầu trùng phiên bản';");
            await Task.Delay(100);await Execute(first,"COMMIT;");
            var error=await Assert.ThrowsAsync<SqlException>(()=>competing);Assert.Equal(52601,error.Number);
        }finally{
            await Cleanup(first);
            await Execute(first,"UPDATE dbo.LOP_HOC_PHAN SET lifecycle='REGISTRATION',trang_thai='DANG_MO' WHERE ma_lhp='LHP0001'; DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE';");
        }
    }
    [LifecycleIntegrationFact]
    public async Task SavingUnregisteredBaseScheduleReturnsFreshActualDatesImmediately()
    {
        var repository=new SqlMinhRepository(Connection);const string id="LIFECYCLE_FRESH";
        await using var cleanup=new SqlConnection(Connection);await cleanup.OpenAsync();
        try{
            var item=repository.SaveClass(new(id,"IT102","HK1-2627","GV0012",10,0,SchedulingLabels.Open,[]),true);
            Assert.False(item.ActualMeetings.IsDefault);Assert.Empty(item.ActualMeetings);
            item=repository.SaveClass(item with {Meetings=[new("C101",DayOfWeek.Tuesday,10,12,new(2026,11,2),new(2026,11,8))]},false);
            Assert.Equal(new DateOnly(2026,11,3),Assert.Single(item.ActualMeetings).Date);
            item=repository.SaveClass(item with {Meetings=[new("C101",DayOfWeek.Thursday,10,12,new(2026,11,2),new(2026,11,8))]},false);
            Assert.Equal(new DateOnly(2026,11,5),Assert.Single(item.ActualMeetings).Date);
        }finally{
            await Execute(cleanup,"DELETE dbo.LICH_HOC WHERE ma_lhp='LIFECYCLE_FRESH'; DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp='LIFECYCLE_FRESH';");
        }
    }
    [LifecycleIntegrationFact]
    public async Task ReplacingOneOfTwoMeetingsOnSameDayRetainsTheOtherOccurrence()
    {
        await using var cn=await Open();try{
            var date=await FutureDate(cn);
            await Execute(cn,$"INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt) VALUES('LHP0001','A201',2,7,9,'{date:yyyyMMdd}','{date:yyyyMMdd}'); DECLARE @id bigint=(SELECT TOP(1) ma_lich FROM dbo.LICH_HOC WHERE ma_lhp='LHP0001' AND tiet_bd=1),@v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='REPLACE',@source='{date:yyyyMMdd}',@target='{date.AddDays(1):yyyyMMdd}',@room='C101',@lecturer='GV0012',@start=10,@end=12,@version=@v,@reason=N'Chỉ đổi một buổi sáng',@source_meeting_id=@id;");
            Assert.Equal(1,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date:yyyyMMdd}' AND tiet_bd=7 AND tiet_kt=9;"));
            Assert.Equal(0,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date:yyyyMMdd}' AND tiet_bd=1;"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task MakeupCanBeRescheduledAndCanceledUsingItsStableIdentity()
    {
        await using var cn=await Open();try{
            var date=await FutureDate(cn);
            await Execute(cn,$"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='MAKEUP',@target='{date.AddDays(1):yyyyMMdd}',@room='C101',@lecturer='GV0012',@start=10,@end=12,@version=@v,@reason=N'Bổ sung buổi học bù'; DECLARE @id bigint=(SELECT source_meeting_id FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date.AddDays(1):yyyyMMdd}'); SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='REPLACE',@source='{date.AddDays(1):yyyyMMdd}',@target='{date.AddDays(2):yyyyMMdd}',@room='C101',@lecturer='GV0012',@start=10,@end=12,@version=@v,@reason=N'Chuyển buổi bù vì phòng hỏng',@source_meeting_id=@id;");
            Assert.Equal(1,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date.AddDays(2):yyyyMMdd}' AND source_meeting_id<0;"));
            await Execute(cn,$"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'),@id bigint=(SELECT source_meeting_id FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date.AddDays(2):yyyyMMdd}' AND source_meeting_id<0); EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='CANCEL',@source='{date.AddDays(1):yyyyMMdd}',@version=@v,@reason=N'Hủy buổi bù đã chuyển',@source_meeting_id=@id;");
            Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND source_meeting_id<0;"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task FutureReplacementCanChangeEvenWhenItsOriginalDateIsInThePast()
    {
        await using var cn=await Open();try{
            var date=await FutureDate(cn);
            await Execute(cn,$"DECLARE @id bigint=(SELECT TOP(1) ma_lich FROM dbo.LICH_HOC WHERE ma_lhp='LHP0001' AND tiet_bd=1),@v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); INSERT dbo.CAMPUS_SCHEDULE_CHANGE(class_id,kind,source_date,target_date,room_id,lecturer_id,start_period,end_period,reason,actor,before_version,source_meeting_id) VALUES('LHP0001','REPLACE','20260907','{date.AddDays(1):yyyyMMdd}','C101','GV0012',10,12,N'Quyết định chuyển lịch trước đây',dbo.ufn_Campus_AccountId(),@v,@id); EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0001',@kind='REPLACE',@source='20260907',@target='{date.AddDays(2):yyyyMMdd}',@room='C101',@lecturer='GV0012',@start=10,@end=12,@version=@v,@reason=N'Thay đổi buổi chưa diễn ra',@source_meeting_id=@id;");
            Assert.Equal(1,await Scalar(cn,$"SELECT COUNT(*) FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND ngay_hoc='{date.AddDays(2):yyyyMMdd}' AND source_date='20260907';"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task ClassCancellationCannotLeaveOtherRegisteredCourseWithoutItsCorequisite()
    {
        await using var cn=await Open();
        await Execute(cn,"INSERT dbo.HV_DOCUMENT(kind,id,payload,published) VALUES('POLICY','LIFE_POLICY',N'{\"name\":\"Fixture\",\"source\":\"Test only\",\"term\":\"HK1-2627\",\"standing\":\"*\",\"minCredits\":0,\"maxCredits\":30,\"passGrade\":5,\"allowImprove\":true,\"maxAttempts\":0,\"enforceCurriculum\":false}',1),('PROGRAM','LIFE_PROGRAM',N'{\"name\":\"Fixture\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":6}',1),('ASSIGNMENT','LIFE_ASSIGN',N'{\"student\":\"SV2024001\",\"program\":\"LIFE_PROGRAM\"}',0),('REQUIREMENT','LIFE_CO',N'{\"program\":\"LIFE_PROGRAM\",\"course\":\"IT102\",\"group\":1,\"requiredCourse\":\"IT101\",\"kind\":\"CO\",\"minGrade\":0}',0);");
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'); EXEC dbo.usp_Lifecycle_Transition 'LHP0001','CANCELED',@v,N'Hủy lớp có môn đồng hành';"));
        Assert.Equal(52603,error.Number);
        Assert.True(await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='LHP0001' AND trang_thai='DANG_KY';")>0);
    }
    private static async Task PrepareCorequisiteAuthorization(SqlConnection cn,bool consumed)
    {
        await Execute(cn,"INSERT dbo.HV_DOCUMENT(kind,id,payload,published) VALUES('POLICY','LIFE_AUTH_POLICY',N'{\"name\":\"Fixture\",\"source\":\"Test only\",\"term\":\"HK1-2627\",\"standing\":\"*\",\"minCredits\":0,\"maxCredits\":30,\"passGrade\":5,\"allowImprove\":true,\"maxAttempts\":0,\"enforceCurriculum\":false}',1),('PROGRAM','LIFE_AUTH_PROGRAM',N'{\"name\":\"Fixture\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":6}',1),('ASSIGNMENT','LIFE_AUTH_ASSIGN',N'{\"student\":\"SV2024002\",\"program\":\"LIFE_AUTH_PROGRAM\"}',0),('REQUIREMENT','LIFE_AUTH_CO',N'{\"program\":\"LIFE_AUTH_PROGRAM\",\"course\":\"IT101\",\"group\":1,\"requiredCourse\":\"IT102\",\"kind\":\"CO\",\"minGrade\":0}',0); INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024002','LHP0001','DANG_KY'); UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY') WHERE ma_lhp='LHP0001'; INSERT dbo.DK3_EXCEPTION_REQUEST(student,term,kind,course,class_id,rule_code,program_id,reason,status,submitted_by,reviewed_by,reviewed_utc,decision_reason,expires_utc,granted_uses,remaining_uses) VALUES('SV2024002','HK1-2627','REQUIREMENT','IT101','LHP0001','GROUP_1','LIFE_AUTH_PROGRAM',N'Fixture ngoại lệ CO','APPROVED',dbo.ufn_Campus_AccountId(),dbo.ufn_Campus_AccountId(),SYSUTCDATETIME(),N'Duyệt fixture','2099-12-31',2,2);");
        if(consumed) await Execute(cn,"DECLARE @exception bigint=(SELECT MAX(id) FROM dbo.DK3_EXCEPTION_REQUEST WHERE student='SV2024002'),@result nvarchar(max)=(SELECT ma_dk,ma_lhp,'ADD' action FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY' FOR JSON PATH); INSERT dbo.DK3_REQUEST(student,request_id,payload_hash,operation,term,result_json) VALUES('SV2024002','99999999-1111-2222-3333-999999999999',0x01,'ADD','HK1-2627',@result); INSERT dbo.DK3_EXCEPTION_USE(exception_id,request_id,actor) VALUES(@exception,'99999999-1111-2222-3333-999999999999',dbo.ufn_Campus_AccountId()); UPDATE dbo.DK3_EXCEPTION_REQUEST SET remaining_uses=1 WHERE id=@exception;");
    }
    [LifecycleIntegrationFact]
    public async Task CancelingUnrelatedClassRetainsConsumedCorequisiteAuthorizationWithoutAnotherUse()
    {
        await using var cn=await Open();try{
            await PrepareCorequisiteAuthorization(cn,true);
            await Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0004'); EXEC dbo.usp_Lifecycle_Transition 'LHP0004','CANCELED',@v,N'Hủy lớp không liên quan CO đã miễn';");
            Assert.Equal(1,await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY';"));
            Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0004' AND trang_thai='DANG_KY';"));
            Assert.Equal(1,await Scalar(cn,"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE request_id='99999999-1111-2222-3333-999999999999';"));
            Assert.Equal(1,await Scalar(cn,"SELECT remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE student='SV2024002' AND program_id='LIFE_AUTH_PROGRAM';"));
        }finally{await Cleanup(cn);}
    }
    [LifecycleIntegrationFact]
    public async Task AdministratorCannotApplyUnusedCorequisiteGrantWhileCancelingAnotherClass()
    {
        await using var cn=await Open();await PrepareCorequisiteAuthorization(cn,false);
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(8)=(SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0004'); EXEC dbo.usp_Lifecycle_Transition 'LHP0004','CANCELED',@v,N'Không được dùng quyền chưa áp dụng';"));
        Assert.Equal(52603,error.Number);
        Assert.Equal(1,await Scalar(cn,"SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0004' AND trang_thai='DANG_KY';"));
        Assert.Equal(0,await Scalar(cn,"SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_USE WHERE request_id='99999999-1111-2222-3333-999999999999';"));
    }
    [LifecycleIntegrationFact]
    public async Task OperationalRepositoryUsesRealCalendarAndVersionedTransitions()
    {
        var repository=new LifecycleRepository(Connection);var writer=new SqlMinhRepository(Connection);
        await using var cn=new SqlConnection(Connection);await cn.OpenAsync();
        var permissionBefore=await Scalar(cn,"SELECT COUNT(*) FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE';");
        static string Version(System.Data.DataTable rows,string key,string id)=>Convert.ToHexString((byte[])rows.Rows.Cast<System.Data.DataRow>().Single(r=>(string)r[key]==id)["phien_ban"]);
        try{
            await Execute(cn,"INSERT dbo.CAMPUS_PERMISSION SELECT dbo.ufn_Campus_AccountId(),'CLASS_OPERATE' WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE');");
            await Execute(cn,"UPDATE dbo.HOC_KY SET lifecycle='REGISTRATION',trang_thai='DANG_MO' WHERE ma_hk='HK2-2627';");
            Assert.NotEmpty((await repository.ClassesAsync("HK1-2627")).Rows.Cast<System.Data.DataRow>());
            var terms=await repository.TermsAsync();
            await repository.TransitionSemesterAsync("HK2-2627","CLOSED",Version(terms,"ma_hk","HK2-2627"),"Đóng đăng ký kiểm thử");
            terms=await repository.TermsAsync();
            await repository.TransitionSemesterAsync("HK2-2627","REGISTRATION",Version(terms,"ma_hk","HK2-2627"),"Mở lại kỳ kiểm thử");
            terms=await repository.TermsAsync();
            await repository.SetHolidayAsync("HK2-2627",new(2027,1,12),true,Version(terms,"ma_hk","HK2-2627"),"Ngày nghỉ kiểm thử API");
            terms=await repository.TermsAsync();
            await repository.SetHolidayAsync("HK2-2627",new(2027,1,12),false,Version(terms,"ma_hk","HK2-2627"),"Khôi phục ngày kiểm thử API");
            var item=writer.SaveClass(new("LIFECYCLE_API","IT102","HK1-2627","GV0012",10,0,SchedulingLabels.Open,[new("C101",DayOfWeek.Tuesday,10,12,new(2026,11,2),new(2026,11,8))]),true);
            var change=new LifecycleScheduleChange(item.Id,"MAKEUP",null,new(2026,11,6),"C101","GV0012",10,12,item.Version,"Bổ sung buổi bù API");
            Assert.Empty((await repository.PreviewScheduleAsync(change)).Rows.Cast<System.Data.DataRow>());
            await repository.ChangeScheduleAsync(change);
            Assert.Equal(2,(await repository.CalendarAsync(item.Id)).Rows.Count);
            Assert.NotEmpty((await repository.HistoryAsync("HK1-2627")).Rows.Cast<System.Data.DataRow>());
            foreach(var target in new[]{"CLOSED","TEACHING","COMPLETED","ARCHIVED"}){
                var classes=await repository.ClassesAsync("HK1-2627");
                await repository.TransitionClassAsync(item.Id,target,Version(classes,"ma_lhp",item.Id),"Chuyển trạng thái qua API");
            }
            var archived=await repository.ClassesAsync("HK1-2627");
            await Assert.ThrowsAsync<QuanLyGiangVien.Domain.PersistenceException>(()=>repository.TransitionClassAsync(item.Id,"REGISTRATION",Version(archived,"ma_lhp",item.Id),"Không hồi sinh lớp đã lưu trữ"));
        }finally{
            await Execute(cn,"DELETE dbo.CAMPUS_SCHEDULE_CHANGE WHERE class_id='LIFECYCLE_API'; DELETE dbo.LICH_HOC WHERE ma_lhp='LIFECYCLE_API'; DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp='LIFECYCLE_API'; DELETE dbo.CAMPUS_HOLIDAY WHERE term='HK2-2627' AND [date]='20270112'; UPDATE dbo.HOC_KY SET lifecycle='REGISTRATION',trang_thai='DANG_MO' WHERE ma_hk='HK2-2627';");
            if(permissionBefore==0)await Execute(cn,"DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE';");
        }
    }
    [LifecycleIntegrationFact]
    public async Task AdminSnapshotUsesActualCalendarProjection()
    {
        var snapshot=await new SqlMinhRepository(Connection).LoadAsync();
        Assert.All(snapshot.Classes,c=>Assert.False(c.ActualMeetings.IsDefault));
        var store=snapshot.CreateStores().Scheduling;
        Assert.NotEmpty(SchedulingQuery.Occurrences(store,new(2026,10,19),new(2026,10,25)));
    }
    [LifecycleIntegrationFact]
    public async Task LegacySemesterDeadlineEditRequiresOperatePermissionEvenWhenStatusUnchanged()
    {
        await using var cn=await Open();
        await Execute(cn,"DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='CLASS_OPERATE';");
        var error=await Assert.ThrowsAsync<SqlException>(()=>Execute(cn,"DECLARE @v binary(32),@year varchar(9),@ky tinyint,@start datetime2,@end datetime2,@drop datetime2,@from date,@to date; SELECT @v=phien_ban,@year=nam_hoc,@ky=hoc_ky,@start=tg_bat_dau,@end=DATEADD(day,1,tg_ket_thuc),@drop=han_huy_dk,@from=ngay_hoc_bd,@to=ngay_hoc_kt FROM dbo.vw_Phong_HocKy WHERE ma_hk='HK1-2627'; EXEC dbo.usp_Phong_HocKy_Luu @la_moi=0,@ma_hk='HK1-2627',@nam_hoc=@year,@hoc_ky=@ky,@tg_bat_dau=@start,@tg_ket_thuc=@end,@han_huy_dk=@drop,@ngay_hoc_bd=@from,@ngay_hoc_kt=@to,@phien_ban=@v;"));
        Assert.Equal(52801,error.Number);
    }
}
public sealed class LifecycleIntegrationFactAttribute:FactAttribute
{
    public LifecycleIntegrationFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIFECYCLE_TEST_CONNECTION")))Skip="Requires separate lifecycle SQL fixture.";}
}
