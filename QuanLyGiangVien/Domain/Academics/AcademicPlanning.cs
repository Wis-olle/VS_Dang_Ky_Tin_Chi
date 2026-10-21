using System.Collections.Immutable;
namespace QuanLyGiangVien.Domain.Academics;
public static class AcademicPlanning
{
    public static ImmutableArray<AcademicPlanningIssue> Check(IEnumerable<AcademicPlanItem> source,IEnumerable<AcademicPlanningTerm> terms,
        IEnumerable<AcademicPlanningRule> requirements,IEnumerable<DegreeEvidence> evidence,IEnumerable<AcademicOffering> offerings,
        IEnumerable<DegreeCourse> curriculum,IEnumerable<DegreeSubstitution> substitutions)
    {
        var items=source.ToArray();var dates=terms.ToDictionary(t=>t.Id);var facts=evidence.ToArray();var available=offerings.ToHashSet();
        var courses=curriculum.ToArray();var replacements=substitutions.ToArray();var issues=ImmutableArray.CreateBuilder<AcademicPlanningIssue>();
        bool Matches(string a,string b)=>a==b||courses.Any(x=>x.Course==a&&x.Equivalence is not null&&courses.Any(y=>y.Course==b&&y.Equivalence==x.Equivalence))
            ||replacements.Any(s=>s.SourceCourse==a&&s.TargetCourse==b);
        foreach(var item in items)
        {
            if(!dates.TryGetValue(item.Term,out var target)){issues.Add(new(item.Course,"TERM_UNKNOWN",true,"Chưa có lịch học kỳ."));continue;}
            if(!available.Contains(new(item.Course,item.Term)))issues.Add(new(item.Course,"OFFERING_UNKNOWN",true,"Chưa có căn cứ dự kiến mở môn trong kỳ này."));
            foreach(var group in requirements.Where(r=>r.Course==item.Course).GroupBy(r=>r.Group))
            {
                bool Official(AcademicPlanningRule r)=>facts.Any(f=>f.Stage=="DONE"&&Matches(f.Course,r.RequiredCourse)
                    &&(f.Grade is null?r.MinGrade==0&&f.Passed:f.Grade>=r.MinGrade&&(r.Kind=="PRIOR"||f.Passed))
                    &&dates.TryGetValue(f.Term??"",out var old)&&(r.Kind=="CO"||old.Start<target.Start));
                bool Conditional(AcademicPlanningRule r)=>items.Any(p=>Matches(p.Course,r.RequiredCourse)&&dates.TryGetValue(p.Term,out var prior)
                    &&(r.Kind=="CO"?prior.Start<=target.Start:prior.End<target.Start))
                    ||facts.Any(f=>f.Stage=="IN_PROGRESS"&&Matches(f.Course,r.RequiredCourse)&&dates.TryGetValue(f.Term??"",out var prior)
                    &&(r.Kind=="CO"?prior.Start<=target.Start:prior.End<target.Start));
                if(group.Any(Official))continue;
                if(group.Any(Conditional))issues.Add(new(item.Course,"CONDITIONAL_RESULT",false,$"Nhóm điều kiện {group.Key}: cần đạt/kết thúc môn đã xếp ở kỳ trước hoặc đồng hành; kết quả tương lai chưa được xác nhận."));
                else issues.Add(new(item.Course,"REQUIREMENT",true,$"Thiếu nhóm điều kiện {group.Key}: {string.Join(" hoặc ",group.Select(r=>r.Kind+" "+r.RequiredCourse))}."));
            }
        }
        return issues.ToImmutable();
    }
}
