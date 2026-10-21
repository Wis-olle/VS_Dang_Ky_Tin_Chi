using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace QuanLyGiangVien.Domain.Scheduling;
public sealed record ActualClassMeeting(DateOnly Date,string RoomId,string LecturerId,int StartPeriod,int EndPeriod);
public sealed record LifecycleScheduleChange(string ClassId,string Kind,DateOnly? SourceDate,DateOnly? TargetDate,
    string? RoomId,string? LecturerId,int? StartPeriod,int? EndPeriod,string Version,string Reason,long? SourceMeetingId=null);
public static class LifecycleRules
{
    public static bool CanTransition(string before,string after) => (before,after) switch {
        ("REGISTRATION","CLOSED" or "CANCELED") or ("CLOSED","REGISTRATION" or "TEACHING" or "CANCELED")
        or ("TEACHING","COMPLETED" or "CANCELED") or ("COMPLETED" or "CANCELED","ARCHIVED") => true,_=>false};
    public static bool BlocksResources(CourseClass item) => item.Status != SchedulingLabels.Canceled
        && item.Lifecycle is not ("COMPLETED" or "CANCELED" or "ARCHIVED");
    public static void Validate(LifecycleScheduleChange value)
    {
        if(!Regex.IsMatch(value.ClassId??"",@"^[A-Z0-9_-]{1,20}$") || value.Kind is not ("CANCEL" or "MAKEUP" or "REPLACE")
            || string.IsNullOrEmpty(value.Version) || value.Version.Length!=16 || !value.Version.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(value.Reason)
            || value.Reason.Trim().Length is < 5 or > 500) throw new ArgumentException("Mã lớp, phiên bản hoặc lý do thay đổi lịch không hợp lệ.");
        if(value.Kind!="MAKEUP" && value.SourceDate is null) throw new ArgumentException("Cần chọn ngày học gốc.");
        if(value.Kind!="CANCEL" && (value.TargetDate is null || !Regex.IsMatch(value.RoomId??"",@"^[A-Z0-9_-]{1,20}$")
            || !Regex.IsMatch(value.LecturerId??"",@"^[A-Z0-9_-]{1,20}$") || value.StartPeriod is null or < 1 or > 12
            || value.EndPeriod is null or > 12 || value.EndPeriod<value.StartPeriod)) throw new ArgumentException("Buổi thay thế/bù chưa hợp lệ.");
    }
    public static ImmutableArray<ActualClassMeeting> ReadActualCalendar(string? json)
    {
        if(json is null) return default;
        try {
            var meetings=JsonSerializer.Deserialize<ActualClassMeeting[]>(json) ?? throw new JsonException();
            if(meetings.Any(m=>m is null || m.Date==default || string.IsNullOrWhiteSpace(m.RoomId) || string.IsNullOrWhiteSpace(m.LecturerId)
                || m.StartPeriod is <1 or >12 || m.EndPeriod<m.StartPeriod || m.EndPeriod>12)) throw new JsonException();
            return meetings.ToImmutableArray();
        }
        catch(JsonException) {throw new PersistenceException("Lịch học thực tế không hợp lệ. Vui lòng tải lại.");}
    }
}
