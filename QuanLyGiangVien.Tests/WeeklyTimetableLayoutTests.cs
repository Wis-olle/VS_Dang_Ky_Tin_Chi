using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class WeeklyTimetableLayoutTests
{
    private static ScheduledOccurrence Session(string id, string room = "A201", string lecturer = "GV0001",
        int from = 1, int to = 3, int day = 0) =>
        new(Start.AddDays(day), Class(id, lecturer), Meeting(room, from: from, to: to));

    [Fact]
    public void ParallelDistinctResourcesUseSeparateLanesWithoutReportingConflict()
    {
        var result = WeeklyTimetableLayout.Arrange([
            Session("LHP0002", "A202", "GV0002"), Session("LHP0001")
        ]);
        Assert.Equal(new[] { 0, 1 }, result.Select(item => item.SessionIndex));
        Assert.Equal(new[] { 1, 0 }, result.Select(item => item.Lane));
        Assert.All(result, item => { Assert.Equal(2, item.LaneCount); Assert.Empty(item.Conflicts); });
    }

    [Fact]
    public void OverlapClusterReusesFreedLaneAndLaterClusterRestoresFullWidth()
    {
        var result = WeeklyTimetableLayout.Arrange([
            Session("A", from: 1, to: 3), Session("B", "A202", "GV0002", 3, 5),
            Session("C", "B101", "GV0003", 4, 6), Session("D", from: 8, to: 10)
        ]);
        Assert.Equal(new[] { 0, 1, 0, 0 }, result.Select(item => item.Lane));
        Assert.Equal(new[] { 2, 2, 2, 1 }, result.Select(item => item.LaneCount));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void InclusivePeriodBoundarySeparatesOverlappingButNotAdjacentMeetings(int start, int laneCount)
    {
        var result = WeeklyTimetableLayout.Arrange([
            Session("A"), Session("B", "A202", "GV0002", start, 5)
        ]);
        Assert.All(result, item => Assert.Equal(laneCount, item.LaneCount));
        Assert.All(result, item => Assert.Empty(item.Conflicts));
    }

    [Fact]
    public void SameResourceAndPeriodsOnDifferentActualDatesHaveIndependentLanes()
    {
        var result = WeeklyTimetableLayout.Arrange([Session("A"), Session("B", day: 7)]);
        Assert.All(result, item => {
            Assert.Equal(0, item.Lane); Assert.Equal(1, item.LaneCount); Assert.Empty(item.Conflicts);
        });
    }

    [Fact]
    public void SharedRoomAndActualSubstituteLecturerReportBothReasonsOnBothCards()
    {
        var result = WeeklyTimetableLayout.Arrange([
            Session("A") with { ActualLecturerId = "GV0002" },
            Session("B", lecturer: "GV0002")
        ]);
        Assert.All(result, item => {
            Assert.Contains(item.Conflicts, text => text.Contains("phòng", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(item.Conflicts, text => text.Contains("giảng viên", StringComparison.OrdinalIgnoreCase));
        });
        Assert.All(result[0].Conflicts, text => Assert.Contains("B", text));
        Assert.All(result[1].Conflicts, text => Assert.Contains("A", text));
    }

    [Theory]
    [InlineData("a201", "GV0002", "phòng", "giảng viên")]
    [InlineData("A202", "gv0001", "giảng viên", "phòng")]
    public void ReportsOnlyTheSharedResourceWithCaseInsensitiveIdentifiers(
        string room, string lecturer, string expected, string absent)
    {
        var result = WeeklyTimetableLayout.Arrange([Session("A"), Session("B", room, lecturer)]);
        Assert.All(result, item => {
            var reason = Assert.Single(item.Conflicts);
            Assert.Contains(expected, reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(absent, reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void SubstituteReleasesNominalLecturerAndCompletedClassDoesNotReportResourceConflict()
    {
        var result = WeeklyTimetableLayout.Arrange([
            Session("A") with { ActualLecturerId = "GV0002" },
            Session("B", "A202"),
            Session("C") with { Class = Class("C") with { Lifecycle = "COMPLETED" } }
        ]);
        Assert.All(result, item => { Assert.Equal(3, item.LaneCount); Assert.Empty(item.Conflicts); });
    }

    [Fact]
    public void ReorderingInputKeepsSemanticLaneChoiceAndDoesNotMutateSource()
    {
        var sessions = new[] { Session("B", "A202", "GV0002"), Session("A") };
        var before = sessions.ToArray();
        var first = WeeklyTimetableLayout.Arrange(sessions);
        var reverse = sessions.Reverse().ToArray();
        var second = WeeklyTimetableLayout.Arrange(reverse);
        Assert.Equal(first.Select(item => (sessions[item.SessionIndex].Class.Id, item.Lane)).OrderBy(item => item.Id),
            second.Select(item => (reverse[item.SessionIndex].Class.Id, item.Lane)).OrderBy(item => item.Id));
        Assert.Equal(before, sessions);
    }

    [Fact]
    public void EmptyCalendarAndInvalidInputsHaveExplicitBehavior()
    {
        Assert.Empty(WeeklyTimetableLayout.Arrange([]));
        Assert.Throws<ArgumentNullException>(() => WeeklyTimetableLayout.Arrange(null!));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([null!]));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([Session("A") with { Date = default }]));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([Session("A") with { Class = null! }]));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([Session("A") with { Meeting = null! }]));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([Session("A", from: 0)]));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([Session("A", from: 4, to: 3)]));
        Assert.Throws<ArgumentException>(() => WeeklyTimetableLayout.Arrange([Session("A", to: 13)]));
    }
}
