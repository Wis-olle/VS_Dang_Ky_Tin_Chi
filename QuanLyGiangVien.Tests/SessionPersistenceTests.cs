using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SessionPersistenceTests
{
    [Theory]
    [InlineData(PersistenceFailure.General, "_global")]
    [InlineData(PersistenceFailure.SessionExpired, "_session")]
    public void LecturerFailurePreservesItsCategoryAndTheOriginalSnapshot(PersistenceFailure kind, string key)
    {
        var original = DemoData.Create()[0];
        var store = new LecturerStore([original], new RejectingPersistence(kind));
        var before = store.Items;
        Assert.False(store.TrySave(original with { Name = "Nguyễn Kiểm Thử" }, false, out var errors));
        Assert.Equal("Không thể lưu kiểm thử.", Assert.Single(errors).Value);
        Assert.Equal(key, Assert.Single(errors).Key);
        Assert.Same(before, store.Items);
    }

    [Theory]
    [InlineData(PersistenceFailure.General, "_global")]
    [InlineData(PersistenceFailure.SessionExpired, "_session")]
    public void RoomFailurePreservesItsCategoryAndTheOriginalSnapshot(PersistenceFailure kind, string key)
    {
        var demo = SchedulingDemo.Create();
        var store = new ScheduleStore(demo.Catalog, demo.Rooms, demo.Classes, new RejectingPersistence(kind));
        var before = store.Rooms;
        Assert.False(store.TrySaveRoom(before[0] with { Building = "Tòa nhà bản nháp" }, false, out var errors));
        Assert.Equal("Không thể lưu kiểm thử.", Assert.Single(errors).Value);
        Assert.Equal(key, Assert.Single(errors).Key);
        Assert.Same(before, store.Rooms);
    }

    [Theory]
    [InlineData(PersistenceFailure.General, "_global")]
    [InlineData(PersistenceFailure.SessionExpired, "_session")]
    public void ClassFailurePreservesItsCategoryAndTheOriginalSnapshot(PersistenceFailure kind, string key)
    {
        var demo = SchedulingDemo.Create();
        var store = new ScheduleStore(demo.Catalog, demo.Rooms, demo.Classes, new RejectingPersistence(kind));
        var before = store.Classes;
        Assert.False(store.TrySaveClass(before.Single(item => item.Id == "LHP0006"), false, out var errors));
        Assert.Equal("Không thể lưu kiểm thử.", Assert.Single(errors).Value);
        Assert.Equal(key, Assert.Single(errors).Key);
        Assert.Same(before, store.Classes);
    }

    private sealed class RejectingPersistence(PersistenceFailure kind) : ILecturerPersistence, ISchedulePersistence
    {
        private PersistenceException Error() => new("Không thể lưu kiểm thử.", kind);
        public Lecturer SaveLecturer(Lecturer value, bool isNew) => throw Error();
        public Room SaveRoom(Room value, bool isNew) => throw Error();
        public CourseClass SaveClass(CourseClass value, bool isNew) => throw Error();
        public void DeleteLecturer(string id, string version = "") => throw Error();
        public void DeleteRoom(string id, string version = "") => throw Error();
        public void DeleteClass(string id, string version = "") => throw Error();
    }
}
