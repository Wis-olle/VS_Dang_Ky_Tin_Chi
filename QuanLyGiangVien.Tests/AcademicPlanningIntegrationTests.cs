using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using Xunit;
namespace QuanLyGiangVien.Tests;
[Collection("Shared SQL fixture")]
public sealed class AcademicPlanningIntegrationTests
{
 private static string Cs {get{var cs=Environment.GetEnvironmentVariable("ACADEMIC4_TEST_CONNECTION")!;Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(cs).InitialCatalog);return cs;}}
 private sealed class Fixture:IAsyncDisposable
 {
  internal SqlConnection C {get;}private Fixture(SqlConnection c)=>C=c;
  internal static async Task<Fixture> Open(){var c=new SqlConnection(Cs);await c.OpenAsync();var f=new Fixture(c);await f.Sql("BEGIN TRAN; INSERT dbo.CAMPUS_PERMISSION SELECT dbo.ufn_Campus_AccountId(),p.permission FROM (VALUES('ADVISE_PLAN'),('CLASS_OPERATE'),('PROFILE_EDIT'))p(permission) WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION old WHERE old.account_id=dbo.ufn_Campus_AccountId() AND old.permission=p.permission);");return f;}
  internal async Task Sql(string text){using var command=new SqlCommand(text,C){CommandTimeout=60};await command.ExecuteNonQueryAsync();}
  internal async Task<object?> Scalar(string text){using var command=new SqlCommand(text,C);return await command.ExecuteScalarAsync();}
  internal Task Student()=>Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  internal Task Staff()=>Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();");
  internal async Task Program(bool prerequisite=false)
  {
   await Sql("EXEC dbo.usp_HocVu_Save 'PROGRAM','AP4_CT',NULL,N'{\"name\":\"CTĐT kiểm thử có nguồn\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":6}'; EXEC dbo.usp_HocVu_Save 'GROUP','AP4_G1',NULL,N'{\"program\":\"AP4_CT\",\"name\":\"Cơ sở\",\"requiredCredits\":3}'; EXEC dbo.usp_HocVu_Save 'GROUP','AP4_G2',NULL,N'{\"program\":\"AP4_CT\",\"name\":\"Chuyên ngành\",\"requiredCredits\":3}'; EXEC dbo.usp_HocVu_Save 'COURSE','AP4_C1',NULL,N'{\"program\":\"AP4_CT\",\"course\":\"IT101\",\"group\":\"AP4_G1\",\"required\":true}'; EXEC dbo.usp_HocVu_Save 'COURSE','AP4_C2',NULL,N'{\"program\":\"AP4_CT\",\"course\":\"IT102\",\"group\":\"AP4_G2\",\"required\":true}';");
   if(prerequisite)await Sql("EXEC dbo.usp_HocVu_Save 'REQUIREMENT','AP4_RULE',NULL,N'{\"program\":\"AP4_CT\",\"course\":\"IT102\",\"group\":1,\"requiredCourse\":\"IT101\",\"kind\":\"PRE\",\"minGrade\":5}';");
   await Sql("DECLARE @v binary(8)=(SELECT version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='AP4_CT'); EXEC dbo.usp_HocVu_Publish 'PROGRAM','AP4_CT',@v; EXEC dbo.usp_HocVu_Save 'ASSIGNMENT','AP4_ASSIGN',NULL,N'{\"student\":\"SV2024002\",\"program\":\"AP4_CT\"}'; INSERT dbo.AP4_ADVISOR(student,account_id,active) VALUES('SV2024002',dbo.ufn_Campus_AccountId(),1); EXEC dbo.usp_AP4_SaveOffering 'IT101','HK1-2627',N'Dự kiến theo quyết định fixture'; EXEC dbo.usp_AP4_SaveOffering 'IT101','HK2-2627',N'Dự kiến theo quyết định fixture'; EXEC dbo.usp_AP4_SaveOffering 'IT102','HK2-2627',N'Dự kiến theo quyết định fixture';");
  }
  internal Task Save(string json="[{\"course\":\"IT101\",\"term\":\"HK2-2627\"}]")=>Sql("DECLARE @hash binary(32)=dbo.ufn_AP4_SourceHash('SV2024002','AP4_CT'); EXEC dbo.usp_AP4_SavePlan 'AP4_CT',@hash,N'"+json+"';");
  internal Task Submit()=>Sql("DECLARE @v binary(8)=(SELECT version FROM dbo.AP4_PLAN WHERE student='SV2024002'),@h binary(32)=dbo.ufn_AP4_SourceHash('SV2024002','AP4_CT'); EXEC dbo.usp_AP4_Submit 'SV2024002',@v,@h;");
  internal async Task<AcademicPlanningSnapshot> Read(string? whatIf=null){using var command=new SqlCommand("dbo.usp_AP4_Snapshot",C){CommandType=CommandType.StoredProcedure};command.Parameters.AddWithValue("@student","SV2024002");if(whatIf is not null)command.Parameters.AddWithValue("@what_if_program",whatIf);using var adapter=new SqlDataAdapter(command);var data=new DataSet();adapter.Fill(data);var names=new[]{"Identity","Groups","Courses","Requirements","Evidence","Substitutions","Offerings","Terms","Plan","Comments","Programs","CourseChoices"};for(int i=0;i<data.Tables.Count;i++)data.Tables[i].TableName=names[i];return AcademicPlanningRepository.Read(data);}
  public async ValueTask DisposeAsync(){await Sql("IF @@TRANCOUNT>0 ROLLBACK;");await C.DisposeAsync();}
 }
 [Academic4Fact] public async Task MissingOfficialCurriculumReturnsExplicitEmptyAudit(){await using var f=await Fixture.Open();await f.Student();using var snapshot=await f.Read();Assert.Null(snapshot.Curriculum);Assert.Contains("MISSING_PROGRAM",snapshot.Audit().Issues);}
 [Academic4Fact] public Task PublishedZeroCreditGroupRemainsReadable()=>CheckPublishedGroupAudit(0);
 [Academic4Fact] public Task PublishedGroupMinimumDoesNotDiscardRequiredCourseCredits()=>CheckPublishedGroupAudit(3);
 private static async Task CheckPublishedGroupAudit(int groupMinimum)
 {
  await using var f=await Fixture.Open();
  await f.Sql("EXEC dbo.usp_HocVu_Save 'PROGRAM','AUD20_CT',NULL,N'{\"name\":\"CTĐT fixture audit 20\",\"major\":\"CNTT\",\"cohort\":2024,\"requiredCredits\":6}';"+
   "EXEC dbo.usp_HocVu_Save 'GROUP','AUD20_G',NULL,N'{\"program\":\"AUD20_CT\",\"name\":\"Nhóm ngưỡng tối thiểu\",\"requiredCredits\":"+groupMinimum+"}';"+
   "EXEC dbo.usp_HocVu_Save 'COURSE','AUD20_A',NULL,N'{\"program\":\"AUD20_CT\",\"course\":\"IT101\",\"group\":\"AUD20_G\",\"required\":true}';"+
   "EXEC dbo.usp_HocVu_Save 'COURSE','AUD20_B',NULL,N'{\"program\":\"AUD20_CT\",\"course\":\"IT102\",\"group\":\"AUD20_G\",\"required\":true}';"+
   "DECLARE @v binary(8)=(SELECT version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='AUD20_CT'); EXEC dbo.usp_HocVu_Publish 'PROGRAM','AUD20_CT',@v;"+
   "EXEC dbo.usp_HocVu_Save 'ASSIGNMENT','AUD20_AS',NULL,N'{\"student\":\"SV2024002\",\"program\":\"AUD20_CT\"}';"+
   "UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN();"+
   "EXEC dbo.usp_HocVu_Save 'RESULT','AUD20_R1',NULL,N'{\"student\":\"SV2024002\",\"course\":\"IT101\",\"term\":\"HK1-2627\",\"attempt\":1,\"recognition\":\"GRADED\",\"grade\":8,\"passed\":true,\"publication\":\"2026-10-01\",\"source\":\"Nguồn fixture riêng\",\"reason\":\"Đối soát nhóm tín chỉ\"}';"+
   "EXEC dbo.usp_HocVu_Save 'RESULT','AUD20_R2',NULL,N'{\"student\":\"SV2024002\",\"course\":\"IT102\",\"term\":\"HK1-2627\",\"attempt\":1,\"recognition\":\"GRADED\",\"grade\":8,\"passed\":true,\"publication\":\"2026-10-01\",\"source\":\"Nguồn fixture riêng\",\"reason\":\"Đối soát nhóm tín chỉ\"}';");
  await f.Student(); using var snapshot=await f.Read();
  Assert.True(snapshot.Curriculum!.Published);
  var result=snapshot.Audit(); Assert.True(result.Satisfied); Assert.Equal(6,result.AllocatedDone);
 }
 [Academic4Fact] public async Task PublishedRequirementCreditsAndNamesSurviveCatalogChanges()
 {
  await using var f=await Fixture.Open();await f.Program();
  var originalName=await f.Scalar("SELECT ten_hp FROM dbo.HOC_PHAN WHERE ma_hp='IT101';");
  await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); EXEC dbo.usp_HocVu_Save 'RESULT','AP4_R1',NULL,N'{\"student\":\"SV2024002\",\"course\":\"IT101\",\"term\":\"HK1-2627\",\"attempt\":1,\"recognition\":\"GRADED\",\"grade\":8,\"passed\":true,\"publication\":\"2026-10-01\",\"source\":\"Kết quả fixture có nguồn\",\"reason\":\"Kiểm snapshot bất biến\"}'; EXEC dbo.usp_HocVu_Save 'RESULT','AP4_R2',NULL,N'{\"student\":\"SV2024002\",\"course\":\"IT102\",\"term\":\"HK1-2627\",\"attempt\":1,\"recognition\":\"GRADED\",\"grade\":8,\"passed\":true,\"publication\":\"2026-10-01\",\"source\":\"Kết quả fixture có nguồn\",\"reason\":\"Kiểm snapshot bất biến\"}'; UPDATE dbo.HOC_PHAN SET so_tin_chi=4,ten_hp=N'Tên danh mục đã thay đổi' WHERE ma_hp='IT101';");
  await f.Student();using var snapshot=await f.Read();
  Assert.All(snapshot.Curriculum!.Courses,c=>Assert.Equal(3,c.Credits));
  Assert.Equal(originalName,snapshot.Data.Tables["Courses"]!.Rows.Cast<DataRow>().Single(r=>(string)r["course"]=="IT101")["name"]);
  Assert.True(snapshot.Audit().Satisfied);Assert.Equal(6,snapshot.Audit().AllocatedDone);
 }
 [Academic4Fact] public async Task StudentCannotReadAnotherStudent(){await using var f=await Fixture.Open();await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_AP4_Snapshot 'SV2024001';"));Assert.Equal(53103,e.Number);}
 [Academic4Fact] public async Task AdvisorNeedsPermissionAndExplicitAssignment(){await using var f=await Fixture.Open();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_AP4_Snapshot 'SV2024002';"));Assert.Equal(53103,e.Number);}
 [Academic4Fact] public async Task UnpublishedProgramCannotBeSaved(){await using var f=await Fixture.Open();await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save());Assert.Equal(53104,e.Number);}
 [Academic4Fact] public async Task PlanPersistsAcrossReloadsAndWhatIfIsReadOnly(){await using var f=await Fixture.Open();await f.Program();await f.Student();await f.Save();using var saved=await f.Read();Assert.Single(saved.Items);using var simulated=await f.Read("AP4_CT");Assert.True(simulated.IsWhatIf);Assert.Equal(saved.PlanVersion,simulated.PlanVersion);Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.AP4_PLAN WHERE student='SV2024002';")));}
 [Academic4Fact] public async Task SameCourseCannotBePlannedInTwoTerms(){await using var f=await Fixture.Open();await f.Program();await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("[{\"course\":\"IT101\",\"term\":\"HK1-2627\"},{\"course\":\"IT101\",\"term\":\"HK2-2627\"}]"));Assert.Equal(53100,e.Number);}
 [Academic4Fact] public async Task DuplicateOrDifferentlyCasedJsonKeysCannotChangeWhatTheServerValidated()
 {await using var f=await Fixture.Open();await f.Program();await f.Student();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save("[{\"course\":\"IT101\",\"Course\":\"EC101\",\"term\":\"HK2-2627\"}]"));Assert.Equal(53100,e.Number);}
 [Academic4Fact] public async Task StalePlanVersionCannotOverwriteAnotherDraft(){await using var f=await Fixture.Open();await f.Program();await f.Student();await f.Save();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Save());Assert.Equal(53101,e.Number);}
 [Academic4Fact] public async Task StaleAcademicSnapshotCannotBeSubmitted(){await using var f=await Fixture.Open();await f.Program();await f.Student();await f.Save();await f.Sql("UPDATE dbo.AP4_OFFERING SET source=N'Căn cứ cập nhật fixture' WHERE course='IT101' AND term='HK2-2627';");var e=await Assert.ThrowsAsync<SqlException>(f.Submit);Assert.Equal(53101,e.Number);}
 [Academic4Fact] public async Task MissingOfferingBlocksSubmissionAndAppearsInRiskView(){await using var f=await Fixture.Open();await f.Program();await f.Sql("DELETE dbo.AP4_OFFERING WHERE course='IT101' AND term='HK2-2627';");await f.Student();await f.Save();Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT planned_students FROM dbo.v_AP4_Demand WHERE course='IT101' AND term='HK2-2627';")));Assert.True(Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.v_AP4_PlanRisks WHERE student='SV2024002';"))>0);var e=await Assert.ThrowsAsync<SqlException>(f.Submit);Assert.Equal(53106,e.Number);}
 [Academic4Fact] public async Task PrerequisiteMustPrecedeTargetSemester(){await using var f=await Fixture.Open();await f.Program(true);await f.Student();await f.Save("[{\"course\":\"IT101\",\"term\":\"HK2-2627\"},{\"course\":\"IT102\",\"term\":\"HK2-2627\"}]");Assert.True(Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.v_AP4_PlanRisks WHERE student='SV2024002' AND reason LIKE N'Thiếu nhóm%';"))>0);var e=await Assert.ThrowsAsync<SqlException>(f.Submit);Assert.Equal(53106,e.Number);}
 [Academic4Fact] public async Task SequentialFuturePrerequisitesCanBeSubmittedConditionally(){await using var f=await Fixture.Open();await f.Program(true);await f.Student();await f.Save("[{\"course\":\"IT101\",\"term\":\"HK1-2627\"},{\"course\":\"IT102\",\"term\":\"HK2-2627\"}]");using var snapshot=await f.Read();Assert.Contains(snapshot.Check(),i=>i.Code=="CONDITIONAL_RESULT");await f.Submit();Assert.Equal("SUBMITTED",await f.Scalar("SELECT status FROM dbo.AP4_PLAN WHERE student='SV2024002';"));}
 [Academic4Fact] public async Task AdvisorApprovalRequiresSubmittedFreshPlanAndProducesNotification(){await using var f=await Fixture.Open();await f.Program();await f.Student();await f.Save();await f.Submit();await f.Staff();var previousEvents=Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE event_type='PLAN_ADVISED' AND student='SV2024002';"));await f.Sql("DECLARE @v binary(8)=(SELECT version FROM dbo.AP4_PLAN WHERE student='SV2024002'); EXEC dbo.usp_AP4_Advise 'SV2024002',@v,'APPROVE',N'Đã kiểm tra chuỗi kế hoạch';");Assert.Equal("APPROVED",await f.Scalar("SELECT status FROM dbo.AP4_PLAN WHERE student='SV2024002';"));Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.AP4_COMMENT WHERE student='SV2024002';")));Assert.Equal(previousEvents+1,Convert.ToInt32(await f.Scalar("SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE event_type='PLAN_ADVISED' AND student='SV2024002';")));}
 [Academic4Fact] public async Task RevokedAdvisorMappingPreventsDecision(){await using var f=await Fixture.Open();await f.Program();await f.Student();await f.Save();await f.Submit();await f.Staff();await f.Sql("UPDATE dbo.AP4_ADVISOR SET active=0 WHERE student='SV2024002';");var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("DECLARE @v binary(8)=(SELECT version FROM dbo.AP4_PLAN WHERE student='SV2024002'); EXEC dbo.usp_AP4_Advise 'SV2024002',@v,'APPROVE',N'Không còn phân công';"));Assert.Equal(53103,e.Number);}
 [Academic4Fact] public async Task SubstitutionIsOneWayAndRequiresAdminConfirmation(){await using var f=await Fixture.Open();await f.Program();var e=await Assert.ThrowsAsync<SqlException>(()=>f.Sql("EXEC dbo.usp_AP4_SaveSubstitution 'AP4_CT','EN101','IT101',N'Quyết định fixture xác nhận';"));Assert.Equal(53103,e.Number);}
 [Academic4Fact] public async Task RepositoryRoundTripUsesRealSourcesAndScopedAdvisorWorkflow()
 {
  await using var f=await Fixture.Open();var repository=new AcademicPlanningRepository(Cs);
  try{
   await f.Program();await f.Sql("COMMIT;");
   using(var directory=await repository.DirectoryAsync())Assert.Single(directory.Tables["Students"]!.Rows.Cast<DataRow>());
   using(var initial=await repository.SnapshotAsync("SV2024002")){
    var row=initial.Data.Tables["Offerings"]!.Rows.Cast<DataRow>().Single(r=>(string)r["course"]=="IT101"&&(string)r["term"]=="HK2-2627");
    await repository.SaveOfferingAsync("IT101","HK2-2627","Quyết định mở môn cập nhật",Convert.ToHexString((byte[])row["version"]));
   }
   await f.Sql("UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai) VALUES(N'ap4-api-advisor','DAO_TAO',1);");
   int advisorId=Convert.ToInt32(await f.Scalar("SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=N'ap4-api-advisor';"));
   await repository.AssignAdvisorAsync("SV2024002",advisorId,true,"Phân công cố vấn theo quyết định fixture");
   await repository.SaveSubstitutionAsync("AP4_CT","EN101","IT101","Quyết định thay thế một chiều fixture");
   Assert.Equal(1,Convert.ToInt32(await f.Scalar("SELECT CONVERT(int,dbo.ufn_AP4_Matches('AP4_CT','EN101','IT101'));")));
   Assert.Equal(0,Convert.ToInt32(await f.Scalar("SELECT CONVERT(int,dbo.ufn_AP4_Matches('AP4_CT','IT101','EN101'));")));
   using(var adminDirectory=await repository.DirectoryAsync())Assert.NotEmpty(adminDirectory.Tables["Advisors"]!.Rows.Cast<DataRow>());
   await f.Student();
   using(var initial=await repository.SnapshotAsync())await repository.SavePlanAsync(new("AP4_CT",initial.SourceHash,null,[new("IT101","HK2-2627")]));
   using(var saved=await repository.SnapshotAsync()){
    Assert.Single(saved.Items);Assert.Single(saved.Audit().Courses.Where(r=>r.Planned==3));
    Assert.DoesNotContain(saved.Check(),i=>i.Blocking);await repository.SubmitPlanAsync(saved.Student,saved.PlanVersion!,saved.SourceHash);
   }
   await f.Staff();using(var submitted=await repository.SnapshotAsync("SV2024002")){
    await repository.AdviseAsync(submitted.Student,submitted.PlanVersion!,"COMMENT","Đề nghị kiểm tra lịch mở môn");
    await repository.AdviseAsync(submitted.Student,submitted.PlanVersion!,"RETURN","Cần cập nhật nội dung kế hoạch");
   }
   await f.Student();using(var returned=await repository.SnapshotAsync())await repository.SavePlanAsync(new("AP4_CT",returned.SourceHash,returned.PlanVersion,returned.Items));
   using(var draft=await repository.SnapshotAsync())await repository.SubmitPlanAsync(draft.Student,draft.PlanVersion!,draft.SourceHash);
   await f.Staff();using(var resubmitted=await repository.SnapshotAsync("SV2024002"))await repository.AdviseAsync(resubmitted.Student,resubmitted.PlanVersion!,"APPROVE","Đã đối soát kế hoạch có căn cứ");
   using(var approved=await repository.SnapshotAsync("SV2024002")){Assert.True(Convert.ToBoolean(approved.Data.Tables["Plan"]!.Rows[0]["approval_current"]));Assert.Equal(3,approved.Data.Tables["Comments"]!.Rows.Count);}
   await f.Student();using(var simulation=await repository.SnapshotAsync(whatIfProgram:"AP4_CT"))Assert.True(simulation.IsWhatIf);
  }finally{
   await f.Sql("IF @@TRANCOUNT>0 ROLLBACK; UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.AP4_COMMENT WHERE student='SV2024002'; DELETE dbo.AP4_PLAN WHERE student='SV2024002'; DELETE dbo.AP4_ADVISOR WHERE student='SV2024002'; DELETE dbo.AP4_SUBSTITUTION WHERE program_id='AP4_CT'; DELETE dbo.AP4_OFFERING WHERE course IN('IT101','IT102'); DELETE dbo.HV_HISTORY WHERE id LIKE 'AP4[_]%'; DELETE dbo.HV_DOCUMENT WHERE id LIKE 'AP4[_]%'; DELETE dbo.TAI_KHOAN WHERE ten_dang_nhap=N'ap4-api-advisor'; DELETE dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission IN('ADVISE_PLAN','CLASS_OPERATE','PROFILE_EDIT');");
  }
 }
}
public sealed class Academic4FactAttribute:FactAttribute{public Academic4FactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ACADEMIC4_TEST_CONNECTION")))Skip="Requires isolated academic phase-four fixture.";}}
