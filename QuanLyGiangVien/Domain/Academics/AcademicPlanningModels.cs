using System.Collections.Immutable;
namespace QuanLyGiangVien.Domain.Academics;
public sealed record AcademicPlanItem(string Course,string Term);
public sealed record AcademicPlanDraft(string Program,string SourceHash,string? Version,ImmutableArray<AcademicPlanItem> Items);
public sealed record AcademicPlanningTerm(string Id,DateOnly Start,DateOnly End);
public sealed record AcademicPlanningRule(string Course,int Group,string RequiredCourse,string Kind,decimal MinGrade);
public sealed record AcademicOffering(string Course,string Term);
public sealed record AcademicPlanningIssue(string Course,string Code,bool Blocking,string Message);
public static class AcademicPlanningValidation
{
    public static void Draft(AcademicPlanDraft draft)
    {
        AcademicValidation.Code(draft.Program);Token(draft.SourceHash,64);
        if(draft.Version is not null)Token(draft.Version,16);
        if(draft.Items.IsDefault||draft.Items.Length>120||draft.Items.Select(i=>AcademicValidation.Code(i.Course)).Distinct().Count()!=draft.Items.Length)
            throw new ArgumentException("Kế hoạch tối đa 120 môn, mỗi môn chỉ xuất hiện một lần.");
        foreach(var item in draft.Items){AcademicValidation.Code(item.Course);AcademicValidation.Code(item.Term);}
    }
    public static void Token(string value,int length)
    {if(value is null||value.Length!=length||!value.All(Uri.IsHexDigit))throw new ArgumentException("Phiên bản dữ liệu không hợp lệ. Hãy tải lại.");}
    public static void Reason(string value)
    {if(string.IsNullOrWhiteSpace(value)||value.Trim().Length is <5 or >500)throw new ArgumentException("Cần căn cứ hoặc nội dung 5–500 ký tự.");}
}
