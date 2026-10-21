using Microsoft.Data.SqlClient;
using System.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;
namespace QuanLyGiangVien.Data.Sql;

public sealed class SeatAllocationRepository(string connectionString)
{
    public Task<DataSet> SnapshotAsync(string term)=>Run(Command("Snapshot",Code("@term",term)));
    public Task<DataSet> SaveAsync(SeatAllocationChange change)=>Run(SaveCommand(change));
    public Task<DataSet> JoinAsync(string term,IEnumerable<string> classes)=>Run(Command("Join",Code("@term",term),P("@class_json",SqlDbType.NVarChar,SeatAllocationValidation.Classes(classes),-1)));
    public Task<DataSet> RespondAsync(long id,string version,bool decline)=>Run(Command("Respond",P("@id",SqlDbType.BigInt,SeatAllocationValidation.Entry(id)),Token(version),P("@decline",SqlDbType.Bit,decline)));
    public Task<DataSet> ClaimAsync(long id,string version,Guid request)=>Run(Command("Claim",P("@id",SqlDbType.BigInt,SeatAllocationValidation.Entry(id)),Token(version),P("@request_id",SqlDbType.UniqueIdentifier,RegistrationWorkflowValidation.Request(request))));
    public Task<DataSet> ProcessAsync(string term,int batch=20)=>Run(Command("Process",Code("@term",term),P("@batch",SqlDbType.Int,SeatAllocationValidation.Batch(batch))));
    public static SqlCommand SaveCommand(SeatAllocationChange change)
    {
        SeatAllocationValidation.Change(change);
        return Command("Save",P("@kind",SqlDbType.VarChar,change.Kind,12),Code("@id",change.Id),P("@version",SqlDbType.Binary,change.Version is null?null:Convert.FromHexString(change.Version),8),P("@payload",SqlDbType.NVarChar,change.Payload,-1));
    }
    private async Task<DataSet> Run(SqlCommand command)
    {
        using(command)try
        {
            await using var c=new SqlConnection(connectionString);await AppSqlConnection.OpenAsync(c).ConfigureAwait(false);command.Connection=c;
            await using var reader=await command.ExecuteReaderAsync().ConfigureAwait(false);var data=new DataSet();int index=0;
            do{if(reader.FieldCount==0)continue;var table=new DataTable("Table"+index++);
                for(int i=0;i<reader.FieldCount;i++)table.Columns.Add(reader.GetName(i),reader.GetFieldType(i));
                while(await reader.ReadAsync().ConfigureAwait(false)){var values=new object[reader.FieldCount];reader.GetValues(values);table.Rows.Add(values);}data.Tables.Add(table);
            }while(await reader.NextResultAsync().ConfigureAwait(false));return data;
        }
        catch(SqlException error){throw new PersistenceException(error.Number is >=53000 and <=53099 or 52801?error.Message:RegistrationErrorMessages.ForNumber(error.Number),error.Number==53004?PersistenceFailure.DataConflict:PersistenceFailure.General);}
        catch(InvalidOperationException){throw new PersistenceException("Không đọc được phân bổ chỗ. Hãy tải lại dữ liệu; tra cứu yêu cầu đăng ký trước khi nhận lại lời mời.");}
    }
    private static SqlCommand Command(string name,params SqlParameter[] args){var q=new SqlCommand("dbo.usp_Seat_"+name){CommandType=CommandType.StoredProcedure,CommandTimeout=45};q.Parameters.AddRange(args);return q;}
    private static SqlParameter P(string name,SqlDbType type,object? value,int size=0)=>new(name,type,size){Value=value??DBNull.Value};
    private static SqlParameter Code(string name,string value)=>P(name,SqlDbType.VarChar,RegistrationValidation.RequiredCode(value),20);
    private static SqlParameter Token(string value)=>value is {Length:16}&&value.All(Uri.IsHexDigit)?P("@version",SqlDbType.Binary,Convert.FromHexString(value),8):throw new ArgumentException("Phiên bản đã thay đổi; hãy tải lại.");
}
