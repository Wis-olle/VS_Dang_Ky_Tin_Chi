using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Academics;
namespace QuanLyGiangVien.Data.Sql;
public sealed record AcademicPlanningSnapshot(DataSet Data,string Student,string? AssignedProgram,string? SelectedProgram,string SourceHash,
 bool IsWhatIf,DegreeCurriculum? Curriculum,ImmutableArray<DegreeEvidence> Evidence,ImmutableArray<AcademicPlanningTerm> Terms,
 ImmutableArray<AcademicPlanningRule> Rules,ImmutableArray<AcademicOffering> Offerings,ImmutableArray<DegreeSubstitution> Substitutions,
 ImmutableArray<AcademicPlanItem> Items,string? PlanVersion):IDisposable
{
 public DegreeAuditResult Audit(IEnumerable<AcademicPlanItem>? items=null)=>DegreeAudit.Calculate(Curriculum,Evidence.Concat((items??Items).Select(i=>
  new DegreeEvidence(i.Course,ActualCredits(i.Course),"PLANNED",Term:i.Term))),Substitutions);
 private int ActualCredits(string course)=>Data.Tables["CourseChoices"]!.Rows.Cast<DataRow>().Where(r=>Text(r,"course")==course)
  .Select(r=>Convert.ToInt32(r["credits"])).Cast<int?>().FirstOrDefault()??throw new PersistenceException("Chưa có số tín chỉ chính thức cho môn trong kế hoạch.");
 public ImmutableArray<AcademicPlanningIssue> Check(IEnumerable<AcademicPlanItem>? items=null)=>AcademicPlanning.Check(items??Items,Terms,Rules,Evidence,Offerings,Curriculum?.Courses??[],Substitutions);
 public void Dispose()=>Data.Dispose();
 internal static string Text(DataRow row,string key)=>row[key] is DBNull?"":Convert.ToString(row[key])!;
}
public sealed class AcademicPlanningRepository(string connectionString)
{
 private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true};
 public async Task<AcademicPlanningSnapshot> SnapshotAsync(string? student=null,string? whatIfProgram=null)
 {
  var data=await Run("Snapshot",["Identity","Groups","Courses","Requirements","Evidence","Substitutions","Offerings","Terms","Plan","Comments","Programs","CourseChoices"],
   P("@student",SqlDbType.VarChar,student is null?null:AcademicValidation.Code(student),20),P("@what_if_program",SqlDbType.VarChar,whatIfProgram is null?null:AcademicValidation.Code(whatIfProgram),20));
  try{return Read(data);}catch{data.Dispose();throw;}
 }
 public static AcademicPlanningSnapshot Read(DataSet data)
 {
  try{
   if(data.Tables.Count!=12||data.Tables["Identity"]!.Rows.Count!=1)throw new InvalidOperationException();
   var id=data.Tables["Identity"]!.Rows[0];
   static string T(DataRow r,string n)=>AcademicPlanningSnapshot.Text(r,n);
   static IEnumerable<DataRow> Rows(DataSet d,string n)=>d.Tables[n]!.Rows.Cast<DataRow>();
   var courses=Rows(data,"Courses").Select(r=>new DegreeCourse(T(r,"id"),T(r,"course"),r["group"] is DBNull?null:T(r,"group"),Convert.ToInt32(r["credits"]),Convert.ToBoolean(r["required"]),r["equivalence"] is DBNull?null:T(r,"equivalence"))).ToImmutableArray();
   var curriculum=Convert.ToBoolean(id["missing_program"])?null:new DegreeCurriculum(T(id,"selected_program"),Convert.ToHexString((byte[])id["program_version"]),Convert.ToInt32(id["required_credits"]),true,
    Rows(data,"Groups").Select(r=>new DegreeGroup(T(r,"id"),T(r,"name"),Convert.ToInt32(r["requiredCredits"]))).ToImmutableArray(),courses);
   var plan=Rows(data,"Plan").FirstOrDefault();
   return new(data,T(id,"student"),id["assigned_program"] is DBNull?null:T(id,"assigned_program"),id["selected_program"] is DBNull?null:T(id,"selected_program"),Convert.ToHexString((byte[])id["source_hash"]),Convert.ToBoolean(id["is_what_if"]),curriculum,
    Rows(data,"Evidence").Select(r=>new DegreeEvidence(T(r,"course"),Convert.ToInt32(r["credits"]),T(r,"stage"),r["grade"] is DBNull?null:Convert.ToDecimal(r["grade"]),Convert.ToBoolean(r["passed"]),T(r,"term"))).ToImmutableArray(),
    Rows(data,"Terms").Select(r=>new AcademicPlanningTerm(T(r,"ma_hk"),DateOnly.FromDateTime(Convert.ToDateTime(r["start_date"])),DateOnly.FromDateTime(Convert.ToDateTime(r["end_date"])))).ToImmutableArray(),
    Rows(data,"Requirements").Select(r=>new AcademicPlanningRule(T(r,"course"),Convert.ToInt32(r["group"]),T(r,"requiredCourse"),T(r,"kind"),Convert.ToDecimal(r["minGrade"]))).ToImmutableArray(),
    Rows(data,"Offerings").Select(r=>new AcademicOffering(T(r,"course"),T(r,"term"))).ToImmutableArray(),
    Rows(data,"Substitutions").Select(r=>new DegreeSubstitution(T(r,"source_course"),T(r,"target_course"))).ToImmutableArray(),
    plan is null?[]:(JsonSerializer.Deserialize<AcademicPlanItem[]>(T(plan,"items_json"),Json)??[]).ToImmutableArray(),plan is null?null:Convert.ToHexString((byte[])plan["version"]));
  }catch(Exception e)when(e is ArgumentException or InvalidOperationException or InvalidCastException or FormatException or JsonException or IndexOutOfRangeException){throw new PersistenceException("Dữ liệu kế hoạch/CTĐT chưa tương thích. Hãy tải lại và kiểm tra dữ liệu chính thức.");}
 }
 public Task<DataSet> DirectoryAsync()=>Run("Directory",["Students","Advisors","Assignments"]);
 public static SqlCommand SaveCommand(AcademicPlanDraft draft)
 {
  AcademicPlanningValidation.Draft(draft);
  return Command("SavePlan",P("@program",SqlDbType.VarChar,AcademicValidation.Code(draft.Program),20),Token("@source_hash",draft.SourceHash,32),
   P("@items_json",SqlDbType.NVarChar,JsonSerializer.Serialize(draft.Items,Json),-1),Token("@version",draft.Version,8));
 }
 public async Task SavePlanAsync(AcademicPlanDraft draft){using var result=await Run(SaveCommand(draft),[]);}
 public async Task SubmitPlanAsync(string student,string version,string sourceHash){using var result=await Run("Submit",[],P("@student",SqlDbType.VarChar,AcademicValidation.Code(student),20),Token("@version",version,8),Token("@source_hash",sourceHash,32));}
 public async Task AdviseAsync(string student,string version,string action,string comment)
 {
  if(action is not("COMMENT" or "APPROVE" or "RETURN"))throw new ArgumentException("Thao tác cố vấn không hợp lệ.");AcademicPlanningValidation.Reason(comment);
  using var result=await Run("Advise",[],P("@student",SqlDbType.VarChar,AcademicValidation.Code(student),20),Token("@version",version,8),P("@action",SqlDbType.VarChar,action,12),P("@comment",SqlDbType.NVarChar,comment,500));
 }
 public async Task AssignAdvisorAsync(string student,int accountId,bool active,string reason)
 {
  AcademicPlanningValidation.Reason(reason);if(accountId<=0)throw new ArgumentException("Chọn tài khoản cố vấn.");
  using var result=await Run("AssignAdvisor",[],P("@student",SqlDbType.VarChar,AcademicValidation.Code(student),20),P("@account_id",SqlDbType.Int,accountId),P("@active",SqlDbType.Bit,active),P("@reason",SqlDbType.NVarChar,reason,500));
 }
 public async Task SaveOfferingAsync(string course,string term,string source,string? version=null)
 {
  AcademicPlanningValidation.Reason(source);using var result=await Run("SaveOffering",[],P("@course",SqlDbType.VarChar,AcademicValidation.Code(course),20),P("@term",SqlDbType.VarChar,AcademicValidation.Code(term),20),P("@source",SqlDbType.NVarChar,source,500),Token("@version",version,8));
 }
 public async Task SaveSubstitutionAsync(string program,string sourceCourse,string targetCourse,string source,string? version=null)
 {
  AcademicPlanningValidation.Reason(source);using var result=await Run("SaveSubstitution",[],P("@program",SqlDbType.VarChar,AcademicValidation.Code(program),20),P("@source_course",SqlDbType.VarChar,AcademicValidation.Code(sourceCourse),20),P("@target_course",SqlDbType.VarChar,AcademicValidation.Code(targetCourse),20),P("@source",SqlDbType.NVarChar,source,500),Token("@version",version,8));
 }
 private Task<DataSet> Run(string name,string[] names,params SqlParameter[] parameters)=>Run(Command(name,parameters),names);
 private async Task<DataSet> Run(SqlCommand command,string[] names)
 {
  using(command)try{
   using var cn=new SqlConnection(connectionString);await AppSqlConnection.OpenAsync(cn).ConfigureAwait(false);command.Connection=cn;
   using var reader=await command.ExecuteReaderAsync().ConfigureAwait(false);var data=new DataSet();int index=0;
   do{if(reader.FieldCount==0)continue;var table=new DataTable(index<names.Length?names[index]:"Table"+index);
    for(int i=0;i<reader.FieldCount;i++)table.Columns.Add(reader.GetName(i),reader.GetFieldType(i));
    while(await reader.ReadAsync().ConfigureAwait(false)){var values=new object[reader.FieldCount];reader.GetValues(values);table.Rows.Add(values);}data.Tables.Add(table);index++;
   }while(await reader.NextResultAsync().ConfigureAwait(false));return data;
  }catch(SqlException e){throw new PersistenceException(e.Number is >=53100 and <=53199?e.Message:SqlErrorMessages.ForNumber(e.Number),e.Number switch{53101=>PersistenceFailure.DataConflict,51001 or 52405=>PersistenceFailure.SessionExpired,>=53100 and <=53199 or 52801=>PersistenceFailure.BusinessRejected,_=>PersistenceFailure.General});}
  catch(InvalidOperationException){throw new PersistenceException("Không thể đọc hoặc lưu kế hoạch. Hãy kiểm tra kết nối và tải lại.");}
 }
 private static SqlCommand Command(string name,params SqlParameter[] ps){var command=new SqlCommand("dbo.usp_AP4_"+name){CommandType=CommandType.StoredProcedure,CommandTimeout=30};command.Parameters.AddRange(ps);return command;}
 private static SqlParameter P(string name,SqlDbType type,object? value,int size=0)=>new(name,type,size){Value=value??DBNull.Value};
 private static SqlParameter Token(string name,string? token,int bytes){if(token is not null)AcademicPlanningValidation.Token(token,bytes*2);return P(name,SqlDbType.Binary,token is null?null:Convert.FromHexString(token),bytes);}
}
