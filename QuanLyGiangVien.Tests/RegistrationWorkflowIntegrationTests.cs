using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;
namespace QuanLyGiangVien.Tests;
[Collection("Shared SQL fixture")]
public sealed class RegistrationWorkflowIntegrationTests
{
 private sealed class Fixture:IAsyncDisposable
 {
  public SqlConnection Connection{get;}
  private bool persisted;
  private Fixture(SqlConnection c)=>Connection=c;
  public static async Task<Fixture> Create()
  {
   var setting=Environment.GetEnvironmentVariable("PHASE3_TEST_CONNECTION")!;
   Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(setting).InitialCatalog);
   var c=new SqlConnection(setting);await c.OpenAsync();var f=new Fixture(c); try {
   await f.Sql("BEGIN TRAN; DELETE dbo.DK3_REQUEST; DELETE dbo.DK3_CART; DELETE dbo.DK3_COMBINATION_MEMBER; DELETE dbo.DK3_COMBINATION; DELETE dbo.HV_DOCUMENT; DELETE dbo.HV_HISTORY; DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002'; DELETE dbo.DANG_KY WHERE ma_sv='SV2024002'; UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN(); UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(day,-2,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),trang_thai='DANG_MO' WHERE ma_hk='HK1-2627'; UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY');");await f.Sql("IF COL_LENGTH('dbo.HOC_KY','lifecycle') IS NOT NULL EXEC(N'UPDATE dbo.HOC_KY SET lifecycle=''REGISTRATION'' WHERE ma_hk=''HK1-2627'';UPDATE dbo.LOP_HOC_PHAN SET lifecycle=''REGISTRATION'' WHERE ma_hk=''HK1-2627'' AND trang_thai IN(''DANG_MO'',''FULL'');');");return f; } catch { try { await f.Sql("IF @@TRANCOUNT>0 ROLLBACK;"); } finally { await c.DisposeAsync(); } throw; }
  }
  public async Task Sql(string sql){using var q=new SqlCommand(sql,Connection);await q.ExecuteNonQueryAsync();}
  public async Task<object?> Scalar(string sql){using var q=new SqlCommand(sql,Connection);return await q.ExecuteScalarAsync();}
  public async Task<DataTable> Submit(Guid id,string add="[\"LHP0001\"]",string remove="[]")=>await Read("dbo.usp_DK3_Submit",new SqlParameter("@request_id",id),new SqlParameter("@term","HK1-2627"),new SqlParameter("@add_json",add),new SqlParameter("@remove_json",remove));
  public async Task<DataTable> Read(string proc,params SqlParameter[] args){using var q=new SqlCommand(proc,Connection){CommandType=CommandType.StoredProcedure};q.Parameters.AddRange(args);using var r=await q.ExecuteReaderAsync();var t=new DataTable();t.Load(r);return t;}
  public async Task CommitSetup(){await Sql("COMMIT;");persisted=true;}
  public async Task Pair()=>await Sql("INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max,trang_thai) SELECT 'P3LAB',ma_hp,ma_gv,ma_hk,ma_phong,40,'DANG_MO' FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0001'; INSERT dbo.LICH_HOC(ma_lhp,thu,tiet_bd,tiet_kt,ma_phong,ngay_bd,ngay_kt) SELECT TOP(1) 'P3LAB',7,10,12,ma_phong,ngay_bd,ngay_kt FROM dbo.LICH_HOC WHERE ma_lhp='LHP0001'; INSERT dbo.DK3_COMBINATION(id,term,course) VALUES('P3PAIR','HK1-2627','IT101'); INSERT dbo.DK3_COMBINATION_MEMBER(combination_id,class_id,component_type) VALUES('P3PAIR','LHP0001','LT'),('P3PAIR','P3LAB','TH');");
  public async Task<byte[]> Session(string student,string name)
  {
   var token=RandomNumberGenerator.GetBytes(32);
   using var q=new SqlCommand("INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv) VALUES(@name,'SINH_VIEN',@student);INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires) VALUES(HASHBYTES('SHA2_256',@token),CONVERT(int,SCOPE_IDENTITY()),ORIGINAL_LOGIN(),DATEADD(hour,1,SYSUTCDATETIME()));",Connection);
   q.Parameters.AddWithValue("@name",name);q.Parameters.AddWithValue("@student",student);q.Parameters.Add("@token",SqlDbType.VarBinary,32).Value=token;await q.ExecuteNonQueryAsync();return token;
  }
  public async ValueTask DisposeAsync()
  {
   await Sql("IF @@TRANCOUNT>0 ROLLBACK;");
   if(persisted) await Sql("BEGIN TRAN;DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002' OR d.ma_lhp LIKE 'P3%'; DELETE dbo.DANG_KY WHERE ma_sv='SV2024002' OR ma_lhp LIKE 'P3%';DELETE dbo.DK3_REQUEST WHERE student='SV2024002' OR result_json LIKE '%P3%';DELETE dbo.DK3_CART WHERE student='SV2024002';DELETE dbo.DK3_COMBINATION_MEMBER WHERE combination_id LIKE 'P3%';DELETE dbo.DK3_COMBINATION WHERE id LIKE 'P3%';DELETE dbo.LICH_HOC WHERE ma_lhp LIKE 'P3%';DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp LIKE 'P3%';DELETE dbo.APP_SESSION WHERE ma_tk IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap LIKE 'P3TEST%');DELETE dbo.CAMPUS_NOTIFICATION WHERE account_id IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap LIKE 'P3TEST%');DELETE dbo.TAI_KHOAN WHERE ten_dang_nhap LIKE 'P3TEST%';UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY');COMMIT;");
   await Connection.DisposeAsync();
  }
 }
 [PhaseThreeSqlFact] public async Task CartPersistsAndRejectsLostUpdate()
 {
  await using var f=await Fixture.Create();
  var saved=await f.Read("dbo.usp_DK3_SaveCart",new SqlParameter("@term","HK1-2627"),new SqlParameter("@class_json","[\"LHP0001\"]"));
  var loaded=await f.Read("dbo.usp_DK3_Cart",new SqlParameter("@term","HK1-2627"));Assert.Equal(saved.Rows[0]["class_json"],loaded.Rows[0]["class_json"]);
  var e=await Assert.ThrowsAsync<SqlException>(()=>f.Read("dbo.usp_DK3_SaveCart",new SqlParameter("@term","HK1-2627"),new SqlParameter("@class_json","[]")));Assert.Equal(52704,e.Number);
 }
 [PhaseThreeSqlFact] public async Task RetryReturnsIdenticalResultWithoutDuplicateRows()
 {
  await using var f=await Fixture.Create();var id=Guid.NewGuid();var first=await f.Submit(id);var retry=await f.Submit(id);Assert.Equal(first.Rows[0]["result_json"],retry.Rows[0]["result_json"]);Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task ReusedRequestWithDifferentPayloadIsRejected()
 {
  await using var f=await Fixture.Create();var id=Guid.NewGuid();await f.Submit(id);var e=await Assert.ThrowsAsync<SqlException>(()=>f.Submit(id,"[\"LHP0002\"]"));Assert.Equal(52705,e.Number);
 }
 [PhaseThreeSqlFact] public async Task FailedSwapRetainsTheOldSeat()
 {
  await using var f=await Fixture.Create();await f.Submit(Guid.NewGuid());var dk=Convert.ToInt64(await f.Scalar("SELECT ma_dk FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'"));
  // The procedure owns its transaction and rolls the caller back on failure; persist setup for this one check via save fixture boundary.
  await f.CommitSetup();await f.Sql("BEGIN TRAN; UPDATE dbo.LOP_HOC_PHAN SET trang_thai='DONG' WHERE ma_lhp='LHP0002';");
  await Assert.ThrowsAsync<SqlException>(()=>f.Submit(Guid.NewGuid(),"[\"LHP0002\"]","["+dk+"]"));
  Assert.Equal("DANG_KY",await f.Scalar("SELECT trang_thai FROM dbo.DANG_KY WHERE ma_dk="+dk));
 }
 [PhaseThreeSqlFact] public async Task PreviewNeverCreatesEnrollmentOrRequest()
 {
  await using var f=await Fixture.Create();var result=await f.Read("dbo.usp_DK3_Precheck",new SqlParameter("@term","HK1-2627"),new SqlParameter("@add_json","[\"LHP0001\"]"));Assert.Contains(result.Rows.Cast<DataRow>(),r=>(string)r["code"]=="CREDITS");Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task ComponentPairCountsCourseCreditsOnceAndCancelsAtomically()
 {
  await using var f=await Fixture.Create();await f.Pair();
  var check=await f.Read("dbo.usp_DK3_Precheck",new SqlParameter("@term","HK1-2627"),new SqlParameter("@add_json","[\"P3LAB\",\"LHP0001\"]"));
  Assert.DoesNotContain(check.Rows.Cast<DataRow>(),r=>(bool)r["blocking"]);
  var credits=Convert.ToInt32(await f.Scalar("SELECT so_tin_chi FROM dbo.HOC_PHAN WHERE ma_hp='IT101'"));
  Assert.Contains(check.Rows.Cast<DataRow>(),r=>(string)r["code"]=="CREDITS"&&((string)r["message"]).EndsWith(credits.ToString()));
  await f.Submit(Guid.NewGuid(),"[\"P3LAB\",\"LHP0001\"]");
  Assert.Equal(2,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
  var resultRows=await f.Read("dbo.usp_XemKetQuaDangKy",new SqlParameter("@ma_hk","HK1-2627"));Assert.Equal(credits,resultRows.Rows.Cast<DataRow>().Sum(r=>Convert.ToInt32(r["tin_chi_tinh"])));
  await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  var statistics=await f.Read("dbo.usp_ThongKeLopHocPhan",new SqlParameter("@ma_hk","HK1-2627"));
  var expectedCredits=Convert.ToInt64(await f.Scalar("SELECT SUM(CONVERT(bigint,h.so_tin_chi)) FROM (SELECT DISTINCT d.ma_sv,l.ma_hp FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.trang_thai='DANG_KY' AND l.ma_hk='HK1-2627') p JOIN dbo.HOC_PHAN h ON h.ma_hp=p.ma_hp"));
  Assert.Equal(expectedCredits,statistics.Rows.Cast<DataRow>().Sum(r=>Convert.ToInt64(r["tong_tin_chi_dang_ky"])));
  await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  var ids=(string)(await f.Scalar("SELECT '['+STRING_AGG(CONVERT(varchar(max),ma_dk),',')+']' FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'"))!;
  await f.Submit(Guid.NewGuid(),"[]",ids);
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task OneMissingComponentBlocksBothNewAndLegacyEntryPoints()
 {
  await using var f=await Fixture.Create();await f.Pair();
  var check=await f.Read("dbo.usp_DK3_Precheck",new SqlParameter("@term","HK1-2627"),new SqlParameter("@add_json","[\"LHP0001\"]"));
  Assert.Contains(check.Rows.Cast<DataRow>(),r=>(string)r["code"]=="COMPONENT"&&(bool)r["blocking"]);
  var legacy=await f.Read("dbo.usp_HocVu_Precheck",new SqlParameter("@lop_json","[\"LHP0001\"]"));
  Assert.Contains(legacy.Rows.Cast<DataRow>(),r=>(string)r["code"]=="COMPONENT"&&(bool)r["blocking"]);
 }
 [PhaseThreeSqlFact] public async Task PartialCancellationFailsAndKeepsBothComponents()
 {
  await using var f=await Fixture.Create();await f.Pair();await f.Submit(Guid.NewGuid(),"[\"P3LAB\",\"LHP0001\"]");
  var dk=Convert.ToInt64(await f.Scalar("SELECT MIN(ma_dk) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'"));
  await f.CommitSetup();
  var e=await Assert.ThrowsAsync<SqlException>(()=>f.Submit(Guid.NewGuid(),"[]","["+dk+"]"));Assert.Equal(52710,e.Number);
  Assert.Equal(2,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task FullComponentRollsBackEntirePair()
 {
  await using var f=await Fixture.Create();await f.Pair();
  await f.Sql("INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024001','P3LAB','DANG_KY');UPDATE dbo.LOP_HOC_PHAN SET si_so_max=1,si_so_hien_tai=1,trang_thai='FULL' WHERE ma_lhp='P3LAB';");
  await f.CommitSetup();
  await Assert.ThrowsAsync<SqlException>(()=>f.Submit(Guid.NewGuid(),"[\"LHP0001\",\"P3LAB\"]"));
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task ResultIsScopedToAuthenticatedStudentAndRetryOrderIsCanonical()
 {
  await using var f=await Fixture.Create();var id=Guid.NewGuid();
  var first=await f.Submit(id,"[\"LHP0001\",\"LHP0002\"]");var second=await f.Submit(id,"[\"LHP0002\",\"LHP0001\"]");Assert.Equal(first.Rows[0]["result_json"],second.Rows[0]["result_json"]);
  await f.Sql("UPDATE dbo.TAI_KHOAN SET ma_sv='SV2024001' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  var result=await f.Read("dbo.usp_DK3_Result",new SqlParameter("@request_id",id));Assert.Empty(result.Rows.Cast<DataRow>());
 }
 [PhaseThreeSqlFact] public async Task ConcurrentReplayCreatesExactlyOneAuditAndEnrollment()
 {
  await using var f=await Fixture.Create();await f.CommitSetup();var id=Guid.NewGuid();
  async Task<string> Attempt()
  {
   await using var c=new SqlConnection(f.Connection.ConnectionString);await c.OpenAsync();
   using var q=new SqlCommand("dbo.usp_DK3_Submit",c){CommandType=CommandType.StoredProcedure};q.Parameters.AddWithValue("@request_id",id);q.Parameters.AddWithValue("@term","HK1-2627");q.Parameters.AddWithValue("@add_json","[\"LHP0001\"]");
   using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());return r.GetString(r.GetOrdinal("result_json"));
  }
  var results=await Task.WhenAll(Attempt(),Attempt());Assert.Equal(results[0],results[1]);
  using var audit=new SqlCommand("SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE request_id=@id AND event_type='REGISTRATION_ADD'",f.Connection);audit.Parameters.AddWithValue("@id",id);Assert.Equal(1,Convert.ToInt32(await audit.ExecuteScalarAsync()));
  Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task TwoStudentsCompetingForLastSeatHaveOnlyOneWinner()
 {
  await using var f=await Fixture.Create();
  await f.Sql("INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max,trang_thai) SELECT 'P3LAST',ma_hp,ma_gv,ma_hk,ma_phong,1,'DANG_MO' FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0004';INSERT dbo.LICH_HOC(ma_lhp,thu,tiet_bd,tiet_kt,ma_phong,ngay_bd,ngay_kt) SELECT TOP(1) 'P3LAST',8,13,15,ma_phong,ngay_bd,ngay_kt FROM dbo.LICH_HOC WHERE ma_lhp='LHP0004';");
  var a=await f.Session("SV2024001","P3TEST_A");var b=await f.Session("SV2024002","P3TEST_B");await f.CommitSetup();
  async Task<int> Attempt(byte[] token)
  {
   await using var c=new SqlConnection(f.Connection.ConnectionString);await c.OpenAsync();
   using(var session=new SqlCommand("dbo.usp_Auth_UseSession",c){CommandType=CommandType.StoredProcedure}){session.Parameters.Add("@token",SqlDbType.VarBinary,32).Value=token;await session.ExecuteNonQueryAsync();}
   using var q=new SqlCommand("dbo.usp_DK3_Submit",c){CommandType=CommandType.StoredProcedure};q.Parameters.AddWithValue("@request_id",Guid.NewGuid());q.Parameters.AddWithValue("@term","HK1-2627");q.Parameters.AddWithValue("@add_json","[\"P3LAST\"]");
   try{await q.ExecuteNonQueryAsync();return 0;}catch(SqlException e){return e.Number;}
  }
  var outcomes=await Task.WhenAll(Attempt(a),Attempt(b));Assert.Equal(new[]{0,52710},outcomes.Order().ToArray());
  Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp='P3LAST' AND trang_thai='DANG_KY'")));
  Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT si_so_hien_tai FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='P3LAST'")));
 }
 [PhaseThreeSqlFact] public async Task SessionRevokedWhileWaitingForLockCannotCommit()
 {
  await using var f=await Fixture.Create();var token=await f.Session("SV2024002","P3TEST_REVOKE");await f.CommitSetup();
  await using var c=new SqlConnection(f.Connection.ConnectionString);await c.OpenAsync();
  using(var session=new SqlCommand("dbo.usp_Auth_UseSession",c){CommandType=CommandType.StoredProcedure}){session.Parameters.Add("@token",SqlDbType.VarBinary,32).Value=token;await session.ExecuteNonQueryAsync();}
  using var spidCommand=new SqlCommand("SELECT @@SPID",c);var spid=Convert.ToInt32(await spidCommand.ExecuteScalarAsync());
  await f.Sql("BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;");
  using var submit=new SqlCommand("dbo.usp_DK3_Submit",c){CommandType=CommandType.StoredProcedure};submit.Parameters.AddWithValue("@request_id",Guid.NewGuid());submit.Parameters.AddWithValue("@term","HK1-2627");submit.Parameters.AddWithValue("@add_json","[\"LHP0001\"]");
  var pending=submit.ExecuteNonQueryAsync();
  var waiting=false;
  for(var i=0;i<100&&!waiting;i++){waiting=Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id="+spid+" AND resource_type='APPLICATION' AND request_status='WAIT'"))>0;if(!waiting)await Task.Delay(20);}
  using(var revoke=new SqlCommand("DELETE dbo.APP_SESSION WHERE token_hash=HASHBYTES('SHA2_256',@token);",f.Connection)){revoke.Parameters.Add("@token",SqlDbType.VarBinary,32).Value=token;await revoke.ExecuteNonQueryAsync();}
  await f.Sql("COMMIT;");
  Assert.True(waiting,"Second request must actually wait on the transaction lock before revocation.");
  var error=await Assert.ThrowsAsync<SqlException>(async()=>await pending);Assert.Equal(51001,error.Number);
  Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));
 }
 [PhaseThreeSqlFact] public async Task ComponentConfigurationRequiresExplicitRolesAndPublishesThem()
 {
  await using var f=await Fixture.Create();
  await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max,trang_thai) SELECT 'P3LT',ma_hp,ma_gv,ma_hk,ma_phong,40,'DANG_MO' FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0004';INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max,trang_thai) SELECT 'P3TH',ma_hp,ma_gv,ma_hk,ma_phong,40,'DANG_MO' FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0004';");
  var result=await f.Read("dbo.usp_DK3_SaveCombination",new SqlParameter("@id","P3CONFIG"),new SqlParameter("@term","HK1-2627"),new SqlParameter("@course","EC101"),new SqlParameter("@class_json","[\"P3LT\",\"P3TH\"]"),new SqlParameter("@roles_json","{\"P3LT\":\"LT\",\"P3TH\":\"TH\"}"));
  Assert.Equal("{\"P3LT\":\"LT\",\"P3TH\":\"TH\"}",result.Rows[0]["roles_json"]);
  Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.CAMPUS_AUDIT WHERE entity='P3CONFIG' AND event_type='COMPONENT_CONFIG'")));
 }
 [PhaseThreeSqlFact] public async Task ServerRejectsRoleGuessingAndNullJson()
 {
  await using var f=await Fixture.Create();
  var invalid=await Assert.ThrowsAsync<SqlException>(()=>f.Read("dbo.usp_DK3_SaveCombination",new SqlParameter("@id","P3INVALID"),new SqlParameter("@term","HK1-2627"),new SqlParameter("@course","IT101"),new SqlParameter("@class_json","[\"LHP0001\",\"LHP0002\"]")));
  Assert.Equal(52702,invalid.Number);
  var nullJson=await Assert.ThrowsAsync<SqlException>(()=>f.Read("dbo.usp_DK3_SaveCart",new SqlParameter("@term","HK1-2627"),new SqlParameter("@class_json",SqlDbType.NVarChar,-1){Value=DBNull.Value}));Assert.Equal(52702,nullJson.Number);
 }
}
