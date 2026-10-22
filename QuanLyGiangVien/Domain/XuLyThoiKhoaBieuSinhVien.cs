using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Domain;

public sealed record StudentRegistration(string StudentId, string ClassId, bool IsCanceled = false);

public sealed record StudentTimetableSnapshot(Semester Semester, DateOnly WeekStart,
    ImmutableArray<CourseClass> Classes, ImmutableArray<ScheduledOccurrence> Occurrences,
    int CourseCount, int TotalCredits, int UnscheduledClasses)
{
    public DateOnly WeekEnd => DateOnly.FromDayNumber(Math.Min(WeekStart.DayNumber + 6, DateOnly.MaxValue.DayNumber));
    public bool CanGoPrevious => WeekStart > SchedulingQuery.WeekStart(Semester.StartDate);
    public bool CanGoNext => WeekEnd < Semester.EndDate;
}

/// <summary>Read-only projection. Registrations must come from the signed-in student's authorized source at integration.</summary>
public static class StudentTimetable
{
    // Lấy thời khóa biểu sinh viên
    public static StudentTimetableSnapshot Create(ScheduleStore store, IEnumerable<StudentRegistration> registrations,
        string studentId, string semesterId, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(semesterId);
        var semester = store.Catalog.Semesters.FirstOrDefault(s => ScheduleRules.Same(s.Id, semesterId))
            ?? throw new ArgumentException("Học kỳ không tồn tại.", nameof(semesterId));
        var source = registrations.ToArray();
        if (source.Any(r => r is null || string.IsNullOrWhiteSpace(r.StudentId) || string.IsNullOrWhiteSpace(r.ClassId)))
            throw new ArgumentException("Đăng ký không hợp lệ.", nameof(registrations));
        var ids = source.Where(r => !r.IsCanceled && ScheduleRules.Same(r.StudentId, studentId))
            .Select(r => r.ClassId.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classes = store.Classes.Where(c => ids.Contains(c.Id) && ScheduleRules.Same(c.SemesterId, semester.Id)
            && c.Status != SchedulingLabels.Canceled).OrderBy(c => c.Id).ToImmutableArray();
        var selectedIds = classes.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clamped = DateOnly.FromDayNumber(Math.Clamp(date.DayNumber, semester.StartDate.DayNumber, semester.EndDate.DayNumber));
        var monday = SchedulingQuery.WeekStart(clamped);
        var weekEnd = DateOnly.FromDayNumber(Math.Min(monday.DayNumber + 6, semester.EndDate.DayNumber));
        var from = monday < semester.StartDate ? semester.StartDate : monday;
        var occurrences = SchedulingQuery.Occurrences(store, from, weekEnd)
            .Where(o => selectedIds.Contains(o.Class.Id)).ToImmutableArray();
        var courseIds = classes.Select(c => c.CourseId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new(semester, monday, classes, occurrences, courseIds.Count,
            store.Catalog.Courses.Where(c => courseIds.Contains(c.Id)).Sum(c => c.Credits),
            classes.Count(c => c.Meetings.IsEmpty));
    }
}
