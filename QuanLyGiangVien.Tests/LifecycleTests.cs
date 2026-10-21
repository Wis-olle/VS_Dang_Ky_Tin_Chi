using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;
namespace QuanLyGiangVien.Tests;
public sealed class LifecycleTests
{
    [Theory]
    [InlineData("REGISTRATION","CLOSED",true)]
    [InlineData("CLOSED","TEACHING",true)]
    [InlineData("TEACHING","COMPLETED",true)]
    [InlineData("COMPLETED","ARCHIVED",true)]
    [InlineData("COMPLETED","REGISTRATION",false)]
    [InlineData("ARCHIVED","TEACHING",false)]
    [InlineData("TEACHING","REGISTRATION",false)]
    public void LifecycleTransitionsAreExplicit(string before,string after,bool allowed) =>
        Assert.Equal(allowed,LifecycleRules.CanTransition(before,after));
    [Fact]
    public void ActualCalendarOverridesRecurringCalendarForEveryProjection()
    {
        var source=Class() with { ActualMeetings=[new(new(2026,9,8),"A202","GV0002",7,9)] };
        var store=Store(source);
        var date=new DateOnly(2026,9,8);
        var actual=Assert.Single(SchedulingQuery.Occurrences(store,date,date));
        Assert.Equal("A202",actual.RoomId);
        Assert.Equal("GV0002",actual.LecturerId);
        Assert.Empty(SchedulingQuery.Occurrences(store,date,date,lecturerId:"GV0001"));
        Assert.Single(SchedulingQuery.Occurrences(store,date,date,lecturerId:"GV0002"));
        Assert.Empty(SchedulingQuery.Occurrences(store,date.AddDays(-1),date.AddDays(-1)));
    }
    [Fact]
    public void ExplicitEmptyActualCalendarMeansHolidayWithoutFallback()
    {
        var store=Store(Class() with {ActualMeetings=[]});
        Assert.Empty(SchedulingQuery.Occurrences(store,new(2026,1,1),new(2027,1,1)));
    }
    [Fact]
    public void CompletedClassPreservesCalendarAndReleasesRoom()
    {
        var completed=Class() with {Lifecycle="COMPLETED"};
        var store=Store(completed);
        Assert.Contains(SchedulingQuery.AvailableRooms(store,completed.Meetings[0],30),r=>r.Id==completed.Meetings[0].RoomId);
        Assert.False(LifecycleRules.BlocksResources(completed));
        Assert.NotEmpty(SchedulingQuery.Occurrences(store,completed.Meetings[0].StartDate,completed.Meetings[0].EndDate));
    }
    [Fact]
    public void ScheduleChangeRejectsInvalidReasonDateAndTime()
    {
        Assert.Throws<ArgumentException>(()=>LifecycleRules.Validate(new("LHP1","REPLACE",new(2026,9,7),new(2026,9,8),"P1","GV1",0,2,"0011223344556677","x")));
    }
    [Theory]
    [InlineData("bad")]
    [InlineData("null")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    public void InvalidActualCalendarFailsClosed(string json) =>
        Assert.Throws<QuanLyGiangVien.Domain.PersistenceException>(()=>LifecycleRules.ReadActualCalendar(json));
    [Fact]
    public void MissingCalendarPreservesLegacyButExplicitEmptyDoesNot()
    {
        Assert.True(LifecycleRules.ReadActualCalendar(null).IsDefault);
        Assert.False(LifecycleRules.ReadActualCalendar("[]").IsDefault);
    }
    [Theory]
    [InlineData("COMPLETED",false)]
    [InlineData("CANCELED",false)]
    [InlineData("ARCHIVED",false)]
    [InlineData("TEACHING",true)]
    [InlineData("CLOSED",true)]
    [InlineData("REGISTRATION",true)]
    public void ResourceReservationFollowsLifecycle(string lifecycle,bool expected) =>
        Assert.Equal(expected,LifecycleRules.BlocksResources(Class() with {Lifecycle=lifecycle}));
    [Fact]
    public void MovedClassCanReleaseOriginalRoomAndKeepsSubstituteRoomReserved()
    {
        var store=Store(Class() with {ActualMeetings=[new(new(2026,9,8),"A202","GV0002",7,9)]});
        Assert.True(store.TrySaveRoom(Room() with {Status=SchedulingLabels.Maintenance},false,out _));
        Assert.False(store.TrySaveRoom(Room("A202") with {Status=SchedulingLabels.Maintenance},false,out _));
    }
    [Fact]
    public void SubstituteLecturerCannotTakeLeaveWhileAssigned()
    {
        var store=Store(Class() with {ActualMeetings=[new(new(2026,9,8),"A202","GV0002",7,9)]});
        var teacher=QuanLyGiangVien.Data.DemoData.Create().First(l=>l.Id=="GV0002");
        Assert.NotNull(SchedulingLecturerGuard.ValidationError(store,teacher with {Status="Tạm nghỉ"},false));
    }

    [Theory]
    [InlineData("MAKEUP")]
    [InlineData("REPLACE")]
    [InlineData("CANCEL")]
    public void ValidOperationalChangesPassBoundaryValidation(string kind) =>
        LifecycleRules.Validate(new("LHP1",kind,new(2026,11,2),new(2026,11,3),"P1","GV1",1,3,"0011223344556677","Lý do thay đổi",10));
    [Fact]
    public void NonMakeupRequiresAnOriginalOccurrence() => Assert.Throws<ArgumentException>(()=>
        LifecycleRules.Validate(new("LHP1","REPLACE",null,new(2026,11,3),"P1","GV1",1,3,"0011223344556677","Lý do thay đổi")));

}
