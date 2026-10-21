using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;
namespace QuanLyGiangVien.Data.Sql;

/// <summary>Durable cart and atomic add/drop/swap. SQL always rechecks ownership and live conditions.</summary>
public sealed class RegistrationWorkflowRepository
{
    private readonly string connectionString;
    public RegistrationWorkflowRepository(string connectionString)
    {
        try {
            var b=new SqlConnectionStringBuilder(connectionString);
            if(string.IsNullOrWhiteSpace(b.DataSource)||string.IsNullOrWhiteSpace(b.InitialCatalog)) throw new ArgumentException();
            b.ConnectTimeout=Math.Clamp(b.ConnectTimeout,1,15);b.PersistSecurityInfo=false;this.connectionString=b.ConnectionString;
        } catch(ArgumentException) {throw new PersistenceException("Cấu hình kết nối SQL không hợp lệ.");}
    }
    public Task<DataTable> CartAsync(string term)=>Execute(RegistrationWorkflowCommands.Cart(term));
    public Task<DataTable> SaveCartAsync(string term,IEnumerable<string> classes,byte[]? version)=>Execute(RegistrationWorkflowCommands.SaveCart(term,classes,version));
    public Task<DataTable> ComponentsAsync(string term)=>Execute(RegistrationWorkflowCommands.Components(term));
    public Task<DataTable> SaveCombinationAsync(string id,string term,string course,IEnumerable<string> classes,byte[]? version,IReadOnlyDictionary<string,string>? roles=null)=>Execute(RegistrationWorkflowCommands.SaveCombination(id,term,course,classes,version,roles));
    public Task<DataTable> PrecheckAsync(string term,IEnumerable<string> add,IEnumerable<long> remove)=>Execute(RegistrationWorkflowCommands.Precheck(term,add,remove));
    public Task<DataTable> SubmitAsync(Guid requestId,string term,IEnumerable<string> add,IEnumerable<long> remove,byte[]? cartVersion=null)=>Execute(RegistrationWorkflowCommands.Submit(requestId,term,add,remove,cartVersion));
    public Task<DataTable> ResultAsync(Guid requestId)=>Execute(RegistrationWorkflowCommands.Result(requestId));
    private async Task<DataTable> Execute(SqlCommand command)
    {
        using(command) try {
            await using var connection=new SqlConnection(connectionString);await AppSqlConnection.OpenAsync(connection).ConfigureAwait(false);command.Connection=connection;
            await using var reader=await command.ExecuteReaderAsync().ConfigureAwait(false);
            var table=new DataTable{Locale=System.Globalization.CultureInfo.InvariantCulture};
            for(var i=0;i<reader.FieldCount;i++) table.Columns.Add(reader.GetName(i),reader.GetFieldType(i));
            while(await reader.ReadAsync().ConfigureAwait(false)){var row=new object[reader.FieldCount];reader.GetValues(row);table.Rows.Add(row);}
            return table;
        } catch(SqlException e){throw new PersistenceException(e.Number is >=52600 and <=52799 or 52801?e.Message:RegistrationErrorMessages.ForNumber(e.Number), e.Number is >=51002 and <=51021 or >=52600 and <=52799 or 52801 ? PersistenceFailure.BusinessRejected : PersistenceFailure.General);}
        catch(InvalidOperationException){throw new PersistenceException("Không thể xác định kết quả. Tải lại giao dịch bằng mã yêu cầu trước khi gửi yêu cầu mới.");}
    }
}
internal static class RegistrationWorkflowCommands
{
    private static SqlCommand Cmd(string name,params SqlParameter[] parameters){var c=new SqlCommand("dbo.usp_DK3_"+name){CommandType=CommandType.StoredProcedure,CommandTimeout=30};c.Parameters.AddRange(parameters);return c;}
    private static SqlParameter Code(string name,string value)=>new(name,SqlDbType.VarChar,20){Value=RegistrationValidation.RequiredCode(value)};
    private static SqlParameter Json(string name,string value)=>new(name,SqlDbType.NVarChar,-1){Value=value};
    private static SqlParameter Version(byte[]? value)=>new("@version",SqlDbType.Binary,8){Value=(object?)RegistrationWorkflowValidation.Version(value)??DBNull.Value};
    private static SqlParameter Request(Guid value)=>new("@request_id",SqlDbType.UniqueIdentifier){Value=RegistrationWorkflowValidation.Request(value)};
    public static SqlCommand Cart(string term)=>Cmd("Cart",Code("@term",term));
    public static SqlCommand SaveCart(string term,IEnumerable<string> classes,byte[]? version)=>Cmd("SaveCart",Code("@term",term),Json("@class_json",RegistrationWorkflowValidation.ClassesJson(classes)),Version(version));
    public static SqlCommand Components(string term)=>Cmd("Components",Code("@term",term));
    public static SqlCommand SaveCombination(string id,string term,string course,IEnumerable<string> classes,byte[]? version,IReadOnlyDictionary<string,string>? roles=null)
    {
        var json=RegistrationWorkflowValidation.ClassesJson(classes);
        return Cmd("SaveCombination",Code("@id",id),Code("@term",term),Code("@course",course),Json("@class_json",json),Version(version),Json("@roles_json",RegistrationWorkflowValidation.RolesJson(json,roles)));
    }
    public static SqlCommand Precheck(string term,IEnumerable<string> add,IEnumerable<long> remove)=>Cmd("Precheck",Code("@term",term),Json("@add_json",RegistrationWorkflowValidation.ClassesJson(add)),Json("@remove_json",RegistrationWorkflowValidation.RemovalsJson(remove)));
    public static SqlCommand Submit(Guid id,string term,IEnumerable<string> add,IEnumerable<long> remove,byte[]? version)
    {
        var a=RegistrationWorkflowValidation.ClassesJson(add);var r=RegistrationWorkflowValidation.RemovalsJson(remove);
        if(a=="[]"&&r=="[]") throw new ArgumentException("Chưa chọn thay đổi đăng ký.");
        return Cmd("Submit",Request(id),Code("@term",term),Json("@add_json",a),Json("@remove_json",r),Version(version));
    }
    public static SqlCommand Result(Guid id)=>Cmd("Result",Request(id));
}
