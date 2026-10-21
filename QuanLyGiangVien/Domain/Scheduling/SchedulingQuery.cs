namespace QuanLyGiangVien.Domain.Scheduling;

public static class SchedulingQuery
{
    public static IReadOnlyList<CourseClass> FilterClasses(ScheduleStore store, string query, string semesterId, string status)
    {
        ArgumentNullException.ThrowIfNull(store);
        var search = ScheduleRules.Fold(query);
        var lecturers = store.Catalog.Lecturers;
        return Array.AsReadOnly(store.Classes.Where(item =>
            All(semesterId, "Tất cả học kỳ") || ScheduleRules.Same(item.SemesterId, semesterId))
            .Where(item => All(status, "Tất cả trạng thái") || ScheduleRules.Same(item.Status, status))
            .Where(item => search.Length == 0 || ClassFields(store, lecturers, item)
                .Any(field => ScheduleRules.Fold(field).Contains(search, StringComparison.Ordinal))).ToArray());
    }

    public static IReadOnlyList<Room> FilterRooms(ScheduleStore store, string query, string building, string type, string status)
    {
        ArgumentNullException.ThrowIfNull(store);
        var search = ScheduleRules.Fold(query);
        return Array.AsReadOnly(store.Rooms.Where(item =>
            (All(building, "Tất cả tòa nhà") || ScheduleRules.Same(item.Building, building)) &&
            (All(type, "Tất cả loại phòng") || ScheduleRules.Same(item.Type, type)) &&
            (All(status, "Tất cả trạng thái") || ScheduleRules.Same(item.Status, status)) &&
            (search.Length == 0 || new[] { item.Id, item.Building, item.Type, item.Status, item.Capacity.ToString() }
                .Any(field => ScheduleRules.Fold(field).Contains(search, StringComparison.Ordinal)))).ToArray());
    }

    public static IReadOnlyList<ScheduledOccurrence> Occurrences(ScheduleStore store, DateOnly startDate, DateOnly endDate,
        string? roomId = null, string? lecturerId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (startDate > endDate) return [];
        return Array.AsReadOnly(store.Classes.Where(item => item.Status != SchedulingLabels.Canceled)
            .SelectMany(item => ExpandActual(item, startDate, endDate))
            .Where(item => (string.IsNullOrWhiteSpace(roomId) || ScheduleRules.Same(item.RoomId,roomId))
                && (string.IsNullOrWhiteSpace(lecturerId) || ScheduleRules.Same(item.LecturerId,lecturerId)))
            .OrderBy(item => item.Date).ThenBy(item => item.Meeting.StartPeriod).ThenBy(item => item.Class.Id, StringComparer.Ordinal)
            .ToArray());
    }

    internal static IEnumerable<ScheduledOccurrence> ExpandActual(CourseClass item, DateOnly from, DateOnly to)
    {
        if(item.ActualMeetings.IsDefault) return item.Meetings.SelectMany(m=>Expand(item,m,from,to));
        return item.ActualMeetings.Where(m=>m.Date>=from && m.Date<=to).Select(m=>
            new ScheduledOccurrence(m.Date,item,new(m.RoomId,m.Date.DayOfWeek,m.StartPeriod,m.EndPeriod,m.Date,m.Date))
                {ActualLecturerId=m.LecturerId});
    }

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    public static ScheduleSummary Summary(ScheduleStore store, string? semesterId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var classes = store.Classes.Where(item => All(semesterId, "Tất cả học kỳ")
            || ScheduleRules.Same(item.SemesterId, semesterId)).ToArray();
        var active = classes.Where(item => item.Status != SchedulingLabels.Canceled).ToArray();
        return new(classes.Length, classes.Count(item => item.Status == SchedulingLabels.Open),
            active.Count(item => item.Meetings.IsEmpty), store.Rooms.Count,
            store.Rooms.Count(item => item.Status == SchedulingLabels.Ready),
            store.Rooms.Count(item => item.Status == SchedulingLabels.Maintenance),
            active.Sum(item => item.Enrolled), active.Sum(item => item.Capacity));
    }

    public static IReadOnlyList<Room> AvailableRooms(ScheduleStore store, ClassMeeting requested, int capacity, string? excludeClassId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!ScheduleRules.ValidTime(requested) || capacity is < 1 or > 500) return [];
        var scheduled = store.Classes.Where(item => LifecycleRules.BlocksResources(item)
            && !ScheduleRules.Same(item.Id, excludeClassId)).SelectMany(item=>ExpandActual(item,requested.StartDate,requested.EndDate)).ToArray();
        return Array.AsReadOnly(store.Rooms.Where(room => room.Status == SchedulingLabels.Ready && room.Capacity >= capacity
            && !scheduled.Any(occurrence => ScheduleRules.Same(occurrence.RoomId, room.Id)
                && ScheduleRules.Overlaps(requested, occurrence.Meeting))).ToArray());
    }

    private static bool All(string? value, string label) => string.IsNullOrWhiteSpace(value) || ScheduleRules.Same(value, label);

    private static IEnumerable<string> ClassFields(ScheduleStore store, IReadOnlyList<TeachingLecturer> lecturers, CourseClass item)
    {
        var course = store.Catalog.Courses.FirstOrDefault(course => ScheduleRules.Same(course.Id, item.CourseId));
        var lecturer = lecturers.FirstOrDefault(lecturer => ScheduleRules.Same(lecturer.Id, item.LecturerId));
        return new[] { item.Id, item.CourseId, course?.Name ?? "", course?.Faculty ?? "",
            item.LecturerId, lecturer?.Name ?? "", item.Status }.Concat(item.Meetings.Select(meeting => meeting.RoomId));
    }

    private static IEnumerable<ScheduledOccurrence> Expand(CourseClass item, ClassMeeting meeting, DateOnly from, DateOnly to)
    {
        var start = from > meeting.StartDate ? from : meeting.StartDate;
        var end = to < meeting.EndDate ? to : meeting.EndDate;
        if (start > end) yield break;
        var first = ScheduleRules.FirstOccurrence(start, end, meeting.Day);
        if (first is null) yield break;
        for (var day = first.Value.DayNumber; day <= end.DayNumber; day += 7)
            yield return new(DateOnly.FromDayNumber(day), item, meeting);
    }
}
