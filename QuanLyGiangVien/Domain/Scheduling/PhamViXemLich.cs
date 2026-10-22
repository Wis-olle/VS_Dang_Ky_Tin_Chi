using System.Collections.Immutable;

namespace QuanLyGiangVien.Domain.Scheduling;

public enum TimetableScopeKind { Lecturer, Student, CourseClass, StudentClass, Room }
public sealed record TimetableStudent(string Id, string Name, string ClassId, string ClassName);
public sealed record TimetableRegistration(string StudentId, string ClassId, bool IsCanceled = false);
public sealed record TimetableDirectory(ImmutableArray<TimetableStudent> Students,
    ImmutableArray<TimetableRegistration> Registrations);
public sealed record TimetableDisplayGroup(DateOnly Date, int StartPeriod, int EndPeriod,
    ImmutableArray<int> SessionIndexes);

public static class TimetableScope
{
    /// <summary>Filters actual occurrences already limited to the selected semester/week.
    /// A student class is the union of its members' active registrations, not a shared personal timetable.</summary>
    public static IReadOnlyList<ScheduledOccurrence> Select(ScheduleStore store,
        IReadOnlyList<ScheduledOccurrence> sessions, TimetableDirectory directory,
        TimetableScopeKind kind, string? subjectId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(directory);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(subjectId)) return ImmutableArray<ScheduledOccurrence>.Empty;
        var id = subjectId.Trim();
        var students = directory.Students.IsDefault ? [] : directory.Students;
        var registrations = directory.Registrations.IsDefault ? [] : directory.Registrations;
        var knownClasses = store.Classes.Select(item => item.Id).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        var memberIds = students.Where(student => kind == TimetableScopeKind.Student
                ? Same(student.Id, id) : kind == TimetableScopeKind.StudentClass && Same(student.ClassId, id))
            .Select(student => student.Id).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        var registeredClasses = registrations.Where(item => !item.IsCanceled && memberIds.Contains(item.StudentId))
            .Select(item => item.ClassId).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        var subjectExists = kind switch {
            TimetableScopeKind.Lecturer => store.Catalog.Lecturers.Any(item => Same(item.Id, id)),
            TimetableScopeKind.Room => store.Rooms.Any(item => Same(item.Id, id)),
            TimetableScopeKind.CourseClass => knownClasses.Contains(id),
            _ => memberIds.Count > 0
        };
        if (!subjectExists) return ImmutableArray<ScheduledOccurrence>.Empty;
        return sessions.Where(item => knownClasses.Contains(item.Class.Id) && (kind switch {
                TimetableScopeKind.Lecturer => Same(item.LecturerId, id),
                TimetableScopeKind.Room => Same(item.RoomId, id),
                TimetableScopeKind.CourseClass => Same(item.Class.Id, id),
                _ => registeredClasses.Contains(item.Class.Id)
            }))
            .DistinctBy(item => (item.Date, Class: Normalize(item.Class.Id), Room: Normalize(item.RoomId),
                item.Meeting.StartPeriod, item.Meeting.EndPeriod, Lecturer: Normalize(item.LecturerId)))
            .ToImmutableArray();
    }

    /// <summary>One fixed-width display block per connected overlap cluster; original indexes open the full list.</summary>
    public static IReadOnlyList<TimetableDisplayGroup> GroupForDisplay(IReadOnlyList<ScheduledOccurrence> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        if (sessions.Any(item => item is null || item.Class is null || item.Meeting is null
            || item.Date == default || item.Meeting.StartPeriod < WeeklyTimetableLayout.FirstPeriod
            || item.Meeting.EndPeriod < item.Meeting.StartPeriod || item.Meeting.EndPeriod > WeeklyTimetableLayout.LastPeriod))
            throw new ArgumentException("Lịch tuần phải có ngày học và khoảng tiết 1–12 hợp lệ.", nameof(sessions));
        var groups = ImmutableArray<TimetableDisplayGroup>.Empty;
        foreach (var item in sessions.Select((session, index) => (Session: session, Index: index))
            .OrderBy(item => item.Session.Date).ThenBy(item => item.Session.Meeting.StartPeriod)
            .ThenBy(item => item.Session.Meeting.EndPeriod).ThenBy(item => item.Index))
        {
            var meeting = item.Session.Meeting;
            var previous = groups.IsEmpty ? null : groups[^1];
            if (previous is not null && previous.Date == item.Session.Date && meeting.StartPeriod <= previous.EndPeriod)
                groups = groups.SetItem(groups.Length - 1, previous with {
                    EndPeriod = Math.Max(previous.EndPeriod, meeting.EndPeriod),
                    SessionIndexes = previous.SessionIndexes.Add(item.Index) });
            else groups = groups.Add(new(item.Session.Date, meeting.StartPeriod, meeting.EndPeriod, [item.Index]));
        }
        return groups;
    }

    private static bool Same(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
