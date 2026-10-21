using System.Collections.Immutable;
namespace QuanLyGiangVien.Domain.Academics;
public sealed record DegreeGroup(string Id,string Name,int RequiredCredits);
public sealed record DegreeCourse(string Id,string Course,string? Group,int Credits,bool Required,string? Equivalence=null);
public sealed record DegreeCurriculum(string Id,string Version,int RequiredCredits,bool Published,
    ImmutableArray<DegreeGroup> Groups,ImmutableArray<DegreeCourse> Courses);
public sealed record DegreeEvidence(string Course,int Credits,string Stage,decimal? Grade=null,bool Passed=true,string? Term=null);
public sealed record DegreeSubstitution(string SourceCourse,string TargetCourse);
public sealed record DegreeCreditAllocation(string SourceCourse,string TargetId,string TargetCourse,string Group,string Stage,int Credits);
public sealed record DegreeCourseProgress(DegreeCourse Requirement,int Done,int InProgress,int Planned);
public sealed record DegreeGroupProgress(string Id,string Name,int Required,int Done,int InProgress,int Planned);
public sealed record DegreeAuditResult(bool Satisfied,ImmutableArray<string> Issues,ImmutableArray<DegreeCourseProgress> Courses,
    ImmutableArray<DegreeGroupProgress> Groups,ImmutableArray<DegreeCreditAllocation> Allocations)
{
    public int AllocatedDone=>Allocations.Where(a=>a.Stage=="DONE").Sum(a=>a.Credits);
    public int AllocatedInProgress=>Allocations.Where(a=>a.Stage=="IN_PROGRESS").Sum(a=>a.Credits);
    public int AllocatedPlanned=>Allocations.Where(a=>a.Stage=="PLANNED").Sum(a=>a.Credits);
}
