using System.Collections.Immutable;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SchedulingStoreTests
{
    internal static readonly DateOnly Start = new(2026, 9, 7);
    internal static readonly DateOnly End = new(2026, 12, 27);
    internal static Room Room(string id = "A201", int capacity = 60) =>
        new(id, "Tòa A", capacity, SchedulingLabels.Theory, SchedulingLabels.Ready);
    internal static ClassMeeting Meeting(string room = "A201", DayOfWeek day = DayOfWeek.Monday,
        int from = 1, int to = 3) => new(room, day, from, to, Start, End);
    internal static CourseClass Class(string id = "LHP0001", string lecturer = "GV0001", int enrolled = 0) =>
        new(id, "IT101", "HK1", lecturer, 40, enrolled, SchedulingLabels.Open, [Meeting()]);
    internal static TeachingCatalog Catalog(Func<IReadOnlyList<Lecturer>>? lecturers = null) => new(
        [new Course("IT101", "Lập trình C#", 3, "Công nghệ thông tin"), new Course("EC101", "Kinh tế vi mô", 3, "Kinh tế")],
        [new Semester("HK1", "Học kỳ I", Start, End), new Semester("HK2", "Học kỳ II", End.AddDays(1), End.AddMonths(5))],
        lecturers ?? DemoData.Create);
    internal static ScheduleStore Store(params CourseClass[] classes) =>
        new(Catalog(), [Room(), Room("A202"), Room("B101")], classes);

    [Fact]
    public void SavingRoomCreatesImmutableSnapshotAndNormalizesInput()
    {
        var source = new[] { Room() };
        var store = new ScheduleStore(Catalog(), source, []);
        var snapshot = store.Rooms;
        Assert.True(store.TrySaveRoom(Room(" b102 ") with { Building = " Tòa B " }, true, out var errors));
        Assert.Empty(errors);
        Assert.Single(snapshot);
        Assert.Single(source);
        Assert.Equal("B102", store.Rooms[1].Id);
        Assert.Equal("Tòa B", store.Rooms[1].Building);
        Assert.True(store.TrySaveRoom(Room() with { Capacity = 80 }, false, out errors));
        Assert.Equal(60, snapshot[0].Capacity);
        Assert.Equal(80, store.Rooms[0].Capacity);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Building")]
    [InlineData("Capacity")]
    [InlineData("Type")]
    [InlineData("Status")]
    public void InvalidRoomFieldIsRejectedWithoutMutation(string field)
    {
        var valid = Room("A203");
        var invalid = field switch {
            "Id" => valid with { Id = "?" }, "Building" => valid with { Building = "" },
            "Capacity" => valid with { Capacity = 0 }, "Type" => valid with { Type = "?" },
            _ => valid with { Status = "?" }
        };
        var store = Store();
        var snapshot = store.Rooms;
        Assert.False(store.TrySaveRoom(invalid, true, out var errors));
        Assert.Contains(field, errors.Keys);
        Assert.Same(snapshot, store.Rooms);
    }

    [Fact]
    public void DuplicateAndMissingRoomsAreRejectedIgnoringCase()
    {
        var store = Store();
        Assert.False(store.TrySaveRoom(Room("a201"), true, out var errors));
        Assert.Contains("Id", errors.Keys);
        Assert.False(store.TrySaveRoom(Room("A999"), false, out errors));
        Assert.Contains("Id", errors.Keys);
        Assert.False(store.TryDeleteRoom("missing", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void RoomCapacityAndMaintenanceProtectExistingSchedule()
    {
        var store = Store(Class());
        Assert.False(store.TrySaveRoom(Room() with { Capacity = 39 }, false, out var errors));
        Assert.Contains("Capacity", errors.Keys);
        Assert.False(store.TrySaveRoom(Room() with { Status = SchedulingLabels.Maintenance }, false, out errors));
        Assert.Contains("Status", errors.Keys);
        Assert.Equal(SchedulingLabels.Ready, store.Rooms[0].Status);
    }

    [Fact]
    public void CanceledClassAllowsMaintenanceButRoomReferenceStillBlocksDelete()
    {
        var store = Store(Class() with { Status = SchedulingLabels.Canceled });
        Assert.True(store.TrySaveRoom(Room() with { Capacity = 20, Status = SchedulingLabels.Maintenance }, false, out _));
        Assert.False(store.TryDeleteRoom("a201", out var error));
        Assert.NotNull(error);
        var snapshot = store.Rooms;
        Assert.True(store.TryDeleteRoom("A202", out error));
        Assert.Null(error);
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(2, store.Rooms.Count);
    }

    [Fact]
    public void SavingClassCopiesMeetingCollectionAndPreservesOldSnapshots()
    {
        var store = Store();
        var snapshot = store.Classes;
        var value = Class() with { Id = " lhp0001 ", LecturerId = " gv0001 ", CourseId = " it101 ", SemesterId = " hk1 " };
        Assert.True(store.TrySaveClass(value, true, out var errors));
        Assert.Empty(errors);
        Assert.Empty(snapshot);
        Assert.Equal("LHP0001", store.Classes[0].Id);
        var original = store.Classes[0];
        Assert.True(store.TrySaveClass(original with { Capacity = 50 }, false, out _));
        Assert.Equal(40, original.Capacity);
        Assert.Equal(50, store.Classes[0].Capacity);
        Assert.Equal("LHP0002", store.NextClassId());
        Assert.Equal("P0001", store.NextRoomId());
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("CourseId")]
    [InlineData("SemesterId")]
    [InlineData("LecturerId")]
    [InlineData("Capacity")]
    [InlineData("Enrolled")]
    [InlineData("Status")]
    public void InvalidClassFieldsAreRejected(string field)
    {
        var valid = Class();
        var invalid = field switch {
            "Id" => valid with { Id = "bad?" }, "CourseId" => valid with { CourseId = "unknown" },
            "SemesterId" => valid with { SemesterId = "unknown" }, "LecturerId" => valid with { LecturerId = "unknown" },
            "Capacity" => valid with { Capacity = 0 }, "Enrolled" => valid with { Enrolled = -1 },
            _ => valid with { Status = "?" }
        };
        var store = Store();
        var snapshot = store.Classes;
        Assert.False(store.TrySaveClass(invalid, true, out var errors));
        Assert.Contains(field, errors.Keys);
        Assert.Same(snapshot, store.Classes);
    }

    [Fact]
    public void DuplicateAndMissingClassUpdatesAreRejected()
    {
        var store = Store(Class());
        Assert.False(store.TrySaveClass(Class("lhp0001"), true, out var errors));
        Assert.Contains("Id", errors.Keys);
        Assert.False(store.TrySaveClass(Class("LHP0099"), false, out errors));
        Assert.Contains("Id", errors.Keys);
        Assert.False(store.TryDeleteClass("missing", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void LecturerReferenceIsLiveAndInactiveAssignmentsAreRejected()
    {
        IReadOnlyList<Lecturer> lecturers = DemoData.Create();
        var store = new ScheduleStore(Catalog(() => lecturers), [Room()], []);
        lecturers = Array.AsReadOnly(lecturers.Select(item => item.Id == "GV0001"
            ? item with { Status = "Tạm nghỉ" } : item).ToArray());
        Assert.False(store.TrySaveClass(Class(), true, out var errors));
        Assert.Contains("LecturerId", errors.Keys);
        Assert.True(store.TrySaveClass(Class() with { Status = SchedulingLabels.Canceled }, true, out _));
    }

    [Fact]
    public void EnrollmentCannotBeWrittenByClassEditorAndProtectsCapacityIdentityCancellationAndDelete()
    {
        var store = Store(Class(enrolled: 25));
        var original = store.Classes[0];
        foreach (var invalid in new[] {
            original with { Enrolled = 24 }, original with { Capacity = 24 },
            original with { CourseId = "EC101" }, original with { SemesterId = "HK2", Meetings = [] },
            original with { Status = SchedulingLabels.Canceled }
        }) Assert.False(store.TrySaveClass(invalid, false, out _));
        Assert.Same(original, store.Classes[0]);
        Assert.False(store.TryDeleteClass(original.Id, out var error));
        Assert.NotNull(error);
        Assert.False(store.TrySaveClass(Class("LHP0002", "GV0002", 1) with { Meetings = [] }, true, out _));
        Assert.True(store.TrySaveClass(original with { Status = SchedulingLabels.Closed }, false, out _));
    }

    [Fact]
    public void DeleteEmptyClassKeepsOldSnapshot()
    {
        var store = Store(Class());
        var snapshot = store.Classes;
        Assert.True(store.TryDeleteClass(" lhp0001 ", out var error));
        Assert.Null(error);
        Assert.Single(snapshot);
        Assert.Empty(store.Classes);
    }

    [Theory]
    [InlineData("RoomId")]
    [InlineData("Day")]
    [InlineData("Period")]
    [InlineData("Dates")]
    [InlineData("OutsideSemester")]
    [InlineData("NoOccurrence")]
    public void InvalidMeetingsAreRejectedWithMeetingsError(string field)
    {
        var valid = Meeting();
        var invalid = field switch {
            "RoomId" => valid with { RoomId = "missing" }, "Day" => valid with { Day = (DayOfWeek)8 },
            "Period" => valid with { StartPeriod = 0, EndPeriod = 13 },
            "Dates" => valid with { EndDate = Start.AddDays(-1) },
            "OutsideSemester" => valid with { StartDate = Start.AddDays(-1) },
            _ => valid with { StartDate = Start.AddDays(1), EndDate = Start.AddDays(2) }
        };
        var store = Store();
        Assert.False(store.TrySaveClass(Class() with { Meetings = [invalid] }, true, out var errors));
        Assert.Contains("Meetings", errors.Keys);
        Assert.Empty(store.Classes);
    }

    [Fact]
    public void MaintenanceAndInsufficientCapacityBlockScheduling()
    {
        var store = new ScheduleStore(Catalog(), [Room() with { Capacity = 20 },
            Room("A202") with { Status = SchedulingLabels.Maintenance }], []);
        Assert.False(store.TrySaveClass(Class(), true, out var errors));
        Assert.Contains("Meetings", errors.Keys);
        Assert.False(store.TrySaveClass(Class() with { Meetings = [Meeting("A202")] }, true, out errors));
        Assert.Contains("Meetings", errors.Keys);
    }

    [Theory]
    [InlineData("A201", "GV0002", 3, 5, "phòng")]
    [InlineData("A202", "GV0001", 2, 3, "giảng viên")]
    public void RoomAndLecturerConflictsRejectSaveAtomically(string room, string lecturer, int from, int to, string reason)
    {
        var store = Store(Class());
        var snapshot = store.Classes;
        Assert.False(store.TrySaveClass(Class("LHP0002", lecturer) with { Meetings = [Meeting(room, from: from, to: to)] }, true, out var errors));
        Assert.Contains(reason, errors["Meetings"], StringComparison.OrdinalIgnoreCase);
        Assert.Same(snapshot, store.Classes);
    }

    [Fact]
    public void DifferentDayAdjacentPeriodsAndNonOverlappingDatesAreAllowed()
    {
        var store = Store(Class());
        foreach (var candidate in new[] {
            Class("LHP0002") with { Meetings = [Meeting(from: 4, to: 6)] },
            Class("LHP0003") with { Meetings = [Meeting(day: DayOfWeek.Tuesday)] }
        }) Assert.True(store.TrySaveClass(candidate, true, out _));
        var split = Store(Class() with { Meetings = [Meeting() with { EndDate = Start.AddDays(6) }] });
        Assert.True(split.TrySaveClass(Class("LHP0002") with { Meetings = [Meeting() with { StartDate = Start.AddDays(7) }] }, true, out _));
    }

    [Fact]
    public void ShortDateIntersectionWithoutSharedWeekdayDoesNotConflict()
    {
        var existing = Class() with { Meetings = [Meeting() with { StartDate = Start, EndDate = Start.AddDays(3) }] };
        var next = Class("LHP0002") with { Meetings = [Meeting() with { StartDate = Start.AddDays(1), EndDate = Start.AddDays(7) }] };
        var store = Store(existing);
        Assert.True(store.TrySaveClass(next, true, out _));
    }

    [Fact]
    public void CanceledClassesAreExcludedFromConflictsAndOwnUpdateIsAllowed()
    {
        var store = Store(Class() with { Status = SchedulingLabels.Canceled });
        Assert.True(store.TrySaveClass(Class("LHP0002"), true, out _));
        Assert.True(store.TrySaveClass(store.Classes[1] with { Capacity = 50 }, false, out _));
        Assert.False(store.TrySaveClass(store.Classes[0] with { Status = SchedulingLabels.Open }, false, out var errors));
        Assert.Contains("Meetings", errors.Keys);
    }

    [Fact]
    public void ClassCannotMeetTwiceInSameTimeEvenInDifferentRooms()
    {
        var store = Store();
        Assert.False(store.TrySaveClass(Class() with { Meetings = [Meeting(), Meeting("A202", from: 3, to: 5)] }, true, out var errors));
        Assert.Contains("Meetings", errors.Keys);
        Assert.True(store.TrySaveClass(Class() with { Meetings = [] }, true, out _));
    }

    [Fact]
    public void DefaultMeetingArrayIsNormalizedToEmpty()
    {
        var store = Store();
        Assert.True(store.TrySaveClass(Class() with { Meetings = default }, true, out _));
        Assert.Empty(store.Classes[0].Meetings);
        Assert.False(store.Classes[0].Meetings.IsDefault);
    }

    [Fact]
    public void NextIdsUseMaximumAndRejectExhaustedRange()
    {
        var store = new ScheduleStore(Catalog(), [Room("P0004"), Room("A999")], [Class("LHP0042") with { Meetings = [] }]);
        Assert.Equal("P0005", store.NextRoomId());
        Assert.Equal("LHP0043", store.NextClassId());
        var full = new ScheduleStore(Catalog(), [Room("P9999")], [Class("LHP9999") with { Meetings = [] }]);
        Assert.Throws<InvalidOperationException>(() => full.NextRoomId());
        Assert.Throws<InvalidOperationException>(() => full.NextClassId());
    }

    [Fact]
    public void ConstructorRejectsInvalidSeedsAndCopiesCollections()
    {
        Assert.Throws<ArgumentException>(() => new ScheduleStore(Catalog(), [Room(), Room("a201")], []));
        Assert.Throws<ArgumentException>(() => new ScheduleStore(Catalog(), [Room()], [Class() with { Capacity = 100 }]));
        var rooms = new[] { Room() };
        var classes = new[] { Class() };
        var store = new ScheduleStore(Catalog(), rooms, classes);
        rooms[0] = Room("B201");
        classes[0] = Class("LHP0099");
        Assert.Equal("A201", store.Rooms[0].Id);
        Assert.Equal("LHP0001", store.Classes[0].Id);
    }

    [Fact]
    public void ClassIdsRequireAsciiDigitsToKeepGeneratedIdsParseable()
    {
        var store = Store();
        Assert.False(store.TrySaveClass(Class("LHP١٢٣٤"), true, out var errors));
        Assert.Contains("Id", errors.Keys);
        Assert.Equal("LHP0001", store.NextClassId());
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public void CapacityUpperBoundaryRejectsRoomsAndClasses(int capacity)
    {
        var store = Store();
        Assert.False(store.TrySaveRoom(Room("A203", capacity), true, out var errors));
        Assert.Contains("Capacity", errors.Keys);
        Assert.False(store.TrySaveClass(Class() with { Capacity = capacity, Meetings = [] }, true, out errors));
        Assert.Contains("Capacity", errors.Keys);
    }

    [Fact]
    public void NullAndExcessiveMeetingCollectionsAreRejectedCleanly()
    {
        var store = Store();
        Assert.False(store.TrySaveClass(Class() with { Meetings = [null!] }, true, out var errors));
        Assert.Contains("Meetings", errors.Keys);
        Assert.False(store.TrySaveClass(Class() with { Meetings = Enumerable.Repeat(Meeting(), 8).ToImmutableArray() }, true, out errors));
        Assert.Contains("Meetings", errors.Keys);
        Assert.Empty(store.Classes);
    }
}
