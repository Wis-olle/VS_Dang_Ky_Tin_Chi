using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Registration;
using Xunit;
namespace QuanLyGiangVien.Tests;
public sealed class SchedulePlannerTests
{
 private static readonly DateOnly Monday=new(2026,11,2);
 private static SchedulePlannerClass C(string id,string course,int day,int start,int end,bool available=true,bool own=false,string? term=null)=>new(id,course,course,3,term??"HK1",available,own,true,[new(id,Monday.AddDays(day),start,end)]);
 private static SchedulePlannerSnapshot S(params SchedulePlannerClass[] classes)=>new("HK1",classes.ToImmutableArray(),[],null);
 private static SchedulePlannerRequest R(params string[] courses)=>new(courses.ToImmutableArray(),[],[],new(),MaxNodes:10000,MaxMilliseconds:3000,MaxOptions:5);
 [Fact]public void ProducesSeveralDeterministicRankedFeasiblePlans()
 {
  var data=S(C("A1","A",0,1,3),C("A2","A",1,1,3),C("B1","B",0,5,6),C("B2","B",2,1,3));
  var first=SchedulePlanner.Generate(data,R("A","B"));var second=SchedulePlanner.Generate(data,R("B","A"));
  Assert.Equal(4,first.Options.Length);Assert.Equal(new[]{"A1","B1"},first.Options[0].AddClassIds.ToArray());
  Assert.Equal(first.Options.Select(p=>p.Key),second.Options.Select(p=>p.Key));Assert.All(first.Options,p=>Assert.Equal(6,p.TotalCredits));
 }
 [Fact]public void ExactComponentsAreChosenTogetherAndCreditsCountOnce()
 {
  var data=S(C("LT","A",0,1,3),C("TH","A",1,1,3),C("TL","A",2,1,3)) with{Bundles=[new("COMBO","A",["LT","TH"],["LT","TH"]) ]};
  var result=SchedulePlanner.Generate(data,R("A"));Assert.Single(result.Options);Assert.Equal(new[]{"LT","TH"},result.Options[0].AddClassIds.ToArray());Assert.Equal(3,result.Options[0].TotalCredits);
 }
 [Fact]public void OneUnavailableOrConflictingComponentExplainsNoOption()
 {
  var data=S(C("LT","A",0,1,3),C("TH","A",0,2,4)) with{Bundles=[new("COMBO","A",["LT","TH"],["LT","TH"])]};
  var result=SchedulePlanner.Generate(data,R("A"));Assert.Empty(result.Options);Assert.Contains(result.Explanations,v=>v.Contains("trùng"));
  result=SchedulePlanner.Generate(data with{Classes=[data.Classes[0],data.Classes[1] with{CanAdd=false}]},R("A"));Assert.Empty(result.Options);
 }
 [Fact]public void BusyTimesAndExistingCrossTermClassesAreRespectedUsingActualDates()
 {
  var data=S(C("OLD","OLD",0,1,3,own:true,term:"HK0"),C("A1","A",0,1,3),C("A2","A",1,1,3),C("A3","A",2,1,3));
  var request=R("A") with{BusyTimes=[new(Monday,Monday.AddDays(6),DayOfWeek.Tuesday,1,3)]};
  var result=SchedulePlanner.Generate(data,request);Assert.Single(result.Options);Assert.Equal(new[]{"A3"},result.Options[0].AddClassIds.ToArray());
 }
 [Fact]public void FixedClassSelectsOnlyMatchingBundleAndExistingCourseIsRetained()
 {
  var data=S(C("A1","A",0,1,3),C("A2","A",1,1,3),C("OLD","B",2,1,3,available:false,own:true));
  var result=SchedulePlanner.Generate(data,R("A","B") with{FixedClassIds=["A2"]});Assert.Single(result.Options);Assert.Equal(new[]{"A2"},result.Options[0].AddClassIds.ToArray());Assert.Contains("OLD",result.Options[0].AllClassIds);Assert.Equal(6,result.Options[0].TotalCredits);
 }
 [Fact]public void NodeBoundAndCancellationDoNotPretendSearchWasExhaustive()
 {
  var data=S(C("A1","A",0,1,3),C("A2","A",1,1,3),C("B1","B",2,1,3),C("B2","B",3,1,3));
  var result=SchedulePlanner.Generate(data,R("A","B") with{MaxNodes=1});Assert.True(result.Truncated);Assert.True(result.ExploredNodes<=1);
  using var canceled=new CancellationTokenSource();canceled.Cancel();Assert.Throws<OperationCanceledException>(()=>SchedulePlanner.Generate(data,R("A"),canceled.Token));
 }
 [Fact]public void InvalidInputAndMissingCoursesFailClearly()
 {
  Assert.Throws<ArgumentException>(()=>SchedulePlanner.Generate(S(C("A1","A",0,1,3)),R("A") with{MaxOptions=0}));
  Assert.Throws<ArgumentException>(()=>SchedulePlanner.Generate(S(),R("A") with{BusyTimes=[new(Monday,Monday.AddDays(-1),DayOfWeek.Monday,1,3)]}));
  var result=SchedulePlanner.Generate(S(),R("A"));Assert.Empty(result.Options);Assert.Contains(result.Explanations,v=>v.Contains("A"));
 }
 [Fact]public void DifferentActualDatesDoNotCreateRecurringCalendarFalseConflicts()
 {
  var data=S(C("A","A",0,1,3),C("B","B",7,1,3));Assert.Single(SchedulePlanner.Generate(data,R("A","B")).Options);
 }
 [Fact]public void TimePreferencesAndOutputBoundAreAppliedWithoutMutatingSnapshot()
 {
  var data=S(C("EARLY","A",0,1,3),C("LATE","A",0,10,12));
  var request=R("A") with{MaxOptions=1,Preferences=new(false,false,ScheduleTimePreference.Early)};
  Assert.Equal("EARLY",Assert.Single(SchedulePlanner.Generate(data,request).Options).AddClassIds[0]);
  Assert.Equal("LATE",Assert.Single(SchedulePlanner.Generate(data,request with{Preferences=new(false,false,ScheduleTimePreference.Late)}).Options).AddClassIds[0]);
  Assert.Equal(new[]{"EARLY","LATE"},data.Classes.Select(c=>c.Id));
 }
 [Fact]public void UnknownRoleOrDuplicateComponentNeverCreatesAFeasibleBundle()
 {
  var data=S(C("LT","A",0,1,3),C("TH","A",1,1,3)) with{Bundles=[new("BAD","A",["LT","TH"],["LT","UNKNOWN"])]};
  Assert.Empty(SchedulePlanner.Generate(data,R("A")).Options);
  Assert.Empty(SchedulePlanner.Generate(data with{Bundles=[new("BAD","A",["LT","LT"],["LT","TH"])]},R("A")).Options);
 }
}
