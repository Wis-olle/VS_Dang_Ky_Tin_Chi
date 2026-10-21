using System.Collections.Immutable;

namespace QuanLyGiangVien.Domain.Scheduling;

public sealed record TimetablePlacement(int SessionIndex, int Lane, int LaneCount, ImmutableArray<string> Conflicts);

public static class WeeklyTimetableLayout
{
    public const int FirstPeriod = 1;
    public const int LastPeriod = 12;

    public static IReadOnlyList<TimetablePlacement> Arrange(IReadOnlyList<ScheduledOccurrence> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        if (sessions.Any(item => item is null || item.Class is null || item.Meeting is null
            || item.Date == default || item.Meeting.StartPeriod < FirstPeriod
            || item.Meeting.EndPeriod < item.Meeting.StartPeriod || item.Meeting.EndPeriod > LastPeriod))
            throw new ArgumentException("Lịch tuần phải có ngày học và khoảng tiết 1–12 hợp lệ.", nameof(sessions));

        var indexed = sessions.Select((session, index) => new IndexedSession(index, session));
        return indexed.GroupBy(item => item.Session.Date).SelectMany(day =>
                Clusters(day.OrderBy(item => item.Session.Meeting.StartPeriod)
                    .ThenBy(item => item.Session.Meeting.EndPeriod)
                    .ThenBy(item => item.Session.Class.Id, StringComparer.Ordinal)
                    .ThenBy(item => item.Index)).SelectMany(PlaceCluster))
            .OrderBy(item => item.SessionIndex).ToImmutableArray();
    }

    // Inclusive ranges: 1–3 and 3–5 share a period; 1–3 and 4–6 are adjacent.
    private static IEnumerable<ImmutableArray<IndexedSession>> Clusters(IEnumerable<IndexedSession> ordered)
    {
        var cluster = ImmutableArray<IndexedSession>.Empty;
        var lastPeriod = 0;
        foreach (var item in ordered)
        {
            if (!cluster.IsEmpty && item.Session.Meeting.StartPeriod > lastPeriod)
            {
                yield return cluster;
                cluster = [];
                lastPeriod = 0;
            }
            cluster = cluster.Add(item);
            lastPeriod = Math.Max(lastPeriod, item.Session.Meeting.EndPeriod);
        }
        if (!cluster.IsEmpty) yield return cluster;
    }

    private static IEnumerable<TimetablePlacement> PlaceCluster(ImmutableArray<IndexedSession> cluster)
    {
        var laneEnds = ImmutableArray<int>.Empty;
        var placements = ImmutableArray<TimetablePlacement>.Empty;
        foreach (var item in cluster)
        {
            var lane = 0;
            while (lane < laneEnds.Length && laneEnds[lane] >= item.Session.Meeting.StartPeriod) lane++;
            laneEnds = lane == laneEnds.Length ? laneEnds.Add(item.Session.Meeting.EndPeriod)
                : laneEnds.SetItem(lane, item.Session.Meeting.EndPeriod);
            placements = placements.Add(new(item.Index, lane, 0, ConflictReasons(item, cluster)));
        }
        return placements.Select(item => item with { LaneCount = laneEnds.Length });
    }

    private static ImmutableArray<string> ConflictReasons(IndexedSession item, ImmutableArray<IndexedSession> cluster)
    {
        if (!LifecycleRules.BlocksResources(item.Session.Class)) return [];
        return cluster.Where(other => other.Index != item.Index && LifecycleRules.BlocksResources(other.Session.Class)
                && item.Session.Meeting.StartPeriod <= other.Session.Meeting.EndPeriod
                && other.Session.Meeting.StartPeriod <= item.Session.Meeting.EndPeriod)
            .SelectMany(other => ResourceReasons(item.Session, other.Session)).Distinct(StringComparer.Ordinal).ToImmutableArray();
    }

    private static IEnumerable<string> ResourceReasons(ScheduledOccurrence item, ScheduledOccurrence other)
    {
        if (ScheduleRules.Same(item.RoomId, other.RoomId))
            yield return $"Trùng phòng {item.RoomId} với lớp {other.Class.Id}.";
        if (ScheduleRules.Same(item.LecturerId, other.LecturerId))
            yield return $"Trùng lịch giảng viên {item.LecturerId} với lớp {other.Class.Id}.";
    }

    private sealed record IndexedSession(int Index, ScheduledOccurrence Session);
}
