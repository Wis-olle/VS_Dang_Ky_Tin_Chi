using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class WeeklyScheduleConflictTests
{
    [Fact]
    public void MondayPeriodsOneToThreeCanContainParallelClassesWithDifferentRoomsAndLecturers()
    {
        var store = Store(Class());
        var parallel = Class("LHP0002", "GV0002") with {
            CourseId = "EC101", Meetings = [Meeting("A202")]
        };

        Assert.True(store.TrySaveClass(parallel, true, out var errors));
        Assert.Empty(errors);
        var calendar = SchedulingQuery.Occurrences(store, Start, Start.AddDays(6));
        Assert.Equal(2, calendar.Count);
        Assert.All(calendar, occurrence => {
            Assert.Equal(Start, occurrence.Date);
            Assert.Equal(1, occurrence.Meeting.StartPeriod);
            Assert.Equal(3, occurrence.Meeting.EndPeriod);
        });
        Assert.Single(SchedulingQuery.Occurrences(store, Start, Start.AddDays(6), roomId: "A201"));
        Assert.Single(SchedulingQuery.Occurrences(store, Start, Start.AddDays(6), lecturerId: "GV0002"));
    }

    [Theory]
    [InlineData("A201", "GV0002", 1, 3, "phòng")]
    [InlineData("A202", "GV0001", 1, 3, "giảng viên")]
    [InlineData("A201", "GV0002", 3, 5, "phòng")]
    public void SharedResourceRejectsMondayOverlapAndKeepsCalendarUnchanged(
        string room, string lecturer, int from, int to, string reason)
    {
        var store = Store(Class());
        var before = store.Classes;
        var candidate = Class("LHP0002", lecturer) with { Meetings = [Meeting(room, from: from, to: to)] };

        Assert.False(store.TrySaveClass(candidate, true, out var errors));
        Assert.Contains(reason, errors["Meetings"], StringComparison.OrdinalIgnoreCase);
        Assert.Same(before, store.Classes);
        Assert.Single(SchedulingQuery.Occurrences(store, Start, Start.AddDays(6)));
    }

    [Fact]
    public void AdjacentPeriodFourStartsAfterPeriodThreeAndDoesNotConflict()
    {
        var store = Store(Class());
        Assert.True(store.TrySaveClass(Class("LHP0002") with {
            Meetings = [Meeting(from: 4, to: 6)]
        }, true, out _));

        var calendar = SchedulingQuery.Occurrences(store, Start, Start.AddDays(6));
        Assert.Equal(new[] { 1, 4 }, calendar.Select(item => item.Meeting.StartPeriod));
    }

    [Fact]
    public void MovedActualMeetingReleasesBaseSlotAndBlocksTheActualRoomAndSubstituteLecturer()
    {
        var store = Store(Class() with {
            ActualMeetings = [new(Start.AddDays(1), "A202", "GV0002", 1, 3)]
        });
        Assert.True(store.TrySaveClass(Class("LHP0002") with {
            Meetings = [Meeting() with { EndDate = Start }]
        }, true, out _));

        var actual = Assert.Single(SchedulingQuery.Occurrences(store, Start.AddDays(1), Start.AddDays(1)));
        Assert.Equal("A202", actual.RoomId);
        Assert.Equal("GV0002", actual.LecturerId);
        Assert.Empty(SchedulingQuery.Occurrences(store, Start, Start, roomId: "A202"));

        var tuesday = Meeting("A202", DayOfWeek.Tuesday) with {
            StartDate = Start.AddDays(1), EndDate = Start.AddDays(1)
        };
        Assert.False(store.TrySaveClass(Class("LHP0003", "GV0003") with {
            Meetings = [tuesday]
        }, true, out var roomErrors));
        Assert.Contains("phòng", roomErrors["Meetings"], StringComparison.OrdinalIgnoreCase);
        Assert.False(store.TrySaveClass(Class("LHP0003", "GV0002") with {
            Meetings = [tuesday with { RoomId = "B101" }]
        }, true, out var lecturerErrors));
        Assert.Contains("giảng viên", lecturerErrors["Meetings"], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(7, true)]
    public void StudentPlannerCannotCombineParallelClassesButAllowsDifferentActualMondays(
        int secondMondayOffset, bool expectedOption)
    {
        var secondMonday = Start.AddDays(secondMondayOffset);
        var store = Store(
            Class() with { Meetings = [Meeting() with { EndDate = Start }] },
            Class("LHP0002", "GV0002") with {
                CourseId = "EC101", Meetings = [Meeting("A202") with {
                    StartDate = secondMonday, EndDate = secondMonday
                }]
            });
        var occurrences = SchedulingQuery.Occurrences(store, Start, Start.AddDays(7));
        var classes = store.Classes.Select(item => new SchedulePlannerClass(
            item.Id, item.CourseId, item.CourseId, 3, item.SemesterId, true, false, true,
            occurrences.Where(occurrence => occurrence.Class.Id == item.Id)
                .Select(occurrence => new SchedulePlannerMeeting(item.Id, occurrence.Date,
                    occurrence.Meeting.StartPeriod, occurrence.Meeting.EndPeriod)).ToImmutableArray())).ToImmutableArray();
        var snapshot = new SchedulePlannerSnapshot("HK1", classes, [], null);
        var request = new SchedulePlannerRequest(["IT101", "EC101"], [], [], new());

        var result = SchedulePlanner.Generate(snapshot, request);
        if (expectedOption)
        {
            var option = Assert.Single(result.Options);
            Assert.Equal(new[] { "LHP0001", "LHP0002" }, option.AddClassIds.Order());
        }
        else
        {
            Assert.Empty(result.Options);
            Assert.Contains(result.Explanations, explanation => explanation.Contains("trùng", StringComparison.OrdinalIgnoreCase));
        }
    }
}
