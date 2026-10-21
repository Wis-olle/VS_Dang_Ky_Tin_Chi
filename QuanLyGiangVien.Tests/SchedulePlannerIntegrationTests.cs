using System.Collections.Immutable;
using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;
using Xunit;
namespace QuanLyGiangVien.Tests;
public sealed class SchedulePlannerSqlFactAttribute:FactAttribute
{public SchedulePlannerSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLANNER_TEST_CONNECTION")))Skip="Requires isolated Planner4 fixture.";}}
[Collection("Shared SQL fixture")]
public sealed class SchedulePlannerIntegrationTests
{
 private sealed class Fixture:IAsyncDisposable
 {
  public string Setting{get;}public SqlConnection Connection{get;}public SchedulePlannerRepository Repository=>new(Setting);
  private Fixture(string setting,SqlConnection connection){Setting=setting;Connection=connection;}
  public static async Task<Fixture> Create()
  {
   var setting=Environment.GetEnvironmentVariable("PLANNER_TEST_CONNECTION")!;Assert.Contains("_Test_Planner4_",new SqlConnectionStringBuilder(setting).InitialCatalog);
   var connection=new SqlConnection(setting);await connection.OpenAsync();var fixture=new Fixture(setting,connection);
   await fixture.Sql("DELETE dbo.DK3_CART WHERE student='SV2024002';DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002';DELETE dbo.DANG_KY WHERE ma_sv='SV2024002';DELETE dbo.HV_HISTORY;DELETE dbo.HV_DOCUMENT;DELETE dbo.CAMPUS_HOLIDAY;DELETE dbo.DK3_COMBINATION_MEMBER WHERE combination_id LIKE 'PL4%';DELETE dbo.DK3_COMBINATION WHERE id LIKE 'PL4%';DELETE dbo.SEAT_QUOTA WHERE id LIKE 'PL4%';DELETE dbo.SEAT_GROUP WHERE id LIKE 'PL4%';UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002',trang_thai=1 WHERE ten_dang_nhap=ORIGINAL_LOGIN();UPDATE dbo.HOC_KY SET lifecycle='REGISTRATION',trang_thai='DANG_MO',tg_bat_dau=DATEADD(day,-1,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())) WHERE ma_hk='HK1-2627';UPDATE dbo.LOP_HOC_PHAN SET lifecycle='REGISTRATION',trang_thai='DANG_MO',si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY') WHERE ma_hk='HK1-2627';");return fixture;
  }
  public async Task Sql(string sql){using var q=new SqlCommand(sql,Connection);await q.ExecuteNonQueryAsync();}
  public async Task<object?> Scalar(string sql){using var q=new SqlCommand(sql,Connection);return await q.ExecuteScalarAsync();}
  public async ValueTask DisposeAsync(){await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();");await Connection.DisposeAsync();}
 }
 private static SchedulePlannerRequest Request(params string[] courses)=>new(courses.ToImmutableArray(),[],[],new(),MaxMilliseconds:10000);
 [SchedulePlannerSqlFact]public async Task SnapshotUsesAuthenticatedOwnPlanAndDoesNotInventAcademicEnforcement()
 {
  await using var f=await Fixture.Create();var snapshot=await f.Repository.LoadAsync("HK1-2627");Assert.NotEmpty(snapshot.Classes);Assert.DoesNotContain(snapshot.Classes,c=>c.IsRegistered);
  var result=await f.Repository.GenerateAsync("HK1-2627",Request("IT101","IT102"));Assert.NotEmpty(result.Options);Assert.Contains(result.Options[0].Warnings,w=>w.Contains("chưa công bố"));Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [SchedulePlannerSqlFact]public async Task CoRequisitesAreCheckedAsOneWholeProposal()
 {
  await using var f=await Fixture.Create();
  await f.Sql("INSERT dbo.HV_DOCUMENT(kind,id,payload,published,published_at) VALUES('PROGRAM','PL4_CT',N'{\"name\":\"Fixture CT\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":6}',1,SYSUTCDATETIME()),('COURSE','PL4_C1',N'{\"program\":\"PL4_CT\",\"course\":\"IT101\",\"required\":true}',0,NULL),('COURSE','PL4_C2',N'{\"program\":\"PL4_CT\",\"course\":\"IT102\",\"required\":true}',0,NULL),('ASSIGNMENT','PL4_A',N'{\"student\":\"SV2024002\",\"program\":\"PL4_CT\"}',0,NULL),('REQUIREMENT','PL4_R',N'{\"program\":\"PL4_CT\",\"course\":\"IT101\",\"group\":1,\"requiredCourse\":\"IT102\",\"kind\":\"CO\",\"minGrade\":0}',0,NULL),('POLICY','PL4_P',N'{\"name\":\"Fixture policy\",\"source\":\"Isolated test\",\"term\":\"HK1-2627\",\"standing\":\"*\",\"minCredits\":1,\"maxCredits\":24,\"passGrade\":5,\"allowImprove\":true,\"maxAttempts\":0,\"enforceCurriculum\":true}',1,SYSUTCDATETIME());");
  var missing=await f.Repository.GenerateAsync("HK1-2627",Request("IT101"));Assert.Empty(missing.Options);Assert.Contains(missing.Explanations,m=>m.Contains("điều kiện"));
  var whole=await f.Repository.GenerateAsync("HK1-2627",Request("IT101","IT102"));Assert.NotEmpty(whole.Options);
 }
 [SchedulePlannerSqlFact]public async Task SelectedPlanOnlyMergesIntoVersionedCartWithoutEnrollment()
 {
  await using var f=await Fixture.Create();var plan=await f.Repository.GenerateAsync("HK1-2627",Request("IT101"));Assert.NotEmpty(plan.Options);
  using var cart=await f.Repository.AddToCartAsync("HK1-2627",plan.Options[0],plan.CartVersion);Assert.Contains("LHP0001",(string)cart.Rows[0]["class_json"]);
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
  var stale=await Assert.ThrowsAsync<PersistenceException>(()=>f.Repository.AddToCartAsync("HK1-2627",plan.Options[0],plan.CartVersion));Assert.Contains("Giỏ đã thay đổi",stale.Message);
 }
 [SchedulePlannerSqlFact]public async Task LiveClosureOrHoldAfterGenerationCannotEnterCart()
 {
  await using var f=await Fixture.Create();var plan=await f.Repository.GenerateAsync("HK1-2627",Request("IT101"));Assert.NotEmpty(plan.Options);
  await f.Sql("UPDATE dbo.LOP_HOC_PHAN SET trang_thai='DONG' WHERE ma_lhp='LHP0001';");await Assert.ThrowsAsync<PersistenceException>(()=>f.Repository.AddToCartAsync("HK1-2627",plan.Options[0],plan.CartVersion));
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DK3_CART WHERE student='SV2024002'")));
 }
 [SchedulePlannerSqlFact]public async Task HolidayDatesAreExcludedFromBusyTimeConflicts()
 {
  await using var f=await Fixture.Create();var request=Request("IT101") with{BusyTimes=[new(new(2026,1,1),new(2026,12,31),DayOfWeek.Monday,1,20)]};Assert.Empty((await f.Repository.GenerateAsync("HK1-2627",request)).Options);
  await f.Sql("INSERT dbo.CAMPUS_HOLIDAY(term,[date],active,reason,actor) SELECT DISTINCT 'HK1-2627',ngay_hoc,1,N'Fixture holiday',dbo.ufn_Campus_AccountId() FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0001' AND DATEDIFF(day,CONVERT(date,'19000101'),ngay_hoc)%7=0;");
  Assert.NotEmpty((await f.Repository.GenerateAsync("HK1-2627",request)).Options);
 }
 [SchedulePlannerSqlFact]public async Task TrainingRoleCannotUseStudentGenerator()
 {await using var f=await Fixture.Create();await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();");await Assert.ThrowsAsync<PersistenceException>(()=>f.Repository.LoadAsync("HK1-2627"));}
 [SchedulePlannerSqlFact]public async Task QuotaChangesAfterGenerationAreCheckedWithoutHoldingSeats()
 {
  await using var f=await Fixture.Create();var generated=await f.Repository.GenerateAsync("HK1-2627",Request("IT101"));Assert.NotEmpty(generated.Options);
  await f.Sql("INSERT dbo.SEAT_GROUP(id,name,source,priority) VALUES('PL4_OTHER',N'Other explicit group',N'Fixture only',1000);INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES('PL4_QUOTA','LHP0001','PL4_OTHER',39,DATEADD(day,1,SYSUTCDATETIME()));");
  var blocked=await f.Repository.GenerateAsync("HK1-2627",Request("IT101"));Assert.Empty(blocked.Options);Assert.Contains(blocked.Explanations,m=>m.Contains("nhóm"));
  await Assert.ThrowsAsync<PersistenceException>(()=>f.Repository.AddToCartAsync("HK1-2627",generated.Options[0],generated.CartVersion));
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DK3_CART WHERE student='SV2024002'")));
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.SEAT_WAITLIST")));
 }
}
