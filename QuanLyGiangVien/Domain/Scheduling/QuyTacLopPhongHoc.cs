using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyGiangVien.Domain.Scheduling;

internal static class ScheduleRules
{
    internal static string Id(string? value) => (value ?? "").Trim().ToUpperInvariant();
    internal static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    internal static string Label(string? value, IReadOnlyList<string> choices) =>
        choices.FirstOrDefault(choice => Same(choice, value)) ?? (value ?? "").Trim();
    internal static string Fold(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
            != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    internal static IReadOnlyDictionary<string, string> Errors(params (string Key, bool Invalid, string Message)[] rules) =>
        rules.Where(rule => rule.Invalid).GroupBy(rule => rule.Key)
            .ToImmutableDictionary(group => group.Key, group => group.First().Message);

    internal static string NextId(IEnumerable<string> ids, string prefix)
    {
        var maximum = ids.Select(id => Regex.Match(id, $"^{prefix}([0-9]{{4}})$", RegexOptions.IgnoreCase))
            .Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();
        if (maximum >= 9999)
            throw new InvalidOperationException($"Đã hết mã trong phạm vi {prefix}0001–{prefix}9999.");
        return $"{prefix}{maximum + 1:0000}";
    }

    internal static bool ValidRoomId(string value) => Regex.IsMatch(value, @"^[A-Z0-9_-]{1,20}$");

    internal static bool ValidTime(ClassMeeting? meeting) => meeting is not null
        && Enum.IsDefined(meeting.Day) && meeting.StartPeriod is >= 1 and <= 12
        && meeting.EndPeriod >= meeting.StartPeriod && meeting.EndPeriod <= 12
        && meeting.StartDate != default && meeting.EndDate >= meeting.StartDate
        && FirstOccurrence(meeting.StartDate, meeting.EndDate, meeting.Day) is not null;

    internal static DateOnly? FirstOccurrence(DateOnly start, DateOnly end, DayOfWeek day)
    {
        var offset = ((int)day - (int)start.DayOfWeek + 7) % 7;
        return end.DayNumber - start.DayNumber >= offset ? start.AddDays(offset) : null;
    }

    // Date ranges and period ranges are inclusive. The intersecting dates must
    // actually contain the shared weekday before two meetings can conflict.
    internal static bool Overlaps(ClassMeeting left, ClassMeeting right)
    {
        if (left.Day != right.Day || left.StartPeriod > right.EndPeriod || right.StartPeriod > left.EndPeriod)
            return false;
        var start = left.StartDate > right.StartDate ? left.StartDate : right.StartDate;
        var end = left.EndDate < right.EndDate ? left.EndDate : right.EndDate;
        return start <= end && FirstOccurrence(start, end, left.Day) is not null;
    }

    internal static Room Normalize(Room value) => value with {
        Id = Id(value.Id), Building = (value.Building ?? "").Trim(),
        Type = Label(value.Type, SchedulingLabels.RoomTypes), Status = Label(value.Status, SchedulingLabels.RoomStatuses)
    };

    internal static CourseClass Normalize(CourseClass value) => value with {
        Id = Id(value.Id), CourseId = Id(value.CourseId), SemesterId = Id(value.SemesterId), LecturerId = Id(value.LecturerId),
        Status = Label(value.Status, SchedulingLabels.ClassStatuses), Meetings = value.Meetings.IsDefault ? [] :
            value.Meetings.Select(meeting => meeting is null ? null! : meeting with { RoomId = Id(meeting.RoomId) }).ToImmutableArray()
    };
}
