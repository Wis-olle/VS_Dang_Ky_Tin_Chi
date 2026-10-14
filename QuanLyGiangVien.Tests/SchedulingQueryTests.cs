using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class SchedulingQueryTests
{
    [Theory]
    [InlineData("lap trinh", 1)]
    [InlineData("nguyen hoang", 1)]
    [InlineData("lhp0001", 1)]
    [InlineData("A201", 1)]
    [InlineData("kinh te", 1)]
    [InlineData("missing", 0)]
    public void ClassSearchCoversReferencesAndIsAccentInsensitive(string query, int count)
    {
        var store = Store(Class(), Class("LHP0002", "GV0003") with { CourseId = "EC101", Meetings = [] });
        Assert.Equal(count, SchedulingQuery.FilterClasses(store, query, "", "").Count);
    }

    [Fact]
    public void ClassFiltersCombineSemesterAndStatusWithoutMutation()
    {
        var store = Store(Class(), Class("LHP0002", "GV0002") with {
            SemesterId = "HK2", Status = SchedulingLabels.Closed, Meetings = [] });
        Assert.Single(SchedulingQuery.FilterClasses(store, "", "hk2", SchedulingLabels.Closed));
        Assert.Empty(SchedulingQuery.FilterClasses(store, "", "HK1", SchedulingLabels.Closed));
        Assert.Equal(2, SchedulingQuery.FilterClasses(store, "", "Tất cả học kỳ", "Tất cả trạng thái").Count);
        Assert.Equal(2, store.Classes.Count);
    }

    [Fact]
    public void RoomSearchAndFiltersCombineAllFields()
    {
        var store = new ScheduleStore(Catalog(), [Room(), Room("B101") with {
            Building = "Tòa B", Type = SchedulingLabels.Lab, Status = SchedulingLabels.Maintenance }], []);
        Assert.Equal(2, SchedulingQuery.FilterRooms(store, "toa", "", "", "").Count);
        Assert.Single(SchedulingQuery.FilterRooms(store, "b101", "Tòa B", SchedulingLabels.Lab, SchedulingLabels.Maintenance));
        Assert.Empty(SchedulingQuery.FilterRooms(store, "", "Tòa A", SchedulingLabels.Lab, ""));
        Assert.Equal(2, SchedulingQuery.FilterRooms(store, "", "Tất cả tòa nhà", "Tất cả loại phòng", "Tất cả trạng thái").Count);
        Assert.Single(SchedulingQuery.FilterRooms(store, "bao tri", "", "", ""));
    }

    [Fact]
    public void SummaryRespectsSemesterAndExcludesCanceledCapacity()
    {
        var store = Store(Class(enrolled: 20), Class("LHP0002", "GV0002") with {
            SemesterId = "HK2", Meetings = [], Capacity = 20 }, Class("LHP0003", "GV0003") with {
            Status = SchedulingLabels.Canceled, Meetings = [] });
        var all = SchedulingQuery.Summary(store);
        Assert.Equal(3, all.TotalClasses);
        Assert.Equal(2, all.OpenClasses);
        Assert.Equal(1, all.UnscheduledClasses);
        Assert.Equal(3, all.TotalRooms);
        Assert.Equal(3, all.AvailableRooms);
        Assert.Equal(0, all.MaintenanceRooms);
        Assert.Equal(20, all.Enrolled);
        Assert.Equal(60, all.Capacity);
        Assert.Equal(33, all.FillPercent);
        Assert.Equal(50, SchedulingQuery.Summary(store, "hk1").FillPercent);
        Assert.Equal(0, SchedulingQuery.Summary(store, "missing").FillPercent);
    }

    [Fact]
    public void MaintenanceSummaryAndEmptyStoreAreDefined()
    {
        var store = new ScheduleStore(Catalog(), [Room() with { Status = SchedulingLabels.Maintenance }], []);
        var summary = SchedulingQuery.Summary(store);
        Assert.Equal(0, summary.AvailableRooms);
        Assert.Equal(1, summary.MaintenanceRooms);
        Assert.Equal(0, summary.FillPercent);
    }

    [Fact]
    public void OccurrencesExpandInclusiveDateRangeAndIgnoreCanceledClasses()
    {
        var store = Store(Class(), Class("LHP0002", "GV0002") with {
            Meetings = [Meeting("A202", DayOfWeek.Tuesday, 4, 6)] },
            Class("LHP0003", "GV0003") with { Status = SchedulingLabels.Canceled });
        var occurrences = SchedulingQuery.Occurrences(store, Start, Start.AddDays(7));
        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new[] { Start, Start.AddDays(1), Start.AddDays(7) }, occurrences.Select(item => item.Date));
        Assert.Equal("A201", occurrences[0].RoomId);
        Assert.Equal("GV0001", occurrences[0].LecturerId);
        Assert.Equal(2, SchedulingQuery.Occurrences(store, Start, Start.AddDays(7), "a201").Count);
        Assert.Single(SchedulingQuery.Occurrences(store, Start, Start.AddDays(7), lecturerId: "gv0002"));
        Assert.Empty(SchedulingQuery.Occurrences(store, Start.AddDays(8), Start));
        Assert.Empty(SchedulingQuery.Occurrences(store, Start.AddDays(-10), Start.AddDays(-1)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 0)]
    public void WeekStartAlignsMondayThroughSunday(int dayOffset, int expectedOffset) =>
        Assert.Equal(Start.AddDays(expectedOffset), SchedulingQuery.WeekStart(Start.AddDays(dayOffset)));

    [Fact]
    public void AvailableRoomsCheckActualWeekdayPeriodsDatesCapacityAndExcludedClass()
    {
        var store = Store(Class());
        Assert.Equal(2, SchedulingQuery.AvailableRooms(store, Meeting(), 40).Count);
        Assert.Equal(3, SchedulingQuery.AvailableRooms(store, Meeting(), 40, "lhp0001").Count);
        Assert.Equal(3, SchedulingQuery.AvailableRooms(store, Meeting(day: DayOfWeek.Tuesday), 40).Count);
        Assert.Equal(3, SchedulingQuery.AvailableRooms(store, Meeting(from: 4, to: 6), 40).Count);
        Assert.Empty(SchedulingQuery.AvailableRooms(store, Meeting(), 61));
        Assert.Empty(SchedulingQuery.AvailableRooms(store, Meeting() with { Day = (DayOfWeek)8 }, 40));
        Assert.Empty(SchedulingQuery.AvailableRooms(store, Meeting() with { EndDate = Start.AddDays(-1) }, 40));
        Assert.Empty(SchedulingQuery.AvailableRooms(store, Meeting(), -1));
    }

    [Fact]
    public void DemoUsesSharedLecturersAndValidNonConflictingSchedule()
    {
        var demo = SchedulingDemo.Create();
        Assert.Equal(8, demo.Rooms.Count);
        Assert.InRange(demo.Classes.Count, 8, 12);
        Assert.Equal(2, demo.Catalog.Semesters.Count);
        Assert.All(demo.Classes, item => Assert.Contains(demo.Catalog.Lecturers, teacher => teacher.Id == item.LecturerId));
        Assert.Contains(demo.Classes, item => item.Meetings.IsEmpty);
        Assert.Contains(demo.Classes, item => item.Meetings.Length == 2);
        Assert.NotEmpty(SchedulingQuery.Occurrences(demo, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18)));
        Assert.All(demo.Classes, item => Assert.True(demo.TrySaveClass(item, false, out _)));
    }
}
