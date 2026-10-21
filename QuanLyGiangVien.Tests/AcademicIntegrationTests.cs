using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
namespace QuanLyGiangVien.Tests;

public sealed class AcademicSqlFactAttribute : FactAttribute
{
 public AcademicSqlFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ACADEMIC_TEST_CONNECTION"))) Skip="Requires isolated academic SQL fixture.";}
}
[Collection("Shared SQL fixture")]
public sealed class AcademicIntegrationTests
{
 private sealed class Fixture : IAsyncDisposable
 {
  public SqlConnection Connection {get;}
  private Fixture(SqlConnection c)=>Connection=c;
  public static async Task<Fixture> Create()
  {
   var setting=Environment.GetEnvironmentVariable("ACADEMIC_TEST_CONNECTION")!;
   Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(setting).InitialCatalog);
   var c=new SqlConnection(setting);await c.OpenAsync();var f=new Fixture(c); try {
   // This fixture explicitly opens registration; keep the phase-three lifecycle in sync.
   await f.Sql("IF COL_LENGTH('dbo.HOC_KY','lifecycle') IS NOT NULL EXEC(N'UPDATE dbo.HOC_KY SET lifecycle=''REGISTRATION'' WHERE ma_hk=''HK1-2627'';');");
   await f.Sql("BEGIN TRAN; DELETE dbo.HV_DOCUMENT; DELETE dbo.HV_HISTORY; UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(day,-2,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,2,DATEADD(hour,7,SYSUTCDATETIME())),trang_thai='DANG_MO' WHERE ma_hk='HK1-2627'; DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024002'; DELETE dbo.DANG_KY WHERE ma_sv='SV2024002'; UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY');");return f; } catch { try { await f.Sql("IF @@TRANCOUNT>0 ROLLBACK;"); } finally { await c.DisposeAsync(); } throw; }
  }
  public async Task Sql(string sql){using var q=new SqlCommand(sql,Connection);await q.ExecuteNonQueryAsync();}
  public async Task<object?> Scalar(string sql){using var q=new SqlCommand(sql,Connection);return await q.ExecuteScalarAsync();}
  public async Task Save(string kind,string id,object payload,bool update=false)
  {
   using var q=new SqlCommand("dbo.usp_HocVu_Save",Connection){CommandType=CommandType.StoredProcedure};
   q.Parameters.AddWithValue("@kind",kind);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@payload",JsonSerializer.Serialize(payload));
   if(update){using var v=new SqlCommand("SELECT version FROM dbo.HV_DOCUMENT WHERE kind=@k AND id=@id",Connection);v.Parameters.AddWithValue("@k",kind);v.Parameters.AddWithValue("@id",id);q.Parameters.Add("@version",SqlDbType.Binary,8).Value=(await v.ExecuteScalarAsync())!;}
   await q.ExecuteNonQueryAsync();
  }
  public async Task Publish(string kind,string id)=>await Sql($"DECLARE @v binary(8)=(SELECT version FROM dbo.HV_DOCUMENT WHERE kind='{kind}' AND id='{id}'); EXEC dbo.usp_HocVu_Publish '{kind}','{id}',@v;");
  public async Task Policy(int max=24,bool improve=true,int attempts=0,bool curriculum=false)
  {
   await Save("POLICY","P1",new{name="Test policy",source="Fixture policy, not institutional rule",term="HK1-2627",standing="*",minCredits=Math.Min(14,max),maxCredits=max,passGrade=5,allowImprove=improve,maxAttempts=attempts,enforceCurriculum=curriculum});
   await Publish("POLICY","P1");
  }
  public async Task Program()
  {
   await Save("PROGRAM","CT1",new{name="CT test",major="CNTT",cohort=2024,requiredCredits=6});
   await Save("COURSE","C1",new{program="CT1",course="IT101",required=true});
   await Save("COURSE","C2",new{program="CT1",course="IT102",required=true});
  }
  public async Task Assign(){await Publish("PROGRAM","CT1");await Save("ASSIGNMENT","A1",new{student="SV2024002",program="CT1"});}
  public async Task Student()=>await Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  public async Task<DataTable> Check(string json="[\"LHP0001\"]",string op="REGISTER",long? dk=null,string? term=null)
  {
   using var q=new SqlCommand("dbo.usp_HocVu_Precheck",Connection){CommandType=CommandType.StoredProcedure};q.Parameters.AddWithValue("@lop_json",json);q.Parameters.AddWithValue("@thao_tac",op);if(dk.HasValue)q.Parameters.AddWithValue("@ma_dk",dk.Value);
   if(term!=null)q.Parameters.AddWithValue("@ma_hk",term);
   using var r=await q.ExecuteReaderAsync();var t=new DataTable();t.Load(r);return t;
  }
  public async ValueTask DisposeAsync(){await Sql("IF @@TRANCOUNT>0 ROLLBACK;");await Connection.DisposeAsync();}
 }
 private static bool Has(DataTable t,string code,bool? blocking=null)=>t.Rows.Cast<DataRow>().Any(r=>(string)r["code"]==code && (!blocking.HasValue || (bool)r["blocking"]==blocking));
 [AcademicSqlFact] public async Task MissingPolicyWarnsWithoutInventingAcademicRules()
 {await using var f=await Fixture.Create();await f.Student();using var t=await f.Check();Assert.True(Has(t,"UNCONFIGURED",false));}
 [AcademicSqlFact] public async Task CreditMaximumBlocksAndMinimumOnlyWarns()
 {await using var f=await Fixture.Create();await f.Policy(max:14);await f.Student();using var t=await f.Check();Assert.True(Has(t,"MIN_CREDITS",false));Assert.False(Has(t,"MAX_CREDITS"));}
 [AcademicSqlFact] public async Task HoldEnforcedEvenWhenPolicyMissing()
 {await using var f=await Fixture.Create();await f.Save("HOLD","H1",new{student="SV2024002",scope="REGISTER",authority="Dao tao",reason="Ho so",expires="2099-12-31",active=true});await f.Student();using var t=await f.Check();Assert.True(Has(t,"HOLD",true));}
 [AcademicSqlFact] public async Task ExpiredHoldDoesNotBlock()
 {await using var f=await Fixture.Create();await f.Save("HOLD","H1",new{student="SV2024002",scope="ALL",authority="Dao tao",reason="Ho so",expires="2000-01-01",active=true});await f.Student();using var t=await f.Check();Assert.False(Has(t,"HOLD"));}
 [AcademicSqlFact] public async Task SuspendedAcademicStatusBlocks()
 {await using var f=await Fixture.Create();await f.Save("STANDING","S1",new{student="SV2024002",term="HK1-2627",status="SUSPENDED",standing="NORMAL",reason="Bao luu"});await f.Student();using var t=await f.Check();Assert.True(Has(t,"STANDING",true));}
 [AcademicSqlFact] public async Task PublishedProgramAndChildrenAreImmutable()
 {await using var f=await Fixture.Create();await f.Program();await f.Publish("PROGRAM","CT1");var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("COURSE","C1",new{program="CT1",course="IT101",required=false},true));Assert.Equal(52603,e.Number);}
 [AcademicSqlFact] public async Task StaleUpdateRejected()
 {await using var f=await Fixture.Create();await f.Program();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("PROGRAM","CT1",new{name="Other"}));Assert.Equal(52604,e.Number);}
 [AcademicSqlFact] public async Task PrerequisiteCycleRejected()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT101",group=1,requiredCourse="IT102",kind="PRE",minGrade=5});var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("REQUIREMENT","R2",new{program="CT1",course="IT102",group=1,requiredCourse="IT101",kind="PRIOR",minGrade=0}));Assert.Equal(52605,e.Number);}
 [AcademicSqlFact] public async Task MissingPrerequisiteExplained()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT101",group=1,requiredCourse="IT102",kind="PRE",minGrade=5});await f.Assign();await f.Policy();await f.Student();using var t=await f.Check();Assert.True(Has(t,"REQUIREMENT",true));}
 [AcademicSqlFact] public async Task AtomicCorequisiteProposalSatisfiesCycle()
 {await using var f=await Fixture.Create();await f.Program();foreach(var (id,a,b) in new[]{("R1","IT101","IT102"),("R2","IT102","IT101")})await f.Save("REQUIREMENT",id,new{program="CT1",course=a,group=1,requiredCourse=b,kind="CO",minGrade=0});await f.Assign();await f.Policy();await f.Student();using var alone=await f.Check();Assert.True(Has(alone,"REQUIREMENT"));using var pair=await f.Check("[\"LHP0001\",\"LHP0002\"]");Assert.False(Has(pair,"REQUIREMENT"));}
 [AcademicSqlFact] public async Task PublishedGradeAndAuditHistoryRequired()
 {await using var f=await Fixture.Create();await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=6,passed=true,source="Bang diem chinh thuc",publication="2026-10-01",reason="Nhap lan dau"});await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=7,passed=true,source="QD sua diem",publication="2026-10-01",reason="Phuc khao"},true);Assert.Equal(2,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.HV_HISTORY WHERE kind='RESULT' AND id='G1'")));}
 [AcademicSqlFact] public async Task NoImprovePolicyBlocksPassedEquivalent()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("COURSE","C1",new{program="CT1",course="IT101",required=true,equivalence="EQ1"},true);await f.Save("COURSE","C2",new{program="CT1",course="IT102",required=true,equivalence="EQ1"},true);await f.Save("PROGRAM","CT1",new{name="CT test",major="CNTT",cohort=2024,requiredCredits=3},true);await f.Assign();await f.Policy(improve:false);await f.Save("RESULT","G1",new{student="SV2024002",course="IT102",term="HK1-2627",attempt=1,grade=6,passed=true,source="Quyet dinh",publication="2026-10-01",reason="Cong nhan"});await f.Student();using var t=await f.Check();Assert.True(Has(t,"ALREADY_PASSED",true));}
 [AcademicSqlFact] public async Task StudentCannotWriteAcademicConfiguration()
 {await using var f=await Fixture.Create();await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Policy());Assert.Equal(52001,e.Number);}
 [AcademicSqlFact] public async Task CreditLimitCheckedAgainstWholeBatch()
 {await using var f=await Fixture.Create();await f.Policy(max:3);await f.Student();using var t=await f.Check("[\"LHP0001\",\"LHP0002\"]");Assert.True(Has(t,"MAX_CREDITS",true));}
 [AcademicSqlFact] public async Task OverrideNeedsReasonAndExpiryAndAllowsExtraCredits()
 {await using var f=await Fixture.Create();await f.Policy(max:3);await f.Save("OVERRIDE","O1",new{student="SV2024002",term="HK1-2627",minCredits=0,maxCredits=6,expires="2099-12-31",reason="Duyet hoc vuot"});await f.Student();using var t=await f.Check("[\"LHP0001\",\"LHP0002\"]");Assert.False(Has(t,"MAX_CREDITS"));}
 [AcademicSqlFact] public async Task DraftResultIsNotRecognized()
 {await using var f=await Fixture.Create();await f.Policy(improve:false);await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=7,passed=true,source="Bang diem nhap",publication=(string?)null,reason="Nhap"});await f.Student();using var t=await f.Check();Assert.False(Has(t,"ALREADY_PASSED"));}
 [AcademicSqlFact] public async Task PriorAllowsFailedCompletedCourse()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT101",group=1,requiredCourse="IT102",kind="PRIOR",minGrade=0});await f.Assign();await f.Policy();await f.Sql("UPDATE dbo.HOC_KY SET ngay_hoc_bd='2025-01-01',ngay_hoc_kt='2025-05-01' WHERE ma_hk='HK2-2627';");await f.Save("RESULT","G1",new{student="SV2024002",course="IT102",term="HK2-2627",attempt=1,grade=3,passed=false,source="Bang diem",publication="2026-01-01",reason="Nhap"});await f.Student();using var t=await f.Check();Assert.False(Has(t,"REQUIREMENT"));}
 [AcademicSqlFact] public async Task AtomicBatchCommitsCorequisiteCycleAndCancelProtectsRemainingCourse()
 {await using var f=await Fixture.Create();await f.Program();foreach(var (id,a,b) in new[]{("R1","IT101","IT102"),("R2","IT102","IT101")})await f.Save("REQUIREMENT",id,new{program="CT1",course=a,group=1,requiredCourse=b,kind="CO",minGrade=0});await f.Assign();await f.Policy();await f.Student();await f.Sql("EXEC dbo.usp_HocVu_DangKyNhom N'[\"LHP0001\",\"LHP0002\"]';");Assert.Equal(2,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND trang_thai='DANG_KY'")));var registrationId=Convert.ToInt64(await f.Scalar("SELECT ma_dk FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'"));using var t=await f.Check("[]","CANCEL",registrationId);Assert.True(Has(t,"REQUIREMENT",true));}
 [AcademicSqlFact] public async Task ExecuteOnlyUserCanCommitWrapperButCannotBypassInternalCore()
 {await using var f=await Fixture.Create();await f.Student();await f.Sql("CREATE USER academic_exec_test WITHOUT LOGIN; ALTER ROLE app_minh ADD MEMBER academic_exec_test; EXECUTE AS USER='academic_exec_test'; EXEC dbo.usp_HocVu_DangKyNhom N'[\"LHP0001\"]'; REVERT;");Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'")));var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXECUTE AS USER='academic_exec_test'; EXEC dbo.usp_HocVu_RegisterCore 'LHP0002';"));Assert.Equal(229,e.Number);await f.Sql("REVERT;");}
 [AcademicSqlFact] public async Task SnapshotVersionRoundTripsAsHex()
 {await using var f=await Fixture.Create();await f.Program();var version=Convert.ToString(await f.Scalar("SELECT version FROM dbo.v_HV_Programs WHERE id='CT1'"))!;Assert.Matches("^[A-F0-9]{16}$",version);await f.Sql($"DECLARE @v binary(8)=CONVERT(binary(8),'{version}',2); EXEC dbo.usp_HocVu_Save 'PROGRAM','CT1',@v,N'{{\"name\":\"Edited\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":7}}';");Assert.Equal("Edited",await f.Scalar("SELECT name FROM dbo.v_HV_Programs WHERE id='CT1'"));}
 [AcademicSqlFact] public async Task ResultMetadataComesFromServerAndRemainsStableOnCorrection()
 {await using var f=await Fixture.Create();await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=6,passed=true,source="Bang diem",publication="2026-01-01",reason="Nhap",credits=999,courseName="Forged"});Assert.Equal(3,Convert.ToInt32(await f.Scalar("SELECT credits FROM dbo.v_HV_Results WHERE id='G1'")));await f.Sql("UPDATE dbo.HOC_PHAN SET so_tin_chi=4 WHERE ma_hp='IT101';");await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=7,passed=true,source="Bang diem",publication="2026-01-01",reason="Sua diem",credits=-1},true);Assert.Equal(3,Convert.ToInt32(await f.Scalar("SELECT credits FROM dbo.v_HV_Results WHERE id='G1'")));}
 [AcademicSqlFact] public async Task ResultOwnershipCannotBeChangedToAnotherStudent()
 {await using var f=await Fixture.Create();await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=6,passed=true,source="Bang diem",publication="2026-01-01",reason="Nhap"});var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("RESULT","G1",new{student="SV2024001",course="IT101",term="HK1-2627",attempt=1,grade=6,passed=true,source="Bang diem",publication="2026-01-01",reason="Chuyen sai"},true));Assert.Equal(52602,e.Number);}
 [AcademicSqlFact] public async Task PublishingPolicyRequiresDocumentSource()
 {await using var f=await Fixture.Create();await f.Save("POLICY","P1",new{name="Unattributed",term="HK1-2627",standing="*",minCredits=0,maxCredits=24,passGrade=5,allowImprove=true,maxAttempts=0,enforceCurriculum=false});var e=await Assert.ThrowsAsync<SqlException>(()=>f.Publish("POLICY","P1"));Assert.Equal(52602,e.Number);}
 [AcademicSqlFact] public async Task GroupReferencedByCoursesCannotMovePrograms()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("PROGRAM","CT2",new{name="Other",major="CNTT",cohort=2024,requiredCredits=3});await f.Save("GROUP","GR1",new{program="CT1",name="Tu chon",requiredCredits=3});await f.Save("COURSE","C1",new{program="CT1",course="IT101",group="GR1",required=true},true);var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("GROUP","GR1",new{program="CT2",name="Chuyen",requiredCredits=3},true));Assert.Equal(52602,e.Number);}
 [AcademicSqlFact] public async Task ExemptionHasCreditWithoutInventedGradeAndCannotSatisfySpecificGrade()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT101",group=1,requiredCourse="IT102",kind="PRE",minGrade=5});await f.Assign();await f.Policy();await f.Save("RESULT","G1",new{student="SV2024002",course="IT102",term="HK2-2627",attempt=1,recognition="EXEMPT",grade=(decimal?)null,passed=true,source="QD mien hoc",publication="2026-01-01",reason="Cong nhan"});Assert.Equal(DBNull.Value,await f.Scalar("SELECT grade FROM dbo.v_HV_Results WHERE id='G1'"));Assert.Equal(3,Convert.ToInt32(await f.Scalar("SELECT credits FROM dbo.v_HV_Results WHERE id='G1'")));await f.Student();using var t=await f.Check();Assert.True(Has(t,"REQUIREMENT",true));}
 [AcademicSqlFact] public async Task ExemptionSatisfiesUnscoredPrerequisiteWithoutCountingAsStudyAttempt()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT101",group=1,requiredCourse="IT102",kind="PRE",minGrade=0});await f.Assign();await f.Policy(attempts:1);await f.Sql("UPDATE dbo.HOC_KY SET ngay_hoc_bd='2025-01-01',ngay_hoc_kt='2025-05-01' WHERE ma_hk='HK2-2627';");await f.Save("RESULT","G1",new{student="SV2024002",course="IT102",term="HK1-2627",attempt=1,recognition="EXEMPT",grade=(decimal?)null,passed=true,source="QD mien hoc",publication="2026-01-01",reason="Cong nhan"});await f.Student();using var t=await f.Check();Assert.False(Has(t,"REQUIREMENT"));using var study=await f.Check("[\"LHP0002\"]");Assert.False(Has(study,"ATTEMPTS"));}
 [AcademicSqlFact] public async Task EquivalentResultsAccumulateCreditsOnceInOwnSnapshot()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("PROGRAM","CT1",new{name="CT test",major="CNTT",cohort=2024,requiredCredits=3},true);await f.Save("COURSE","C1",new{program="CT1",course="IT101",required=true,equivalence="EQ1"},true);await f.Save("COURSE","C2",new{program="CT1",course="IT102",required=true,equivalence="EQ1"},true);await f.Assign();foreach(var (id,course) in new[]{("G1","IT101"),("G2","IT102")})await f.Save("RESULT",id,new{student="SV2024002",course,term="HK1-2627",attempt=1,grade=7,passed=true,source="Bang diem",publication="2026-01-01",reason="Nhap"});await f.Student();using var q=new SqlCommand("dbo.usp_HocVu_Snapshot",f.Connection){CommandType=CommandType.StoredProcedure};using var adapter=new SqlDataAdapter(q);var snapshot=new DataSet();adapter.Fill(snapshot);Assert.Equal(18,snapshot.Tables.Count);Assert.Single(snapshot.Tables[15].Rows.Cast<DataRow>());Assert.Equal(3,Convert.ToInt32(snapshot.Tables[16].Rows[0]["accumulatedCredits"]));}
 [AcademicSqlFact] public async Task CancelPreservesMixedOrGroupAlternatives()
 {await using var f=await Fixture.Create();await f.Program();await f.Save("COURSE","C3",new{program="CT1",course="IT103",required=true});await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT102",group=1,requiredCourse="IT101",kind="CO",minGrade=0});await f.Save("REQUIREMENT","R2",new{program="CT1",course="IT102",group=1,requiredCourse="IT103",kind="PRE",minGrade=5});await f.Assign();await f.Policy();await f.Sql("UPDATE dbo.HOC_KY SET ngay_hoc_bd='2025-01-01',ngay_hoc_kt='2025-05-01' WHERE ma_hk='HK2-2627';");await f.Save("RESULT","G1",new{student="SV2024002",course="IT103",term="HK2-2627",attempt=1,grade=7,passed=true,source="Bang diem",publication="2026-01-01",reason="Nhap"});await f.Student();await f.Sql("EXEC dbo.usp_HocVu_DangKyNhom N'[\"LHP0001\",\"LHP0002\"]';");var registrationId=Convert.ToInt64(await f.Scalar("SELECT ma_dk FROM dbo.DANG_KY WHERE ma_sv='SV2024002' AND ma_lhp='LHP0001' AND trang_thai='DANG_KY'"));using var t=await f.Check("[]","CANCEL",registrationId);Assert.False(Has(t,"REQUIREMENT"));}
 [AcademicSqlFact] public async Task SaveReauthenticatesAfterWaitingForAcademicLock()
 {
  var settings=Environment.GetEnvironmentVariable("ACADEMIC_TEST_CONNECTION")!;Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(settings).InitialCatalog);
  await using var blocker=new SqlConnection(settings);await blocker.OpenAsync();await using var caller=new SqlConnection(settings);await caller.OpenAsync();
  int accountId=0;var token=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
  async Task Run(SqlConnection c,string text){using var q=new SqlCommand(text,c);await q.ExecuteNonQueryAsync();}
  try
  {
   using(var create=new SqlCommand("INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai) VALUES(@name,'DAO_TAO',1); SELECT CONVERT(int,SCOPE_IDENTITY());",blocker)){create.Parameters.AddWithValue("@name","race_"+Guid.NewGuid().ToString("N"));accountId=Convert.ToInt32(await create.ExecuteScalarAsync());}
   using(var create=new SqlCommand("INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires) VALUES(HASHBYTES('SHA2_256',@token),@id,ORIGINAL_LOGIN(),DATEADD(hour,1,SYSUTCDATETIME()));",blocker)){create.Parameters.Add("@token",SqlDbType.VarBinary,32).Value=token;create.Parameters.AddWithValue("@id",accountId);await create.ExecuteNonQueryAsync();}
   using(var auth=new SqlCommand("dbo.usp_Auth_UseSession",caller){CommandType=CommandType.StoredProcedure}){auth.Parameters.Add("@token",SqlDbType.VarBinary,32).Value=token;await auth.ExecuteNonQueryAsync();}
   await Run(blocker,"BEGIN TRAN; DECLARE @rc int; EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Academics',@LockMode='Exclusive',@LockOwner='Transaction';");
   var saving=Run(caller,"EXEC dbo.usp_HocVu_Save 'PROGRAM','RACE_TEST',NULL,N'{\"name\":\"Race\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":3}';");
   await Task.Delay(300);Assert.False(saving.IsCompleted);
   await Run(blocker,$"UPDATE dbo.TAI_KHOAN SET trang_thai=0 WHERE ma_tk={accountId}; COMMIT;");
   var e=await Assert.ThrowsAsync<SqlException>(async()=>await saving);Assert.Equal(51001,e.Number);
   using var check=new SqlCommand("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='RACE_TEST';",blocker);Assert.Equal(0,Convert.ToInt32(await check.ExecuteScalarAsync()));
  }
  finally{await Run(blocker,$"IF @@TRANCOUNT>0 ROLLBACK; DELETE dbo.APP_SESSION WHERE ma_tk={accountId}; DELETE dbo.TAI_KHOAN WHERE ma_tk={accountId}; DELETE dbo.HV_HISTORY WHERE id='RACE_TEST'; DELETE dbo.HV_DOCUMENT WHERE id='RACE_TEST';");}
 }
 [AcademicSqlFact] public async Task OfficialGradedResultRejectsPassedFlagBelowPublishedThreshold()
 {await using var f=await Fixture.Create();await f.Policy();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=4,passed=true,source="Bang diem",publication="2026-01-01",reason="Nhap"}));Assert.Equal(52602,e.Number);}
 [AcademicSqlFact] public async Task OfficialGradedResultRejectsFailedFlagAbovePublishedThreshold()
 {await using var f=await Fixture.Create();await f.Policy();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=7,passed=false,source="Bang diem",publication="2026-01-01",reason="Nhap"}));Assert.Equal(52602,e.Number);}
 [AcademicSqlFact] public async Task SnapshotDoesNotAccumulateOldGradedFlagAgainstPublishedPolicy()
 {await using var f=await Fixture.Create();await f.Save("RESULT","G1",new{student="SV2024002",course="IT101",term="HK1-2627",attempt=1,grade=4,passed=true,source="Nguon cu chua co policy",publication="2026-01-01",reason="Nhap"});await f.Policy();await f.Student();using var q=new SqlCommand("dbo.usp_HocVu_Snapshot",f.Connection){CommandType=CommandType.StoredProcedure};using var adapter=new SqlDataAdapter(q);var data=new DataSet();adapter.Fill(data);Assert.Equal(0,Convert.ToInt32(data.Tables[16].Rows[0]["accumulatedCredits"]));}
 [AcademicSqlFact] public async Task DraftChildChangeInvalidatesParentPublishToken()
 {await using var f=await Fixture.Create();await f.Program();var version=Convert.ToString(await f.Scalar("SELECT version FROM dbo.v_HV_Programs WHERE id='CT1'"))!;await f.Save("COURSE","C3",new{program="CT1",course="IT103",required=true});var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql($"DECLARE @v binary(8)=CONVERT(binary(8),'{version}',2); EXEC dbo.usp_HocVu_Publish 'PROGRAM','CT1',@v;"));Assert.Equal(52604,e.Number);}
 [AcademicSqlFact] public async Task CreditPlanAtZeroCreditsChecksMinimumForExplicitSemester()
 {await using var f=await Fixture.Create();await f.Policy();await f.Student();using var t=await f.Check("[]","FINALIZE",term:"HK1-2627");Assert.True(Has(t,"MIN_CREDITS",true));Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024002'")));}
 [AcademicSqlFact] public async Task ConfirmedFinalSemesterCanBeExemptFromMinimum()
 {await using var f=await Fixture.Create();await f.Policy();await f.Save("STANDING","S1",new{student="SV2024002",term="HK1-2627",status="ACTIVE",standing="NORMAL",finalTerm=true,reason="Phong dao tao xac nhan ky cuoi"});await f.Student();using var t=await f.Check("[]","FINALIZE",term:"HK1-2627");Assert.False(Has(t,"MIN_CREDITS"));}
 [AcademicSqlFact] public async Task CreditPlanRejectsUnknownSemester()
 {await using var f=await Fixture.Create();await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Check("[]","FINALIZE",term:"UNKNOWN"));Assert.Equal(51004,e.Number);}
 [AcademicSqlFact] public async Task HoldAddedAfterPrecheckIsEnforcedAgainAtCommit()
 {
  await using var f=await Fixture.Create();await f.Policy();await f.Student();
  using(var t=await f.Check())Assert.DoesNotContain(t.AsEnumerable(),r=>(bool)r["blocking"]);
  await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  await f.Save("HOLD","H1",new{student="SV2024002",scope="REGISTER",authority="Dao tao",reason="Ho so thay doi sau precheck",expires="2099-12-31",active=true});await f.Student();
  var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_HocVu_DangKyNhom N'[\"LHP0001\"]';"));Assert.Equal(52610,e.Number);Assert.Contains("Khóa",e.Message);
 }
 [AcademicSqlFact] public async Task CorrectedGradeAfterPrecheckIsEnforcedAgainAtCommit()
 {
  await using var f=await Fixture.Create();await f.Program();await f.Save("REQUIREMENT","R1",new{program="CT1",course="IT101",group=1,requiredCourse="IT102",kind="PRE",minGrade=5});await f.Assign();await f.Policy();
  await f.Sql("UPDATE dbo.HOC_KY SET ngay_hoc_bd='2025-01-01',ngay_hoc_kt='2025-05-01' WHERE ma_hk='HK2-2627';");
  await f.Save("RESULT","G1",new{student="SV2024002",course="IT102",term="HK2-2627",attempt=1,grade=7,passed=true,source="Bang diem",publication="2026-01-01",reason="Nhap"});await f.Student();
  using(var t=await f.Check())Assert.DoesNotContain(t.AsEnumerable(),r=>(bool)r["blocking"]);
  await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  await f.Save("RESULT","G1",new{student="SV2024002",course="IT102",term="HK2-2627",attempt=1,grade=3,passed=false,source="QD dieu chinh",publication="2026-01-01",reason="Sua diem chinh thuc"},true);await f.Student();
  var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_HocVu_DangKyNhom N'[\"LHP0001\"]';"));Assert.Equal(52610,e.Number);Assert.Contains("thiếu điều kiện",e.Message);
 }
 [AcademicSqlFact] public async Task SecondClassWriteFailureRollsBackTheEntireGroup()
 {
  var cs=Environment.GetEnvironmentVariable("ACADEMIC_TEST_CONNECTION")!;
  await using var initial=new SqlConnection(cs);await initial.OpenAsync();
  using var count=new SqlCommand("SELECT COUNT(*) FROM dbo.DANG_KY;",initial);int original=Convert.ToInt32(await count.ExecuteScalarAsync());
  await using var f=await Fixture.Create();await f.Policy();
  await f.Sql("CREATE TRIGGER dbo.tr_AcademicAtomicFailure ON dbo.DANG_KY AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ma_lhp='LHP0002') THROW 59991,N'Injected second-class failure',1; END;");
  await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_HocVu_DangKyNhom N'[\"LHP0001\",\"LHP0002\"]';"));Assert.Equal(59991,e.Number);
  Assert.Equal(original,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.DANG_KY;")));
 }
}
