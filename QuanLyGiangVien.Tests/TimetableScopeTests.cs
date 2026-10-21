using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class TimetableScopeTests
{
    private static readonly TimetableDirectory Directory = new(
        [new("SV1", "An", "D1", "Đại học 1"), new("SV2", "Bình", "D1", "Đại học 1"),
            new("SV3", "Chi", "D2", "Đại học 2")],
        [new("SV1", "A"), new("SV1", "B", true), new("SV2", "B"), new("SV2", "A"), new("SV3", "C")]);
    private static readonly CourseClass[] Classes = [Class("A"), Class("B", "GV0002") with {
        Meetings = [Meeting("A202", from: 4, to: 6)] }, Class("C", "GV0003") with {
        Meetings = [Meeting("B101", from: 7, to: 9)] }];
    private static ScheduledOccurrence Session(CourseClass item, int offset = 0) => new(Start.AddDays(offset), item, item.Meetings[0]);
    private static IReadOnlyList<ScheduledOccurrence> Sessions => Classes.Select(item => Session(item)).ToArray();

    [Theory]
    [InlineData(TimetableScopeKind.Lecturer)]
    [InlineData(TimetableScopeKind.Student)]
    [InlineData(TimetableScopeKind.CourseClass)]
    [InlineData(TimetableScopeKind.StudentClass)]
    [InlineData(TimetableScopeKind.Room)]
    public void NoSelectionOrUnknownSubjectNeverFallsBackToAll(TimetableScopeKind kind)
    {
        foreach (var id in new string?[] { null, "", " ", "UNKNOWN" })
            Assert.Empty(TimetableScope.Select(Store(Classes), Sessions, Directory, kind, id));
    }

    [Theory]
    [InlineData(TimetableScopeKind.Student, " sv1 ", "A")]
    [InlineData(TimetableScopeKind.CourseClass, "a", "A")]
    [InlineData(TimetableScopeKind.Room, "a202", "B")]
    [InlineData(TimetableScopeKind.Lecturer, "gv0002", "B")]
    public void SelectedSubjectUsesExactNormalizedIdentity(TimetableScopeKind kind, string id, string expected) =>
        Assert.Equal(expected, Assert.Single(TimetableScope.Select(Store(Classes), Sessions, Directory, kind, id)).Class.Id);

    [Fact]
    public void LecturerUsesActualSubstituteInsteadOfClassOwner()
    {
        var sessions = new[] { Session(Classes[0]) with { ActualLecturerId = "GV0002" } };
        Assert.Empty(TimetableScope.Select(Store(Classes), sessions, Directory, TimetableScopeKind.Lecturer, "GV0001"));
        Assert.Single(TimetableScope.Select(Store(Classes), sessions, Directory, TimetableScopeKind.Lecturer, "GV0002"));
    }

    [Fact]
    public void StudentClassUnionsActiveRegistrationsAcrossMembersAndDeduplicatesOccurrences()
    {
        var sessions = Sessions.Append(Sessions[0]).ToArray();
        var selected = TimetableScope.Select(Store(Classes), sessions, Directory, TimetableScopeKind.StudentClass, "D1");
        Assert.Equal(new[] { "A", "B" }, selected.Select(item => item.Class.Id));
    }

    [Fact]
    public void UnregisteredStudentAndEmptyDirectoryAreEmpty()
    {
        var directory = Directory with { Students = Directory.Students.Add(new("SV4", "Dung", "D3", "Lớp 3")) };
        Assert.Empty(TimetableScope.Select(Store(Classes), Sessions, directory, TimetableScopeKind.Student, "SV4"));
        Assert.Empty(TimetableScope.Select(Store(Classes), Sessions, new([], []), TimetableScopeKind.Student, "SV1"));
    }

    [Fact]
    public void StaleRegistrationsAndForeignSessionClassesCannotLeakIntoScope()
    {
        var foreign = Class("FOREIGN");
        var directory = Directory with { Registrations = Directory.Registrations.Add(new("SV1", foreign.Id)) };
        var sessions = Sessions.Append(Session(foreign)).ToArray();
        var selected = TimetableScope.Select(Store(Classes), sessions, directory, TimetableScopeKind.Student, "SV1");
        Assert.Equal("A", Assert.Single(selected).Class.Id);
    }

    [Fact]
    public void DistinctDatesAreRetainedAndOnlyPassedOccurrencesAreSelected()
    {
        var sessions = new[] { Session(Classes[0]), Session(Classes[0], 7) };
        Assert.Equal(2, TimetableScope.Select(Store(Classes), sessions, Directory, TimetableScopeKind.Student, "SV1").Count);
    }

    [Fact]
    public void TenConcurrentClassesProduceOneDisplayGroupWithoutIncreasingColumnWidth()
    {
        var sessions = Enumerable.Range(0, 10).Select(i => Session(Class($"L{i}"))).ToArray();
        var group = Assert.Single(TimetableScope.GroupForDisplay(sessions));
        Assert.Equal(Start, group.Date);
        Assert.Equal(1, group.StartPeriod);
        Assert.Equal(3, group.EndPeriod);
        Assert.Equal(Enumerable.Range(0, 10), group.SessionIndexes);
    }

    [Fact]
    public void ConnectedInclusiveOverlapIsOneGroupButAfternoonAndOtherDatesStaySeparate()
    {
        var sessions = new[] { Session(Classes[0]), new ScheduledOccurrence(Start, Classes[1], Meeting(from: 3, to: 5)),
            new ScheduledOccurrence(Start, Classes[2], Meeting(from: 5, to: 7)),
            new ScheduledOccurrence(Start, Classes[0], Meeting(from: 8, to: 10)), Session(Classes[0], 7) };
        var groups = TimetableScope.GroupForDisplay(sessions);
        Assert.Equal(3, groups.Count);
        Assert.Equal(new[] { 0, 1, 2 }, groups[0].SessionIndexes);
        Assert.Equal(7, groups[0].EndPeriod);
        Assert.Equal(new[] { 3 }, groups[1].SessionIndexes);
        Assert.Equal(new[] { 4 }, groups[2].SessionIndexes);
    }

    [Fact]
    public void GroupsAreSortedByDateThenPeriodAndIndexesReferToOriginalInput()
    {
        var sessions = new[] { Session(Classes[1], 1), Session(Classes[2]), Session(Classes[0]) };
        Assert.Equal(new[] { 2, 1, 0 }, TimetableScope.GroupForDisplay(sessions).SelectMany(group => group.SessionIndexes));
    }

    [Fact]
    public void EmptySessionsAreEmptyAndInvalidPeriodsFailClearly()
    {
        Assert.Empty(TimetableScope.GroupForDisplay([]));
        Assert.Throws<ArgumentNullException>(() => TimetableScope.GroupForDisplay(null!));
        Assert.Throws<ArgumentException>(() => TimetableScope.GroupForDisplay([new(Start, Classes[0], Meeting(from: 0))]));
        Assert.Throws<ArgumentException>(() => TimetableScope.GroupForDisplay([new(default, Classes[0], Meeting())]));
    }

    [Fact]
    public void InvalidScopeAndNullInputsFailClearly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimetableScope.Select(Store(Classes), Sessions, Directory, (TimetableScopeKind)99, "A"));
        Assert.Throws<ArgumentNullException>(() => TimetableScope.Select(null!, Sessions, Directory, TimetableScopeKind.Student, "SV1"));
        Assert.Throws<ArgumentNullException>(() => TimetableScope.Select(Store(Classes), null!, Directory, TimetableScopeKind.Student, "SV1"));
        Assert.Throws<ArgumentNullException>(() => TimetableScope.Select(Store(Classes), Sessions, null!, TimetableScopeKind.Student, "SV1"));
    }
}
