using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Academics;
using Xunit;
namespace QuanLyGiangVien.Tests;
public sealed class AcademicPlanningTests
{
    private static readonly ImmutableArray<AcademicPlanningTerm> Terms=[new("T1",new(2027,1,1),new(2027,5,1)),new("T2",new(2027,8,1),new(2027,12,1))];
    [Fact] public void DuplicateCourseAcrossTermsIsRejected()
    {Assert.Throws<ArgumentException>(()=>AcademicPlanningValidation.Draft(new("CT1",new string('A',64),null,[new("A","T1"),new("A","T2")])));}
    [Theory][InlineData("PRE",true)][InlineData("PRIOR",true)][InlineData("CO",false)]
    public void EarlierRequiredCourseMustPrecedeTargetExceptCorequisite(string kind,bool blocked)
    {
        var issues=AcademicPlanning.Check([new("A","T1"),new("B","T1")],Terms,[new("A",1,"B",kind,0)],[],[new("A","T1"),new("B","T1")],[],[]);
        Assert.Equal(blocked,issues.Any(i=>i.Code=="REQUIREMENT"));
    }
    [Fact] public void FuturePrerequisiteIsConditionalUntilOfficialResultExists()
    {
        var issues=AcademicPlanning.Check([new("B","T1"),new("A","T2")],Terms,[new("A",1,"B","PRE",5)],[],[new("B","T1"),new("A","T2")],[],[]);
        Assert.DoesNotContain(issues,i=>i.Blocking);Assert.Contains(issues,i=>i.Code=="CONDITIONAL_RESULT");
    }
    [Fact] public void UnknownOfferingDoesNotInventFutureAvailability()
    {Assert.Contains(AcademicPlanning.Check([new("A","T1")],Terms,[],[],[],[],[]),i=>i.Code=="OFFERING_UNKNOWN"&&i.Blocking);}
    [Fact] public void PrerequisiteAlternativesUseOrWithinGroupAndAndAcrossGroups()
    {
        var rules=new[]{new AcademicPlanningRule("A",1,"B","PRE",0),new("A",1,"C","PRE",0),new("A",2,"D","CO",0)};
        var issues=AcademicPlanning.Check([new("A","T2")],Terms,rules,[new("C",3,"DONE",8,true,"T1")],[new("A","T2")],[],[]);
        Assert.Single(issues.Where(i=>i.Code=="REQUIREMENT"));
    }
}
