using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Academics;
using Xunit;
namespace QuanLyGiangVien.Tests;
public sealed class DegreeAuditTests
{
    private static DegreeCurriculum Program(params DegreeCourse[] courses)=>new("CT1","0000000000000001",6,true,
        [new("G1","Cơ sở",3),new("G2","Chuyên ngành",3)],courses.ToImmutableArray());
    [Fact] public void MissingCurriculumIsExplicitAndCannotGraduate()
    {var result=DegreeAudit.Calculate(null,[],[]);Assert.False(result.Satisfied);Assert.Contains("MISSING_PROGRAM",result.Issues);}
    [Fact] public void SharedCourseCannotProvideItsCreditsTwiceAcrossGroups()
    {
        var result=DegreeAudit.Calculate(Program(new DegreeCourse("A","A","G1",3,true),new("B","A","G2",3,true)),[new("A",3,"DONE")],[]);
        Assert.Equal(3,result.Allocations.Sum(a=>a.Credits));Assert.False(result.Satisfied);
    }
    [Fact] public void EquivalentCoursesAreTwoWayAndRepeatedResultsCountOnce()
    {
        var program=Program(new DegreeCourse("A","A","G1",3,true,"EQ"),new("B","B","G2",3,true,"EQ"));
        var result=DegreeAudit.Calculate(program,[new("A",3,"DONE"),new("B",3,"DONE"),new("A",3,"DONE")],[]);
        Assert.Equal(3,result.Allocations.Sum(a=>a.Credits));
    }
    [Fact] public void SubstitutionIsDirectional()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true));
        Assert.Equal(3,DegreeAudit.Calculate(p,[new("B",3,"DONE")],[new("B","A")]).AllocatedDone);
        Assert.Equal(0,DegreeAudit.Calculate(p,[new("B",3,"DONE")],[new("A","B")]).AllocatedDone);
    }
    [Fact] public void DoneInProgressAndPlannedRemainSeparateAndDoNotRecount()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true),new("B","B","G2",3,true));
        var r=DegreeAudit.Calculate(p,[new("A",3,"DONE"),new("A",3,"IN_PROGRESS"),new("A",3,"PLANNED"),new("B",3,"IN_PROGRESS"),new("B",3,"PLANNED")],[]);
        Assert.Equal(3,r.AllocatedDone);Assert.Equal(3,r.AllocatedInProgress);Assert.Equal(0,r.AllocatedPlanned);Assert.False(r.Satisfied);
    }
    [Fact] public void AllocationCanRerouteFlexibleEvidenceToKeepBothRequiredCourses()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true),new("B","B","G2",3,true));
        var r=DegreeAudit.Calculate(p,[new("X",3,"DONE"),new("Y",3,"DONE")],[new("X","A"),new("X","B"),new("Y","A")]);
        Assert.True(r.Satisfied);Assert.Equal(6,r.AllocatedDone);
    }
    [Fact] public void DraftProgramNeverReportsRequirementsSatisfied()
    {var p=Program(new DegreeCourse("A","A","G1",3,true)) with {Published=false};Assert.False(DegreeAudit.Calculate(p,[new("A",3,"DONE")],[]).Satisfied);}
    [Fact] public void EquivalentAlternativesInOneGroupFormOneRequirement()
    {
        var p=new DegreeCurriculum("CT1","0000000000000001",3,true,[new("G1","Nhóm",3)],
            [new("A","A","G1",3,true,"EQ"),new("B","B","G1",3,true,"EQ")]);
        var result=DegreeAudit.Calculate(p,[new("B",3,"DONE")],[]);
        Assert.True(result.Satisfied);Assert.Single(result.Courses);Assert.Equal(3,result.AllocatedDone);
    }
    [Fact] public void PublishedZeroCreditGroupRemainsReadable()
    {
        var p=new DegreeCurriculum("CT1","1",3,true,[new("G0","Bổ sung",0)],
            [new("A","A",null,3,true)]);
        var result=DegreeAudit.Calculate(p,[new("A",3,"DONE")],[]);
        Assert.True(result.Satisfied);Assert.Equal(0,Assert.Single(result.Groups).Required);
    }
    [Fact] public void GroupCreditRequirementIsMinimumAndDoesNotTruncateRequiredCourses()
    {
        var p=new DegreeCurriculum("CT1","1",6,true,[new("G1","Nhóm",3)],
            [new("A","A","G1",3,true),new("B","B","G1",3,true)]);
        var result=DegreeAudit.Calculate(p,[new("A",3,"DONE"),new("B",3,"DONE")],[]);
        Assert.True(result.Satisfied);Assert.Equal(6,result.AllocatedDone);
        Assert.Equal(6,Assert.Single(result.Groups).Done);
    }
    [Theory]
    [InlineData("DONE","IN_PROGRESS")]
    [InlineData("DONE","PLANNED")]
    [InlineData("IN_PROGRESS","PLANNED")]
    public void EarlierFlexibleEvidenceCanRerouteToFitLaterEvidence(string earlier,string later)
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true),new("B","B","G2",3,true));
        var result=DegreeAudit.Calculate(p,[new("X",3,earlier),new("Y",3,later)],
            [new("X","A"),new("X","B"),new("Y","A")]);
        Assert.Equal(6,result.Allocations.Sum(a=>a.Credits));
        Assert.Equal(3,result.Allocations.Where(a=>a.Stage==earlier).Sum(a=>a.Credits));
        Assert.Equal("B",Assert.Single(result.Allocations.Where(a=>a.Stage==earlier)).TargetCourse);
        Assert.Equal("A",Assert.Single(result.Allocations.Where(a=>a.Stage==later)).TargetCourse);
    }
    [Fact] public void EarlierEvidenceCountsAreLexicographicallyPreferred()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true),new("B","B","G2",3,true));
        var result=DegreeAudit.Calculate(p,[new("X",6,"DONE"),new("Y",3,"IN_PROGRESS"),new("Z",3,"PLANNED")],
            [new("X","A"),new("X","B"),new("Y","A"),new("Z","B")]);
        Assert.Equal(6,result.AllocatedDone);Assert.Equal(0,result.AllocatedInProgress);Assert.Equal(0,result.AllocatedPlanned);
    }
    [Fact] public void OptionalCreditsBeyondGroupMinimumCanMeetOverallMinimum()
    {
        var p=new DegreeCurriculum("CT1","1",6,true,[new("G1","Nhóm",3)],
            [new("A","A","G1",3,false),new("B","B","G1",3,false)]);
        Assert.True(DegreeAudit.Calculate(p,[new("A",3,"DONE"),new("B",3,"DONE")],[]).Satisfied);
    }
    [Fact] public void FlexibleCreditsPreferUnmetGroupMinimumOverSurplus()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,false),new("B","B","G1",3,false),new("C","C","G2",3,false));
        var result=DegreeAudit.Calculate(p,[new("A",3,"DONE"),new("X",3,"DONE")],[new("X","B"),new("X","C")]);
        Assert.True(result.Satisfied);Assert.All(result.Groups,g=>Assert.Equal(3,g.Done));
    }
    [Fact] public void LaterEvidenceCannotTakeAwayAlreadySatisfiedGraduation()
    {
        var p=new DegreeCurriculum("CT1","1",3,true,[new("G1","Bắt buộc",3),new("G0","Tự chọn",0)],
            [new("A","A","G1",3,true),new("B","B","G0",3,false)]);
        var result=DegreeAudit.Calculate(p,[new("X",3,"DONE"),new("Y",3,"IN_PROGRESS")],
            [new("X","A"),new("X","B"),new("Y","A")]);
        Assert.True(result.Satisfied);Assert.Equal("A",Assert.Single(result.Allocations).TargetCourse);
    }
    [Fact] public void LaterEvidenceCannotTakeAwaySatisfiedOptionalGroupMinimum()
    {
        var p=new DegreeCurriculum("CT1","1",3,true,[new("G1","Cần đạt",3),new("G0","Bổ sung",0)],
            [new("A","A","G1",3,false),new("B","B","G0",3,false)]);
        var result=DegreeAudit.Calculate(p,[new("X",3,"DONE"),new("Y",3,"PLANNED")],
            [new("X","A"),new("X","B"),new("Y","A")]);
        Assert.True(result.Satisfied);Assert.Equal(3,result.Groups.Single(g=>g.Id=="G1").Done);
    }
    [Fact] public void AllocationDoesNotDependOnInputEnumerationOrder()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true),new("B","B","G2",3,true));
        DegreeEvidence[] evidence=[new("X",3,"DONE"),new("Y",3,"IN_PROGRESS"),new("X",2,"DONE")];
        DegreeSubstitution[] substitutions=[new("X","A"),new("X","B"),new("Y","A")];
        var first=DegreeAudit.Calculate(p,evidence,substitutions);
        var reordered=DegreeAudit.Calculate(p with {Groups=p.Groups.Reverse().ToImmutableArray(),Courses=p.Courses.Reverse().ToImmutableArray()},
            evidence.Reverse(),substitutions.Reverse());
        Assert.Equal(first.Allocations.ToArray(),reordered.Allocations.ToArray());
    }
    [Fact] public void MaximumValidatedCreditBoundDoesNotOverflowLexicographicCosts()
    {
        var courses=Enumerable.Range(0,500).Select(i=>new DegreeCourse($"R{i:D3}",$"C{i:D3}",null,30,true)).ToImmutableArray();
        var p=new DegreeCurriculum("CT1","1",1000,true,[],courses);
        var evidence=courses.SelectMany(c=>Enumerable.Repeat(new DegreeEvidence(c.Course,30,"DONE"),4)).ToArray();
        var result=DegreeAudit.Calculate(p,evidence,[]);
        Assert.True(result.Satisfied);Assert.Equal(15000,result.AllocatedDone);
        Assert.All(result.Courses,c=>Assert.Equal(30,c.Done));
    }
    [Fact] public void FailedDoneEvidenceDoesNotHideAnInProgressAttempt()
    {
        var p=Program(new DegreeCourse("A","A","G1",3,true));
        var result=DegreeAudit.Calculate(p,[new("A",3,"DONE",Passed:false),new("A",3,"IN_PROGRESS")],[]);
        Assert.Equal(0,result.AllocatedDone);Assert.Equal(3,result.AllocatedInProgress);
    }
}
