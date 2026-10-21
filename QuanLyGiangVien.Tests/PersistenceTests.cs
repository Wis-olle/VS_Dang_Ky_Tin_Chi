using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Data;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class PersistenceTests
{
    [Theory]
    [InlineData("Name", 101)]
    [InlineData("Faculty", 101)]
    [InlineData("Degree", 81)]
    [InlineData("Email", 255)]
    [InlineData("AvatarKey", 41)]
    public void LecturerSqlFieldBoundsAreRejectedBeforePersistence(string field, int length)
    {
        var sample = DemoData.Create()[0] with { ActiveClasses = 0 };
        var value = field switch {
            "Name" => sample with { Name = new string('A', length) },
            "Faculty" => sample with { Faculty = new string('A', length) },
            "Degree" => sample with { Degree = new string('A', length) },
            "Email" => sample with { Email = new string('a', length - 12) + "@example.edu" },
            _ => sample with { AvatarKey = new string('a', length) }
        };
        var persistence = new FailingPersistence();
        var store = new LecturerStore([sample], persistence);
        var before = store.Items;
        Assert.False(store.TrySave(value, false, out var errors));
        Assert.Contains(field, errors.Keys);
        Assert.Equal(0, persistence.Calls);
        Assert.Same(before, store.Items);
    }

    [Theory]
    [InlineData("../portrait-01")]
    [InlineData("portrait-49")]
    [InlineData("portrait-00")]
    public void LecturerRejectsUnsupportedAvatarAtDomainBoundary(string key)
    {
        var store = new LecturerStore([]);
        Assert.False(store.TrySave(DemoData.Create()[0] with { AvatarKey = key }, true, out var errors));
        Assert.Contains("AvatarKey", errors.Keys);
    }

    [Fact]
    public void LecturerRejectsNonAsciiEmailBeforeVarcharConversion()
    {
        var store = new LecturerStore([]);
        Assert.False(store.TrySave(DemoData.Create()[0] with { Email = "minh@đạihọc.edu.vn" }, true, out var errors));
        Assert.Contains("Email", errors.Keys);
    }

    [Theory]
    [InlineData("Id", "GV١٢٣٤")]
    [InlineData("Phone", "0١٢٣٤٥٦٧٨٩")]
    public void LecturerRejectsNonAsciiDigitsInVarcharIdentifiersAndPhone(string field, string value)
    {
        var item = DemoData.Create()[0];
        var store = new LecturerStore([]);
        Assert.False(store.TrySave(field == "Id" ? item with { Id = value } : item with { Phone = value }, true, out var errors));
        Assert.Contains(field, errors.Keys);
    }
    [Fact]
    public void LecturerValidationRunsBeforePersistenceAndFailureKeepsSnapshot()
    {
        var persistence = new FailingPersistence();
        var sample = DemoData.Create()[0] with { ActiveClasses = 0 };
        var store = new LecturerStore([sample], persistence);
        var before = store.Items;
        Assert.False(store.TrySave(sample with { Name = "" }, false, out _));
        Assert.Equal(0, persistence.Calls);
        Assert.False(store.TrySave(sample with { Name = "Nguyễn Đăng Minh" }, false, out var errors));
        Assert.Equal("Không thể lưu dữ liệu kiểm thử.", errors["_global"]);
        Assert.Same(before, store.Items);
        Assert.False(store.TryDelete(sample.Id, out var error));
        Assert.Equal("Không thể lưu dữ liệu kiểm thử.", error);
        Assert.Same(before, store.Items);
        Assert.Equal(2, persistence.Calls);
    }

    [Fact]
    public void SchedulingLoadDoesNotWriteAndAllFailuresKeepSnapshots()
    {
        var persistence = new FailingPersistence();
        var demo = SchedulingDemo.Create();
        var store = new ScheduleStore(demo.Catalog, demo.Rooms, demo.Classes, persistence);
        Assert.Equal(0, persistence.Calls);
        var rooms = store.Rooms;
        var classes = store.Classes;
        Assert.False(store.TrySaveRoom(rooms[0] with { Capacity = 0 }, false, out _));
        Assert.Equal(0, persistence.Calls);
        Assert.False(store.TrySaveRoom(rooms[0] with { Building = "Tòa mới" }, false, out var errors));
        Assert.Contains("_global", errors.Keys);
        Assert.False(store.TrySaveClass(classes[0], false, out _));
        Assert.False(store.TryDeleteRoom("C202", out _));
        Assert.False(store.TryDeleteClass("LHP0006", out _));
        Assert.Equal(4, persistence.Calls);
        Assert.Same(rooms, store.Rooms);
        Assert.Same(classes, store.Classes);
    }

    [Fact]
    public void SuccessfulPersistenceReceivesNormalizedValueBeforeMemoryChanges()
    {
        var sample = DemoData.Create()[0] with { ActiveClasses = 0 };
        LecturerStore? store = null;
        var persistence = new RecordingPersistence(value => {
            Assert.Equal(sample.Name, store!.Items[0].Name);
            Assert.Equal("Nguyễn Đăng Minh", value.Name);
            Assert.Equal("0912345678", value.Phone);
        });
        store = new LecturerStore([sample], persistence);
        Assert.True(store.TrySave(sample with { Name = " Nguyễn Đăng Minh ", Phone = "+84912345678" }, false, out _));
        Assert.Equal("Nguyễn Đăng Minh", store.Items[0].Name);
        Assert.True(store.TryDelete(sample.Id, out _));
        Assert.Empty(store.Items);
    }

    [Fact]
    public void SuccessfulScheduleWritesRunBeforeReplacingSnapshots()
    {
        var demo = SchedulingDemo.Create();
        ScheduleStore? store = null;
        var persistence = new RecordingSchedulePersistence((kind, id) => {
            if (kind == "room-save") Assert.DoesNotContain(store!.Rooms, r => r.Id == id);
            if (kind == "room-delete") Assert.Contains(store!.Rooms, r => r.Id == id);
            if (kind == "class-save") Assert.DoesNotContain(store!.Classes, r => r.Id == id);
            if (kind == "class-delete") Assert.Contains(store!.Classes, r => r.Id == id);
        });
        store = new ScheduleStore(demo.Catalog, demo.Rooms, demo.Classes, persistence);
        Assert.True(store.TrySaveRoom(new("P9999", "Tòa mới", 60, SchedulingLabels.Theory, SchedulingLabels.Ready), true, out _));
        Assert.True(store.TryDeleteRoom("P9999", out _));
        var item = demo.Classes[5] with { Id = "LHP9999", Meetings = [] };
        Assert.True(store.TrySaveClass(item, true, out _));
        Assert.True(store.TryDeleteClass(item.Id, out _));
    }

    [Fact]
    public void SynchronizeAssignmentsDerivesCountsWithoutPersistenceOrMutatingPriorSnapshot()
    {
        var demo = SchedulingDemo.Create();
        var persistence = new FailingPersistence();
        var store = new LecturerStore(DemoData.Create(), persistence);
        var before = store.Items;
        store.SynchronizeAssignments(demo.Classes);
        Assert.Equal(demo.Classes.Count(c => c.LecturerId == store.Items[0].Id && c.Status != SchedulingLabels.Canceled),
            store.Items[0].ActiveClasses);
        Assert.NotSame(before, store.Items);
        Assert.Equal(0, persistence.Calls);
    }

    [Fact]
    public void SuccessfulClassWriteUsesCanonicalServerEnrollment()
    {
        var demo = SchedulingDemo.Create();
        var store = new ScheduleStore(demo.Catalog, demo.Rooms, demo.Classes, new EnrollmentPersistence(7));
        var before = store.Classes;
        var item = store.Classes.Single(c => c.Id == "LHP0006");
        Assert.True(store.TrySaveClass(item with { Capacity = 45 }, false, out _));
        Assert.Equal(7, store.Classes.Single(c => c.Id == item.Id).Enrolled);
        Assert.Equal(0, before.Single(c => c.Id == item.Id).Enrolled);
    }

    private sealed class EnrollmentPersistence(int enrolled) : ISchedulePersistence
    {
        public Room SaveRoom(Room value, bool isNew) => value;
        public void DeleteRoom(string id, string version = "") { }
        public CourseClass SaveClass(CourseClass value, bool isNew) => value with { Enrolled = enrolled };
        public void DeleteClass(string id, string version = "") { }
    }

    private sealed class RecordingPersistence(Action<Lecturer> save) : ILecturerPersistence
    {
        public Lecturer SaveLecturer(Lecturer value, bool isNew) { save(value); return value; }
        public void DeleteLecturer(string id, string version = "") { }
    }

    private sealed class RecordingSchedulePersistence(Action<string, string> write) : ISchedulePersistence
    {
        public Room SaveRoom(Room value, bool isNew) { write("room-save", value.Id); return value; }
        public void DeleteRoom(string id, string version = "") => write("room-delete", id);
        public CourseClass SaveClass(CourseClass value, bool isNew) { write("class-save", value.Id); return value; }
        public void DeleteClass(string id, string version = "") => write("class-delete", id);
    }

    private sealed class FailingPersistence : ILecturerPersistence, ISchedulePersistence
    {
        public int Calls { get; private set; }
        private void Fail() { Calls++; throw new PersistenceException("Không thể lưu dữ liệu kiểm thử."); }
        public Lecturer SaveLecturer(Lecturer value, bool isNew) { Fail(); return value; }
        public void DeleteLecturer(string id, string version = "") => Fail();
        public Room SaveRoom(Room value, bool isNew) { Fail(); return value; }
        public void DeleteRoom(string id, string version = "") => Fail();
        public CourseClass SaveClass(CourseClass value, bool isNew) { Fail(); return value; }
        public void DeleteClass(string id, string version = "") => Fail();
    }
}
