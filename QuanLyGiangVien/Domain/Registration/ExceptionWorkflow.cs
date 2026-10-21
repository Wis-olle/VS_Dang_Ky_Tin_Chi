using System.Text.RegularExpressions;

namespace QuanLyGiangVien.Domain.Registration;

public sealed record ExceptionApplication(string Kind, string Term, string? Course, string? ClassId,
    string? RuleCode, string Reason);

public sealed record ExceptionDecision(long Id, string Version, string Decision, string Reason,
    DateTime? ExpiresUtc = null, int? Uses = null, int? MaxCredits = null);

public static class ExceptionWorkflowValidation
{
    public static IReadOnlyList<string> Kinds { get; } = Array.AsReadOnly(new[]
        { "MAX_CREDITS", "REQUIREMENT", "LATE_REGISTER", "WITHDRAW" });

    public static void Application(ExceptionApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (!Kinds.Contains(application.Kind)) throw new ArgumentException("Loại ngoại lệ không hợp lệ.");
        Code(application.Term);
        if (application.Course is not null) Code(application.Course);
        if (application.ClassId is not null) Code(application.ClassId);
        Reason(application.Reason);
        if (application.Kind == "MAX_CREDITS" && (application.Course is not null || application.ClassId is not null || application.RuleCode is not null))
            throw new ArgumentException("Ngoại lệ tín chỉ chỉ có phạm vi sinh viên/học kỳ.");
        if (application.Kind == "REQUIREMENT" && (application.Course is null || application.RuleCode is null ||
            !Regex.IsMatch(application.RuleCode, "^GROUP_([1-9][0-9]{0,2}|1000)$")))
            throw new ArgumentException("Ngoại lệ điều kiện cần mã học phần và nhóm điều kiện GROUP_1–GROUP_1000.");
        if (application.Kind is "LATE_REGISTER" or "WITHDRAW" && (application.ClassId is null || application.RuleCode is not null))
            throw new ArgumentException("Đăng ký muộn/rút môn cần lớp cụ thể và không cấp quyền bỏ qua điều kiện học vụ.");
    }

    public static void Decision(ExceptionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Id <= 0 || decision.Version is null || decision.Version.Length != 16 || !decision.Version.All(Uri.IsHexDigit))
            throw new ArgumentException("Mã đơn hoặc phiên bản không hợp lệ. Hãy tải lại danh sách.");
        if (decision.Decision is not ("APPROVE" or "REJECT" or "REVOKE")) throw new ArgumentException("Quyết định không hợp lệ.");
        Reason(decision.Reason);
        if (decision.Decision == "APPROVE")
        {
            if (decision.ExpiresUtc is null || decision.ExpiresUtc.Value.Kind != DateTimeKind.Utc || decision.Uses is not (>= 1 and <= 100) || decision.MaxCredits is < 1 or > 1000)
                throw new ArgumentException("Phê duyệt cần hạn UTC và 1–100 lượt; giới hạn tín chỉ nếu có phải từ 1–1000. Giờ hiệu lực được kiểm lại trên máy chủ.");
        }
        else if (decision.ExpiresUtc is not null || decision.Uses is not null || decision.MaxCredits is not null)
            throw new ArgumentException("Từ chối/thu hồi không cấp thêm hạn hoặc lượt sử dụng.");
    }

    private static void Code(string value)
    {
        if (!Regex.IsMatch(value, "^[A-Z0-9_-]{1,20}$")) throw new ArgumentException("Mã phải gồm 1–20 chữ ASCII hoa, số, gạch ngang hoặc gạch dưới.");
    }

    private static void Reason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 500)
            throw new ArgumentException("Lý do cần có nội dung, tối đa 500 ký tự.");
    }
}
