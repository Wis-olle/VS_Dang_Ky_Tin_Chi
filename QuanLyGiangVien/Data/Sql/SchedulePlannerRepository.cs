using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;
namespace QuanLyGiangVien.Data.Sql;

public sealed class SchedulePlannerRepository
{
 private readonly string connectionString;
 public SchedulePlannerRepository(string setting)
 {
  try{var b=new SqlConnectionStringBuilder(setting);if(string.IsNullOrWhiteSpace(b.DataSource)||string.IsNullOrWhiteSpace(b.InitialCatalog))throw new ArgumentException();b.ConnectTimeout=Math.Clamp(b.ConnectTimeout,1,15);b.PersistSecurityInfo=false;connectionString=b.ConnectionString;}
  catch(ArgumentException){throw new PersistenceException("Cấu hình kết nối bộ sinh lịch không hợp lệ.");}
 }
 public async Task<SchedulePlannerSnapshot> LoadAsync(string term,CancellationToken token=default)
 {
  using var command=Command("dbo.usp_Planner_Snapshot",Code("@term",term));
  var tables=await ReadAsync(command,token).ConfigureAwait(false);
  if(tables.Length!=4||tables[3].Rows.Count!=1)throw new PersistenceException("Không đọc đủ dữ liệu để sinh thời khóa biểu. Hãy tải lại.");
  try{
   var meetings=tables[2].Rows.Cast<DataRow>().Select(r=>new SchedulePlannerMeeting((string)r["class_id"],DateOnly.FromDateTime((DateTime)r["meeting_date"]),(int)r["start_period"],(int)r["end_period"])).ToLookup(m=>m.ClassId);
   var classes=tables[0].Rows.Cast<DataRow>().Select(r=>new SchedulePlannerClass((string)r["class_id"],(string)r["course_id"],(string)r["course_name"],(int)r["credits"],(string)r["term"],(bool)r["can_add"],(bool)r["is_registered"],(bool)r["blocks_schedule"],meetings[(string)r["class_id"]].ToImmutableArray())).ToImmutableArray();
   var bundles=tables[1].Rows.Cast<DataRow>().GroupBy(r=>(string)r["id"]).Select(g=>new SchedulePlannerBundle(g.Key,(string)g.First()["course"],g.Select(r=>(string)r["class_id"]).ToImmutableArray(),g.Select(r=>r.IsNull("component_type")?"":(string)r["component_type"]).ToImmutableArray())).ToImmutableArray();
   var cart=tables[3].Rows[0];return new(term,classes,bundles,cart.IsNull("version")?null:Convert.ToHexString((byte[])cart["version"]),JsonSerializer.Deserialize<ImmutableArray<string>>((string)cart["class_json"]));
  }finally{foreach(var table in tables)table.Dispose();}
 }
 public async Task<SchedulePlannerResult> GenerateAsync(string term,SchedulePlannerRequest request,CancellationToken token=default)
 {
  SchedulePlanner.Validate(request);var snapshot=await LoadAsync(term,token).ConfigureAwait(false);
  var timer=Stopwatch.StartNew();var candidates=await Task.Run(()=>SchedulePlanner.GenerateCandidates(snapshot,request,token),token).ConfigureAwait(false);
  var options=ImmutableArray.CreateBuilder<SchedulePlannerOption>();var explanations=candidates.Explanations.ToBuilder();var limited=candidates.Truncated;
  foreach(var option in candidates.Options)
  {
   token.ThrowIfCancellationRequested();var remaining=request.MaxMilliseconds-(int)timer.ElapsedMilliseconds;
   if(remaining<=0){limited=true;break;}
   using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(remaining);
   try{
    using var command=Command("dbo.usp_DK3_Precheck",Code("@term",term),Json("@add_json",RegistrationWorkflowValidation.ClassesJson(option.AddClassIds)),Json("@remove_json","[]"));
    var tables=await ReadAsync(command,budget.Token).ConfigureAwait(false);
    try{
     var checks=tables[0].Rows.Cast<DataRow>().ToArray();var blocked=checks.Where(r=>(bool)r["blocking"]).Select(r=>(string)r["message"]).ToArray();
     if(blocked.Length>0){foreach(var message in blocked.Take(3))if(!explanations.Contains(message)&&explanations.Count<20)explanations.Add(message);continue;}
     options.Add(option with{Warnings=checks.Where(r=>!(bool)r["blocking"]&&(string)r["code"]!="CREDITS").Select(r=>(string)r["message"]).Distinct().ToImmutableArray()});
     if(options.Count>=request.MaxOptions)break;
    }finally{foreach(var table in tables)table.Dispose();}
   }
   catch(OperationCanceledException)when(!token.IsCancellationRequested){limited=true;break;}
   catch(PersistenceException)when(timer.ElapsedMilliseconds>=request.MaxMilliseconds&&!token.IsCancellationRequested){limited=true;break;}
  }
  if(limited&&!explanations.Any(v=>v.Contains("giới hạn")))explanations.Add("Đã hết thời gian kiểm tra; chỉ trả các phương án đã được máy chủ kiểm tra học vụ.");
  if(options.Count==0&&!limited&&explanations.Count==0)explanations.Add("Không có phương án thỏa điều kiện học vụ hiện tại. Kiểm tra CTĐT, hold, thời hạn và số chỗ rồi thử lại.");
  return new(options.ToImmutable(),explanations.Distinct().ToImmutableArray(),candidates.ExploredNodes,limited,snapshot.CartVersion);
 }
 public async Task<DataTable> AddToCartAsync(string term,SchedulePlannerOption option,string? expectedCartVersion,CancellationToken token=default)
 {
  ArgumentNullException.ThrowIfNull(option);if(option.AddClassIds.IsEmpty)throw new ArgumentException("Phương án này chỉ gồm các lớp đã đăng ký, không có lớp mới để thêm.");
  var version=expectedCartVersion is null?null:expectedCartVersion.Length==16&&expectedCartVersion.All(Uri.IsHexDigit)?Convert.FromHexString(expectedCartVersion):throw new ArgumentException("Phiên bản giỏ không hợp lệ. Hãy tải lại.");
  using var command=Command("dbo.usp_Planner_SaveCart",Code("@term",term),Json("@class_json",RegistrationWorkflowValidation.ClassesJson(option.AddClassIds)),new SqlParameter("@version",SqlDbType.Binary,8){Value=(object?)version??DBNull.Value});
  var tables=await ReadAsync(command,token).ConfigureAwait(false);return tables[0];
 }
 private async Task<ImmutableArray<DataTable>> ReadAsync(SqlCommand command,CancellationToken token)
 {
  try{
   await using var connection=new SqlConnection(connectionString);await AppSqlConnection.OpenAsync(connection,token).ConfigureAwait(false);command.Connection=connection;
   await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);var tables=ImmutableArray.CreateBuilder<DataTable>();
   do{
    if(reader.FieldCount==0)continue;var table=new DataTable{Locale=System.Globalization.CultureInfo.InvariantCulture};
    for(var i=0;i<reader.FieldCount;i++)table.Columns.Add(reader.GetName(i),reader.GetFieldType(i));
    while(await reader.ReadAsync(token).ConfigureAwait(false)){var values=new object[reader.FieldCount];reader.GetValues(values);table.Rows.Add(values);}tables.Add(table);
   }while(await reader.NextResultAsync(token).ConfigureAwait(false));
   return tables.ToImmutable();
  }
  catch(SqlException e){throw new PersistenceException(e.Number is >=52600 and <=53299?e.Message:e.Number==51005?"Bộ sinh lịch chỉ dành cho sinh viên.":"Không thể kiểm tra dữ liệu bộ sinh lịch. Hãy tải lại hoặc đăng nhập lại.",e.Number is >=52600 and <=53299?PersistenceFailure.BusinessRejected:PersistenceFailure.General);}
 }
 private static SqlCommand Command(string name,params SqlParameter[] parameters){var command=new SqlCommand(name){CommandType=CommandType.StoredProcedure,CommandTimeout=15};command.Parameters.AddRange(parameters);return command;}
 private static SqlParameter Code(string name,string value)=>new(name,SqlDbType.VarChar,20){Value=RegistrationValidation.RequiredCode(value)};
 private static SqlParameter Json(string name,string value)=>new(name,SqlDbType.NVarChar,-1){Value=value};
}
