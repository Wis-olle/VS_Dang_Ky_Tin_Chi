using System.Collections.Immutable;
using System.Numerics;
namespace QuanLyGiangVien.Domain.Academics;
/// <summary>Conserves each recognized credit across all requirements; allocations remain inspectable.</summary>
public static class DegreeAudit
{
    public static DegreeAuditResult Calculate(DegreeCurriculum? curriculum,IEnumerable<DegreeEvidence> evidence,
        IEnumerable<DegreeSubstitution> substitutions)
    {
        if(curriculum is null)return new(false,["MISSING_PROGRAM"],[],[],[]);
        Validate(curriculum);
        var sources=evidence.ToArray();var replacements=substitutions.ToArray();
        if(sources.Length>2000||sources.Any(s=>s.Credits is <1 or >30||s.Stage is not("DONE" or "IN_PROGRESS" or "PLANNED")))
            throw new ArgumentException("Nguồn tín chỉ hoặc trạng thái tiến độ không hợp lệ.");
        var canonical=Canonical(curriculum.Courses);
        string Key(string c)=>canonical.GetValueOrDefault(AcademicValidation.Code(c),AcademicValidation.Code(c));
        // Alternatives in a published equivalence group are one curriculum requirement.
        curriculum=curriculum with {Courses=curriculum.Courses.GroupBy(c=>(Key(c.Course),c.Group))
            .Select(g=>g.OrderBy(c=>c.Id,StringComparer.Ordinal).First() with {Credits=g.Max(c=>c.Credits),Required=g.Any(c=>c.Required)})
            .OrderBy(c=>c.Id,StringComparer.Ordinal).ToImmutableArray()};
        var buckets=sources.Where(s=>s.Stage!="DONE"||s.Passed).GroupBy(s=>Key(s.Course))
            .Select(g=>g.OrderBy(s=>Stage(s.Stage)).ThenByDescending(s=>s.Credits).ThenBy(s=>s.Course,StringComparer.Ordinal).First())
            .OrderBy(s=>Stage(s.Stage)).ThenBy(s=>Key(s.Course),StringComparer.Ordinal).ToArray();
        var done=Allocate(curriculum,buckets.Where(b=>b.Stage=="DONE").ToArray(),replacements,Key,[]);
        // Once DONE proves graduation, future study cannot invalidate that proof. Keep its allocation,
        // while still counting future credits in remaining curriculum slots. Otherwise one shared
        // residual graph may reroute earlier evidence to accommodate later evidence (A03).
        var allocations=Satisfied(curriculum,done)?Allocate(curriculum,buckets.Where(b=>b.Stage!="DONE").ToArray(),replacements,Key,done)
            :Allocate(curriculum,buckets,replacements,Key,[]);
        int Sum(string id,string stage)=>allocations.Where(a=>a.TargetId==id&&a.Stage==stage).Sum(a=>a.Credits);
        int GroupSum(string id,string stage)=>allocations.Where(a=>a.Group==id&&a.Stage==stage).Sum(a=>a.Credits);
        var rows=curriculum.Courses.Select(c=>new DegreeCourseProgress(c,Sum(c.Id,"DONE"),Sum(c.Id,"IN_PROGRESS"),Sum(c.Id,"PLANNED"))).ToImmutableArray();
        var progress=curriculum.Groups.Select(g=>new DegreeGroupProgress(g.Id,g.Name,g.RequiredCredits,
            GroupSum(g.Id,"DONE"),GroupSum(g.Id,"IN_PROGRESS"),GroupSum(g.Id,"PLANNED"))).ToImmutableArray();
        return new(Satisfied(curriculum,allocations),curriculum.Published?[]:["UNPUBLISHED_PROGRAM"],rows,progress,allocations);
    }
    private static ImmutableArray<DegreeCreditAllocation> Allocate(DegreeCurriculum curriculum,DegreeEvidence[] buckets,
        DegreeSubstitution[] replacements,Func<string,string> key,ImmutableArray<DegreeCreditAllocation> locked)
    {
        var groups=curriculum.Groups.Append(new DegreeGroup("_GENERAL","Ngoài nhóm",0)).OrderBy(g=>g.Id,StringComparer.Ordinal).ToArray();
        int groupStart=1+buckets.Length+curriculum.Courses.Length;
        var flow=new DegreeCreditFlow(groupStart+groups.Length+1);int sink=flow.Count-1;
        var groupNodes=groups.Select((g,i)=>(g.Id,Node:groupStart+i)).ToDictionary(x=>x.Id,x=>x.Node);
        // A group requirement is a minimum, never a maximum. Reward its unmet minimum,
        // then allow remaining curriculum credits through an overflow edge.
        int total=curriculum.Courses.Sum(c=>c.Credits);BigInteger radix=total+1;
        foreach(var g in groups)
        {
            int remaining=Math.Max(0,g.RequiredCredits-locked.Where(a=>a.Group==g.Id).Sum(a=>a.Credits));
            flow.Add(groupNodes[g.Id],sink,remaining,-1);flow.Add(groupNodes[g.Id],sink,total,0);
        }
        var links=new List<(DegreeEvidence Source,int Target,DegreeCreditFlow.Edge Edge)>();
        for(int j=0;j<curriculum.Courses.Length;j++)
        {
            var target=curriculum.Courses[j];int node=1+buckets.Length+j;
            flow.Add(node,groupNodes[target.Group??"_GENERAL"],Math.Max(0,target.Credits-locked.Where(a=>a.TargetId==target.Id).Sum(a=>a.Credits)),0);
            for(int i=0;i<buckets.Length;i++)
                if(key(buckets[i].Course)==key(target.Course)||replacements.Any(r=>key(r.SourceCourse)==key(buckets[i].Course)&&key(r.TargetCourse)==key(target.Course)))
                    links.Add((buckets[i],j,flow.Add(1+i,node,Math.Min(buckets[i].Credits,target.Credits),
                        target.Required?-BigInteger.Pow(radix,5-2*Stage(buckets[i].Stage)):0)));
        }
        // Each radix digit dominates all lower criteria (at most total credits). BigInteger
        // avoids overflow at the validated 500-course / 2,000-evidence bounds.
        for(int i=0;i<buckets.Length;i++)flow.Add(0,1+i,buckets[i].Credits,-BigInteger.Pow(radix,6-2*Stage(buckets[i].Stage)));
        flow.Run(0,sink);
        return locked.AddRange(links.Where(l=>l.Edge.Used>0).Select(l=>new DegreeCreditAllocation(l.Source.Course,
            curriculum.Courses[l.Target].Id,curriculum.Courses[l.Target].Course,curriculum.Courses[l.Target].Group??"_GENERAL",l.Source.Stage,l.Edge.Used)))
            .OrderBy(a=>Stage(a.Stage)).ThenBy(a=>a.SourceCourse,StringComparer.Ordinal).ThenBy(a=>a.TargetId,StringComparer.Ordinal).ToImmutableArray();
    }
    private static bool Satisfied(DegreeCurriculum curriculum,ImmutableArray<DegreeCreditAllocation> allocations)
    {
        var done=allocations.Where(a=>a.Stage=="DONE").ToArray();
        return curriculum.Published&&curriculum.Courses.Where(c=>c.Required).All(c=>done.Where(a=>a.TargetId==c.Id).Sum(a=>a.Credits)>=c.Credits)
            &&curriculum.Groups.All(g=>done.Where(a=>a.Group==g.Id).Sum(a=>a.Credits)>=g.RequiredCredits)
            &&done.Sum(a=>a.Credits)>=curriculum.RequiredCredits;
    }
    private static int Stage(string stage)=>stage=="DONE"?0:stage=="IN_PROGRESS"?1:2;
    private static Dictionary<string,string> Canonical(ImmutableArray<DegreeCourse> courses)
    {
        var result=courses.Select(c=>c.Course).Distinct().ToDictionary(c=>c,c=>c,StringComparer.Ordinal);
        foreach(var group in courses.Where(c=>!string.IsNullOrWhiteSpace(c.Equivalence)).GroupBy(c=>c.Equivalence))
        {var key=group.Select(c=>c.Course).Min(StringComparer.Ordinal)!;foreach(var c in group)result[c.Course]=key;}
        return result;
    }
    private static void Validate(DegreeCurriculum c)
    {
        if(c.RequiredCredits is <1 or >1000||c.Courses.Length>500||c.Groups.Length>100||c.Groups.Any(g=>g.Id=="_GENERAL"||g.RequiredCredits is <0 or >1000)
            ||c.Groups.Select(g=>g.Id).Distinct().Count()!=c.Groups.Length||c.Courses.Select(r=>r.Id).Distinct().Count()!=c.Courses.Length
            ||c.Courses.Any(r=>r.Credits is <1 or >30||r.Group is not null&&!c.Groups.Any(g=>g.Id==r.Group)))
            throw new ArgumentException("Cấu trúc CTĐT không hợp lệ.");
    }
}
