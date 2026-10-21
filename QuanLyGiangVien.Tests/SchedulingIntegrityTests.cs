using QuanLyGiangVien.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SchedulingIntegrityTests
{
    [Fact]
    public void CanceledRegistrationHistoryProtectsClassIdentityAndDelete()
    {
        var demo = SchedulingDemo.Create();
        var item = demo.Classes.Single(c => c.Id == "LHP0006") with { HasRegistrationHistory = true };
        var store = new ScheduleStore(demo.Catalog, demo.Rooms, [item]);
        Assert.False(store.TrySaveClass(item with { CourseId = demo.Catalog.Courses.First(c => c.Id != item.CourseId).Id }, false, out var errors));
        Assert.Contains("CourseId", errors.Keys);
        Assert.False(store.TrySaveClass(item with { SemesterId = demo.Catalog.Semesters.First(c => c.Id != item.SemesterId).Id }, false, out errors));
        Assert.Contains("SemesterId", errors.Keys);
        Assert.False(store.TrySaveClass(item with { LecturerId = demo.Catalog.Lecturers.First(g => g.Id != item.LecturerId).Id }, false, out errors));
        Assert.Contains("LecturerId", errors.Keys);
        var term = demo.Catalog.Semesters.Single(t => t.Id == item.SemesterId);
        Assert.False(store.TrySaveClass(item with { Meetings = [new(demo.Rooms[0].Id, DayOfWeek.Monday, 1, 3, term.StartDate, term.EndDate)] }, false, out errors));
        Assert.Contains("Meetings", errors.Keys);
        Assert.False(store.TryDeleteClass(item.Id, out var error));
        Assert.Contains("lịch sử", error);
        Assert.True(store.TrySaveClass(item with { Capacity = 45 }, false, out _));
    }

    [Fact]
    public void StaleEditorTokensCannotReplaceNewerStoreSnapshots()
    {
        var demo = SchedulingDemo.Create();
        var room = demo.Rooms[0] with { Version = "0000000000000002" };
        var item = demo.Classes.Single(c => c.Id == "LHP0006") with { Version = "0000000000000002" };
        var store = new ScheduleStore(demo.Catalog, [room, .. demo.Rooms.Skip(1)], [item]);
        Assert.False(store.TrySaveRoom(room with { Building = "Stale", Version = "0000000000000001" }, false, out var errors));
        Assert.Contains("_global", errors.Keys);
        Assert.False(store.TrySaveClass(item with { Capacity = 45, Version = "0000000000000001" }, false, out errors));
        Assert.Contains("_global", errors.Keys);
        Assert.Equal(room, store.Rooms[0]);
        Assert.Equal(item, store.Classes[0]);
    }

    [Theory]
    [InlineData(501)]
    [InlineData(1000)]
    public void SqlSupportedRoomCapacityLoadsAndSaves(int capacity)
    {
        var demo = SchedulingDemo.Create();
        var store = new ScheduleStore(demo.Catalog, demo.Rooms, demo.Classes);
        Assert.True(store.TrySaveRoom(new("AUDIT", "Tòa kiểm thử", capacity,
            SchedulingLabels.Hall, SchedulingLabels.Ready), true, out var errors), string.Join(";", errors.Values));
        var reloaded = new ScheduleStore(demo.Catalog, store.Rooms, store.Classes);
        Assert.Equal(capacity, reloaded.Rooms.Single(r => r.Id == "AUDIT").Capacity);
    }

    [Fact]
    public void SaveCommandsCarryExpectedVersionToSql()
    {
        using var lecturer = SqlMinhCommands.SaveLecturer(DemoData.Create()[0], false);
        using var room = SqlMinhCommands.SaveRoom(SchedulingDemo.Create().Rooms[0], false);
        using var courseClass = SqlMinhCommands.SaveClass(SchedulingDemo.Create().Classes[0], false);
        foreach (var command in new[] { lecturer, room, courseClass })
            Assert.True(command.Parameters.Contains("@phien_ban"));
    }
}
