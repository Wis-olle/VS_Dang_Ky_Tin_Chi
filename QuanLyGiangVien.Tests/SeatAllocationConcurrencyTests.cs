using Microsoft.Data.SqlClient;
using Xunit;
using System.Diagnostics;
using System.Text.Json;
namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class SeatAllocationConcurrencyTests
{
    [SeatSqlFact] public async Task TwelveWorkersAndTwoClaimRetriesAllocateOnlyOneLastSeat()
    {
        var cs=Environment.GetEnvironmentVariable("SEAT_TEST_CONNECTION")!;Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(cs).InitialCatalog);
        await using var setup=new SqlConnection(cs);await setup.OpenAsync();int account=0;string? role=null,student=null;bool hadPermission=false;
        async Task Sql(string sql){using var q=new SqlCommand(sql,setup){CommandTimeout=60};await q.ExecuteNonQueryAsync();}
        async Task<object?> Scalar(string sql){using var q=new SqlCommand(sql,setup);return await q.ExecuteScalarAsync();}
        try
        {
            using(var q=new SqlCommand("SELECT ma_tk,vai_tro,ma_sv FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN();",setup))
            using(var r=await q.ExecuteReaderAsync()){Assert.True(await r.ReadAsync());account=r.GetInt32(0);role=r.GetString(1);student=r.IsDBNull(2)?null:r.GetString(2);}
            hadPermission=Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='WAITLIST_OPERATE';"))>0;
            await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); INSERT dbo.CAMPUS_PERMISSION SELECT dbo.ufn_Campus_AccountId(),'WAITLIST_OPERATE' WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='WAITLIST_OPERATE'); INSERT dbo.HOC_KY(ma_hk,nam_hoc,hoc_ky,tg_bat_dau,tg_ket_thuc,han_huy_dk,trang_thai,ngay_hoc_bd,ngay_hoc_kt,lifecycle) VALUES('SEAT_RACE_TERM','2030-2031',1,DATEADD(day,-1,DATEADD(hour,7,SYSUTCDATETIME())),DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())),DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())),'DANG_MO','2030-01-01','2030-06-01','REGISTRATION'); INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,trang_thai) VALUES('SEAT_RACE_CLASS','IT102','GV0012','SEAT_RACE_TERM',1,'DANG_MO'); INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt) VALUES('SEAT_RACE_CLASS','C101',2,10,12,'2030-01-01','2030-01-07'); INSERT dbo.SEAT_WAITLIST(student,term,course,class_json,priority) VALUES('SV2024002','SEAT_RACE_TERM','IT102',N'[\"SEAT_RACE_CLASS\"]',0),('SV2024001','SEAT_RACE_TERM','IT102',N'[\"SEAT_RACE_CLASS\"]',1); INSERT dbo.SEAT_WAITLIST_ITEM(entry_id,class_id) SELECT id,'SEAT_RACE_CLASS' FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM';");
            async Task Work(){await using var c=new SqlConnection(cs);await c.OpenAsync();using var q=new SqlCommand("EXEC dbo.usp_Seat_Work 'SEAT_RACE_TERM';",c){CommandTimeout=60};await q.ExecuteNonQueryAsync();}
            var timer=Stopwatch.StartNew();await Task.WhenAll(Enumerable.Range(0,12).Select(_=>Work()));
            Assert.Equal(1,Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM' AND status='Offered';")));
            var id=Convert.ToInt64(await Scalar("SELECT id FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM' AND student='SV2024002';"));
            await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
            var repository=new QuanLyGiangVien.Data.Sql.SeatAllocationRepository(cs);
            using var offeredSnapshot=await repository.SnapshotAsync("SEAT_RACE_TERM");
            var offeredRow=Assert.Single(offeredSnapshot.Tables[3].Rows.Cast<System.Data.DataRow>());
            var version=Assert.IsType<string>(offeredRow["version"]);Assert.Matches("^[0-9A-F]{16}$",version);
            var request=QuanLyGiangVien.Domain.Registration.SeatAllocationValidation.ClaimRequest("SV2024002",id,version);
            async Task Claim(){using var result=await repository.ClaimAsync(id,version,request);Assert.NotEmpty(result.Tables.Cast<System.Data.DataTable>());}
            await Task.WhenAll(Claim(),Claim());
            Assert.Equal(1,Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='SEAT_RACE_CLASS' AND trang_thai='DANG_KY';")));
            Assert.Equal(1,Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_lhp='SEAT_RACE_CLASS';")));
            Assert.Equal(1,Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE event_type='WAITLIST_ENROLLED' AND entity IN(SELECT CONCAT('WAITLIST:',id) FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM');")));
            using var snapshot=await repository.SnapshotAsync("SEAT_RACE_TERM");
            Assert.Equal("Enrolled",Assert.Single(snapshot.Tables[3].Rows.Cast<System.Data.DataRow>())["status"]);
            timer.Stop();var output=Environment.GetEnvironmentVariable("SEAT_LOAD_OUTPUT");
            if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{workers=12,simultaneousClaimRetries=2,offered=1,enrolled=1,allocationRows=1,claimEvents=1,elapsedMilliseconds=timer.ElapsedMilliseconds,utc=DateTime.UtcNow,scope="Local SQL concurrency fixture; not a production capacity claim"},new JsonSerializerOptions{WriteIndented=true}));}
        }
        finally
        {
            await Sql("DELETE n FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE o.entity='SV2024002:SEAT_RACE_TERM' OR o.entity IN(SELECT CONCAT('WAITLIST:',id) FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM'); DELETE dbo.CAMPUS_OUTBOX WHERE entity='SV2024002:SEAT_RACE_TERM' OR entity IN(SELECT CONCAT('WAITLIST:',id) FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM'); DELETE dbo.CAMPUS_AUDIT WHERE entity='SV2024002:SEAT_RACE_TERM' OR entity IN(SELECT CONCAT('WAITLIST:',id) FROM dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM'); DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_lhp='SEAT_RACE_CLASS'; DELETE dbo.DANG_KY WHERE ma_lhp='SEAT_RACE_CLASS'; DELETE dbo.DK3_REQUEST WHERE term='SEAT_RACE_TERM'; DELETE i FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE w.term='SEAT_RACE_TERM'; DELETE dbo.SEAT_WAITLIST WHERE term='SEAT_RACE_TERM'; DELETE dbo.LICH_HOC WHERE ma_lhp='SEAT_RACE_CLASS'; DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp='SEAT_RACE_CLASS'; DELETE dbo.HOC_KY WHERE ma_hk='SEAT_RACE_TERM';");
            if(role is not null){using var restore=new SqlCommand("UPDATE dbo.TAI_KHOAN SET vai_tro=@role,ma_sv=@student WHERE ma_tk=@id; IF @had=0 DELETE dbo.CAMPUS_PERMISSION WHERE account_id=@id AND permission='WAITLIST_OPERATE';",setup);restore.Parameters.AddWithValue("@role",role);restore.Parameters.AddWithValue("@student",(object?)student??DBNull.Value);restore.Parameters.AddWithValue("@id",account);restore.Parameters.AddWithValue("@had",hadPermission);await restore.ExecuteNonQueryAsync();}
        }
    }
}
