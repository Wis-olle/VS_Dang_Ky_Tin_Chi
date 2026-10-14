using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class StudentTimetableTests
{
    [Fact]
    public void FiltersStudentSemesterCanceledAndDuplicates()
    {
        var store = SchedulingDemo.Create();
        StudentRegistration[] registrations = [new("SV1", "LHP0001"), new("sv1", "lhp0001"),
            new("SV2", "LHP0002"), new("SV1", "LHP0003", true), new("SV1", "LHP0008"),
            new("SV1", "LHP0009"), new("SV1", "missing"), new("SV1", "LHP0007")];
        var result = StudentTimetable.Create(store, registrations, " sv1 ", "hk1-2627", Start);
        Assert.Equal(new[] { "LHP0001", "LHP0007" }, result.Classes.Select(item => item.Id));
        Assert.Equal(3, result.Occurrences.Length);
        Assert.Equal(6, result.TotalCredits);
        Assert.Equal(2, result.CourseCount);
        Assert.Equal(10, store.Classes.Count);
    }

    [Fact]
    public void CountsDistinctCoursesAndKeepsUnscheduledClasses()
    {
        var store = Store(Class(), Class("LHP0002", "GV0002") with { Meetings = [] });
        var result = StudentTimetable.Create(store, [new("S", "LHP0001"), new("S", "LHP0002")], "S", "HK1", Start);
        Assert.Equal(2, result.Classes.Length);
        Assert.Equal(1, result.CourseCount);
        Assert.Equal(3, result.TotalCredits);
        Assert.Equal(1, result.UnscheduledClasses);
        Assert.Single(result.Occurrences);
    }

    [Fact]
    public void ClampsNavigationAndOrdersOccurrences()
    {
        var store = SchedulingDemo.Create();
        var registrations = new[] { new StudentRegistration("S", "LHP0001"), new("S", "LHP0002") };
        var first = StudentTimetable.Create(store, registrations, "S", "HK1-2627", DateOnly.MinValue);
        var last = StudentTimetable.Create(store, registrations, "S", "HK1-2627", DateOnly.MaxValue);
        Assert.Equal(Start, first.WeekStart);
        Assert.False(first.CanGoPrevious);
        Assert.True(first.CanGoNext);
        Assert.Equal(new DateOnly(2026, 12, 21), last.WeekStart);
        Assert.Equal(End, last.WeekEnd);
        Assert.True(last.CanGoPrevious);
        Assert.False(last.CanGoNext);
        Assert.Equal(first.Occurrences.OrderBy(item => item.Date).ThenBy(item => item.Meeting.StartPeriod), first.Occurrences);
    }

    [Fact]
    public void EmptyStudentHasNoPrivateClassesOrCredits()
    {
        var result = StudentTimetable.Create(SchedulingDemo.Create(), [new("OTHER", "LHP0001")], "S", "HK1-2627", Start);
        Assert.Empty(result.Classes);
        Assert.Empty(result.Occurrences);
        Assert.Equal(0, result.CourseCount);
        Assert.Equal(0, result.TotalCredits);
        Assert.Equal(0, result.UnscheduledClasses);
    }

    [Fact]
    public void RejectsInvalidBoundaryInputs()
    {
        var store = SchedulingDemo.Create();
        Assert.Throws<ArgumentNullException>(() => StudentTimetable.Create(null!, [], "S", "HK1", Start));
        Assert.Throws<ArgumentNullException>(() => StudentTimetable.Create(store, null!, "S", "HK1", Start));
        Assert.Throws<ArgumentException>(() => StudentTimetable.Create(store, [], " ", "HK1", Start));
        Assert.Throws<ArgumentException>(() => StudentTimetable.Create(store, [], "S", " ", Start));
        Assert.Throws<ArgumentException>(() => StudentTimetable.Create(store, [], "S", "missing", Start));
        Assert.Throws<ArgumentException>(() => StudentTimetable.Create(store, [new("", "LHP0001")], "S", "HK1-2627", Start));
        Assert.Throws<ArgumentException>(() => StudentTimetable.Create(store, [null!], "S", "HK1-2627", Start));
    }
}
