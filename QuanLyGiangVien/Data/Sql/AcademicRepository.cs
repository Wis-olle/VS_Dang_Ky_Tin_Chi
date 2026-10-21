using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Academics;
namespace QuanLyGiangVien.Data.Sql;

public sealed class AcademicRepository(string connectionString)
{
 public static SqlCommand SaveCommand(AcademicChange change)
 {
  AcademicValidation.Change(change);
  return Command("Save",P("@kind",SqlDbType.VarChar,change.Kind,20),P("@id",SqlDbType.VarChar,AcademicValidation.Code(change.Id),20),
   P("@version",SqlDbType.Binary,change.Version is null?null:Convert.FromHexString(change.Version),8),P("@payload",SqlDbType.NVarChar,change.Payload,-1));
 }
 public Task SaveAsync(AcademicChange change) => ExecuteNoResultAsync(SaveCommand(change));
 public Task PublishProgramAsync(string id,string version)=>Publish("PROGRAM",id,version);
 public Task PublishPolicyAsync(string id,string version)=>Publish("POLICY",id,version);
 private Task Publish(string kind,string id,string version)
 {
  AcademicValidation.Change(new(kind,id,version,"{}"));
  return ExecuteNoResultAsync(Command("Publish",P("@kind",SqlDbType.VarChar,kind,20),P("@id",SqlDbType.VarChar,AcademicValidation.Code(id),20),P("@version",SqlDbType.Binary,Convert.FromHexString(version),8)));
 }
 public Task<DataSet> SnapshotAsync(string? studentId=null)=>RunAsync(Command("Snapshot",P("@student",SqlDbType.VarChar,studentId is null?null:AcademicValidation.Code(studentId),20)),
  ["Programs","Groups","Courses","Requirements","Assignments","Results","ResultHistory","Policies","Overrides","Holds","Standing","Students","CourseChoices","TermChoices","MajorChoices","DegreeAudit","Summary","GroupProgress"]);
 public async Task<DataTable> PrecheckAsync(string[] classIds,string operation="REGISTER",long? registrationId=null,string? semesterId=null)
 {
  if(operation is not ("REGISTER" or "CANCEL" or "FINALIZE")) throw new ArgumentException("Thao tác học vụ không hợp lệ.");
  if(operation=="CANCEL" && registrationId is not >0) throw new ArgumentException("Chọn đăng ký cần hủy.");
  var codes=operation=="REGISTER"?AcademicValidation.Classes(classIds):classIds.Select(AcademicValidation.Code).ToArray();
  var data=await RunAsync(Command("Precheck",P("@lop_json",SqlDbType.NVarChar,JsonSerializer.Serialize(codes),-1),P("@thao_tac",SqlDbType.VarChar,operation,20),P("@ma_dk",SqlDbType.BigInt,registrationId),P("@ma_hk",SqlDbType.VarChar,semesterId is null?null:AcademicValidation.Code(semesterId),20)),["Issues"]);
  return data.Tables[0];
 }
 public async Task<DataTable> RegisterBatchAsync(string[] classIds)
 {
  var data=await RunAsync(Command("DangKyNhom",P("@lop_json",SqlDbType.NVarChar,JsonSerializer.Serialize(AcademicValidation.Classes(classIds)),-1)),["Results"]); return data.Tables[0];
 }
 private async Task ExecuteNoResultAsync(SqlCommand command){ using var result=await RunAsync(command,[]); }
 private async Task<DataSet> RunAsync(SqlCommand command,string[] names)
 {
  using(command)
  try
  {
   await using var connection=new SqlConnection(connectionString); await AppSqlConnection.OpenAsync(connection).ConfigureAwait(false); command.Connection=connection;
   using var reader=await command.ExecuteReaderAsync().ConfigureAwait(false); var data=new DataSet(); int index=0;
   do { if(reader.FieldCount==0) continue; var table=new DataTable(index<names.Length?names[index]:"Table"+index);
    for(int i=0;i<reader.FieldCount;i++) table.Columns.Add(reader.GetName(i),reader.GetFieldType(i));
    while(await reader.ReadAsync().ConfigureAwait(false)){var values=new object[reader.FieldCount];reader.GetValues(values);table.Rows.Add(values);} data.Tables.Add(table); index++;
   } while(await reader.NextResultAsync().ConfigureAwait(false)); return data;
  }
  catch(SqlException e){throw new PersistenceException(e.Number is >=52600 and <=52699?e.Message:SqlErrorMessages.ForNumber(e.Number),e.Number==52604?PersistenceFailure.DataConflict:PersistenceFailure.General);}
  catch(InvalidOperationException){throw new PersistenceException("Không thể truy cập dữ liệu học vụ. Hãy tải lại.");}
 }
 private static SqlCommand Command(string name,params SqlParameter[] parameters){var command=new SqlCommand("dbo.usp_HocVu_"+name){CommandType=CommandType.StoredProcedure,CommandTimeout=30}; command.Parameters.AddRange(parameters);return command;}
 private static SqlParameter P(string name,SqlDbType type,object? value,int size=0)=>new(name,type,size){Value=value??DBNull.Value};
}
