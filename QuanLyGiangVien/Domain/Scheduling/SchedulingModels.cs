using System.Collections.Immutable;

namespace QuanLyGiangVien.Domain.Scheduling;

public sealed record Room(string Id, string Building, int Capacity, string Type, string Status);
public sealed record Course(string Id, string Name, int Credits, string Faculty);
public sealed record Semester(string Id, string Name, DateOnly StartDate, DateOnly EndDate);
public sealed record ClassMeeting(string RoomId, DayOfWeek Day, int StartPeriod, int EndPeriod,
    DateOnly StartDate, DateOnly EndDate);
public sealed record CourseClass(string Id, string CourseId, string SemesterId, string LecturerId,
    int Capacity, int Enrolled, string Status, ImmutableArray<ClassMeeting> Meetings);
public sealed record ScheduledOccurrence(DateOnly Date, CourseClass Class, ClassMeeting Meeting)
{
    public string RoomId => Meeting.RoomId;
    public string LecturerId => Class.LecturerId;
}
public sealed record ScheduleSummary(int TotalClasses, int OpenClasses, int UnscheduledClasses,
    int TotalRooms, int AvailableRooms, int MaintenanceRooms, int Enrolled, int Capacity)
{
    public int FillPercent => Capacity == 0 ? 0 : (int)Math.Round(100d * Enrolled / Capacity);
}

public static class SchedulingLabels
{
    public const string Ready = "Sẵn sàng";
    public const string Maintenance = "Bảo trì";
    public const string Theory = "Lý thuyết";
    public const string Lab = "Thực hành";
    public const string Hall = "Hội trường";
    public const string Open = "Đang mở";
    public const string Closed = "Đã đóng";
    public const string Canceled = "Đã hủy";
    public static IReadOnlyList<string> RoomTypes { get; } = Array.AsReadOnly(new[] { Theory, Lab, Hall });
    public static IReadOnlyList<string> RoomStatuses { get; } = Array.AsReadOnly(new[] { Ready, Maintenance });
    public static IReadOnlyList<string> ClassStatuses { get; } = Array.AsReadOnly(new[] { Open, Closed, Canceled });
}
