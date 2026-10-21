using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
namespace QuanLyGiangVien.Data.Sql;
public sealed class LifecycleRepository(string connectionString)
{
    public Task<DataTable> ClassesAsync(string? term=null) => Query("Classes",P("@term",SqlDbType.VarChar,term,20));
    public Task<DataTable> CalendarAsync(string classId)=>Query("Calendar",P("@class",SqlDbType.VarChar,classId,20));
    public Task<DataTable> HistoryAsync(string term)=>Query("History",P("@term",SqlDbType.VarChar,term,20));
    public Task<DataTable> TermsAsync() => Query("Terms");
    public Task TransitionClassAsync(string id,string target,string version,string reason) => Transition(id,target,version,reason,false);
    public Task TransitionSemesterAsync(string id,string target,string version,string reason) => Transition(id,target,version,reason,true);
    private async Task Transition(string id,string target,string version,string reason,bool term)
    {
        if(string.IsNullOrWhiteSpace(id)||id.Length>20||target is not ("REGISTRATION" or "CLOSED" or "TEACHING" or "COMPLETED" or "CANCELED" or "ARCHIVED")
            ||string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <5 or >500)
            throw new ArgumentException("Cần mã và lý do 5–500 ký tự.");
        await Query("Transition",P("@id",SqlDbType.VarChar,id,20),P("@target",SqlDbType.VarChar,target,20),Token(version),
            P("@reason",SqlDbType.NVarChar,reason,500),P("@term",SqlDbType.Bit,term)).ConfigureAwait(false);
    }
    public Task<DataTable> PreviewScheduleAsync(LifecycleScheduleChange change) => Change(change,true);
    public async Task ChangeScheduleAsync(LifecycleScheduleChange change) => await Change(change,false).ConfigureAwait(false);
    private Task<DataTable> Change(LifecycleScheduleChange v,bool preview)
    {
        LifecycleRules.Validate(v);
        return Query("ChangeSchedule",P("@class",SqlDbType.VarChar,v.ClassId,20),P("@kind",SqlDbType.VarChar,v.Kind,12),
            P("@source",SqlDbType.Date,v.SourceDate?.ToDateTime(TimeOnly.MinValue)),P("@target",SqlDbType.Date,v.TargetDate?.ToDateTime(TimeOnly.MinValue)),
            P("@room",SqlDbType.VarChar,v.RoomId,20),P("@lecturer",SqlDbType.VarChar,v.LecturerId,20),P("@start",SqlDbType.Int,v.StartPeriod),
            P("@end",SqlDbType.Int,v.EndPeriod),Token(v.Version),P("@reason",SqlDbType.NVarChar,v.Reason,500),P("@preview",SqlDbType.Bit,preview),P("@source_meeting_id",SqlDbType.BigInt,v.SourceMeetingId));
    }
    public async Task SetHolidayAsync(string term,DateOnly date,bool active,string version,string reason)
    {
        if(string.IsNullOrWhiteSpace(term)||term.Length>20||string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <5 or >500)throw new ArgumentException("Cần mã kỳ và lý do 5–500 ký tự.");
        await Query("Holiday",P("@term",SqlDbType.VarChar,term,20),P("@date",SqlDbType.Date,date.ToDateTime(TimeOnly.MinValue)),
            P("@active",SqlDbType.Bit,active),Token(version),P("@reason",SqlDbType.NVarChar,reason,500)).ConfigureAwait(false);
    }
    private async Task<DataTable> Query(string proc,params SqlParameter[] parameters)
    {
        try {
            using var cn=new SqlConnection(connectionString);await AppSqlConnection.OpenAsync(cn).ConfigureAwait(false);
            using var command=new SqlCommand("dbo.usp_Lifecycle_"+proc,cn){CommandType=CommandType.StoredProcedure,CommandTimeout=30};
            command.Parameters.AddRange(parameters);using var reader=await command.ExecuteReaderAsync().ConfigureAwait(false);
            var result=new DataTable();if(reader.FieldCount>0)result.Load(reader);return result;
        }
        catch(SqlException ex){throw new PersistenceException(ex.Number switch {
            51001=>"Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại.",
            52600=>"Thông tin thao tác vận hành chưa hợp lệ.",52601=>"Phiên bản đã thay đổi hoặc lớp/kỳ đã kết thúc. Hãy tải lại.",
            52602=>"Không được chuyển trạng thái này. Hãy kiểm tra vòng đời lớp/học kỳ.",52603=>"Còn lớp, tổ hợp hoặc môn đồng hành bị ảnh hưởng. Cần xử lý các đăng ký liên quan trước.",
            52604=>"Có xung đột lịch phòng, giảng viên hoặc sinh viên bị ảnh hưởng. Thay đổi chưa lưu.",
            52605=>"Ngày học hoặc tài nguyên mới không hợp lệ. Không sửa lại lịch sử đã học.",
            52801=>"Tài khoản chưa được cấp quyền vận hành lớp. Liên hệ quản trị.",_=>SqlErrorMessages.ForNumber(ex.Number)},ex.Number switch {
                51001 or 52405=>PersistenceFailure.SessionExpired,52601=>PersistenceFailure.DataConflict,
                >=52600 and <=52699 or 52801=>PersistenceFailure.BusinessRejected,_=>PersistenceFailure.General});}
        catch(InvalidOperationException){throw new PersistenceException("Không thể thực hiện thao tác SQL. Hãy kiểm tra kết nối và tải lại dữ liệu.");}
    }
    private static SqlParameter Token(string value) => value is {Length:16}&&value.All(Uri.IsHexDigit)
        ? P("@version",SqlDbType.Binary,Convert.FromHexString(value),8):throw new ArgumentException("Phiên bản không hợp lệ; hãy tải lại.");
    private static SqlParameter P(string name,SqlDbType type,object? value,int size=0)=>new(name,type,size){Value=value??DBNull.Value};
}
