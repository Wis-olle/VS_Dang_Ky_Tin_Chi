using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class SchedulingLecturerGuardTests
{
    private static Lecturer Lecturer() => DemoData.Create()[0];

    [Theory]
    [InlineData(SchedulingLabels.Open)]
    [InlineData(SchedulingLabels.Closed)]
    public void AssignedLecturerCannotPauseWhileClassIsNotCanceled(string classStatus)
    {
        var store = Store(Class() with { Status = classStatus });
        var snapshot = store.Classes;
        var error = SchedulingLecturerGuard.ValidationError(store, Lecturer() with { Status = "Tạm nghỉ" }, false);
        Assert.Contains("lớp", error, StringComparison.OrdinalIgnoreCase);
        Assert.Same(snapshot, store.Classes);
        Assert.Equal("Đang giảng dạy", Lecturer().Status);
    }

    [Fact]
    public void LecturerCanEditActiveProfileAndCanceledClassAllowsPause()
    {
        Assert.Null(SchedulingLecturerGuard.ValidationError(Store(Class()), Lecturer() with { Name = "Tên mới" }, false));
        Assert.Null(SchedulingLecturerGuard.ValidationError(Store(Class() with { Status = SchedulingLabels.Canceled }),
            Lecturer() with { Status = "Tạm nghỉ" }, false));
        Assert.Null(SchedulingLecturerGuard.ValidationError(Store(), Lecturer() with { Status = "Tạm nghỉ" }, false));
    }

    [Fact]
    public void NewLecturerDoesNotOwnAnExistingClassReference()
    {
        Assert.Null(SchedulingLecturerGuard.ValidationError(Store(Class()), Lecturer() with { Status = "Tạm nghỉ" }, true));
    }

    [Theory]
    [InlineData(SchedulingLabels.Open)]
    [InlineData(SchedulingLabels.Closed)]
    [InlineData(SchedulingLabels.Canceled)]
    public void DeleteProtectsEveryClassReferenceEvenWithNoEnrollmentsOrMeetings(string status)
    {
        var store = Store(Class() with { Status = status, Meetings = [] });
        var snapshot = store.Classes;
        var error = SchedulingLecturerGuard.DeleteError(store, " gv0001 ");
        Assert.Contains("lớp", error, StringComparison.OrdinalIgnoreCase);
        Assert.Same(snapshot, store.Classes);
    }

    [Fact]
    public void DeleteAllowsOnlyUnreferencedLecturers()
    {
        Assert.Null(SchedulingLecturerGuard.DeleteError(Store(Class()), "GV0002"));
        Assert.Null(SchedulingLecturerGuard.DeleteError(Store(Class()), "missing"));
    }

    [Fact]
    public void ValidationMatchesTrimmedIdsAndLiveScheduleChanges()
    {
        var store = Store();
        var paused = Lecturer() with { Id = " gv0001 ", Status = "Tạm nghỉ" };
        Assert.Null(SchedulingLecturerGuard.ValidationError(store, paused, false));
        Assert.True(store.TrySaveClass(Class(), true, out _));
        Assert.NotNull(SchedulingLecturerGuard.ValidationError(store, paused, false));
        Assert.True(store.TryDeleteClass("LHP0001", out _));
        Assert.Null(SchedulingLecturerGuard.ValidationError(store, paused, false));
    }
}
