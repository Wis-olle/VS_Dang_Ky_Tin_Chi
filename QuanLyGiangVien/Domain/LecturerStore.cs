using System.Collections.Immutable;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace QuanLyGiangVien.Domain;

public sealed class LecturerStore(IEnumerable<Lecturer> initial, ILecturerPersistence? persistence = null)
{
    private IReadOnlyList<Lecturer> items = Array.AsReadOnly(initial.ToArray());
    public IReadOnlyList<Lecturer> Items => items;

    public void SynchronizeAssignments(IEnumerable<Domain.Scheduling.CourseClass> classes)
    {
        ArgumentNullException.ThrowIfNull(classes);
        var active = classes.Where(item => item.Status != Domain.Scheduling.SchedulingLabels.Canceled).ToArray();
        items = Array.AsReadOnly(items.Select(item => item with {
            ActiveClasses = active.Count(c => Same(c.LecturerId, item.Id)) }).ToArray());
    }

    public string NextId()
    {
        var maximum = items.Select(item => Regex.Match(item.Id, @"^GV([0-9]{4})$", RegexOptions.IgnoreCase))
            .Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();
        if (maximum >= 9999)
            throw new InvalidOperationException("Đã hết mã giảng viên trong phạm vi GV0001–GV9999.");
        return $"GV{maximum + 1:0000}";
    }

    public bool TrySave(Lecturer value, bool isNew, out IReadOnlyDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = Normalize(value);
        errors = Validate(normalized, isNew);
        if (errors.Count > 0)
            return false;
        try { normalized = persistence?.SaveLecturer(normalized, isNew) ?? normalized; }
        catch (PersistenceException exception)
        {
            errors = ImmutableDictionary<string, string>.Empty.Add(
                exception.Kind == PersistenceFailure.SessionExpired ? "_session" : "_global", exception.Message);
            return false;
        }
        items = Array.AsReadOnly(isNew ? items.Append(normalized).ToArray() :
            items.Select(item => Same(item.Id, normalized.Id) ? normalized : item).ToArray());
        return true;
    }

    public bool TryDelete(string id, out string? error)
    {
        var existing = items.FirstOrDefault(item => Same(item.Id, id));
        error = existing is null ? "Không tìm thấy giảng viên." : existing.ActiveClasses > 0
            ? "Giảng viên đang được phân công lớp học. Vui lòng chuyển phân công trước khi xóa." : null;
        if (error is not null)
            return false;
        try { persistence?.DeleteLecturer(existing!.Id, existing.Version); }
        catch (PersistenceException exception) { error = exception.Message; return false; }
        items = Array.AsReadOnly(items.Where(item => !Same(item.Id, id)).ToArray());
        return true;
    }

    private IReadOnlyDictionary<string, string> Validate(Lecturer value, bool isNew)
    {
        var existing = items.FirstOrDefault(item => Same(item.Id, value.Id));
        var hasId = existing is not null;
        var today = DateTime.Today;
        var rules = new (string Key, bool Invalid, string Message)[] {
            (nameof(value.Id), !Regex.IsMatch(value.Id, @"^[A-Z0-9_-]{1,20}$"), "Mã giảng viên gồm 1–20 chữ cái ASCII, chữ số, gạch ngang hoặc gạch dưới."),
            (nameof(value.Id), isNew && hasId, "Mã giảng viên đã tồn tại."),
            (nameof(value.Id), !isNew && !hasId, "Không tìm thấy giảng viên cần cập nhật."),
            ("_global", !isNew && existing is not null && existing.Version != value.Version,
                "Dữ liệu đã thay đổi. Vui lòng đóng form, tải lại bằng F5 và mở lại."),
            (nameof(value.Name), value.Name.Length > 100 || value.Name.Count(char.IsLetter) < 2 || !Regex.IsMatch(value.Name, @"^[\p{L}\p{M}\s.'’-]+$"), "Họ tên phải hợp lệ và không quá 100 ký tự."),
            (nameof(value.BirthDate), value.BirthDate.Date > today.AddYears(-18) || value.BirthDate.Date < today.AddYears(-100), "Giảng viên phải từ 18 đến 100 tuổi."),
            (nameof(value.Gender), !new[] { "Nam", "Nữ", "Khác" }.Contains(value.Gender), "Vui lòng chọn giới tính."),
            (nameof(value.Email), value.Email.Length > 254 || value.Email.Any(character => character > 127) || !ValidEmail(value.Email), "Email phải hợp lệ, dùng ký tự ASCII và không quá 254 ký tự."),
            (nameof(value.Email), items.Any(item => Same(item.Email, value.Email) && (isNew || !Same(item.Id, value.Id))), "Email đã được sử dụng."),
            (nameof(value.Phone), !Regex.IsMatch(value.Phone, @"^0[0-9]{9,10}$"), "Số điện thoại phải có 10–11 chữ số, bắt đầu bằng 0 hoặc +84."),
            (nameof(value.Faculty), string.IsNullOrWhiteSpace(value.Faculty) || value.Faculty.Length > 100, "Khoa phải có 1–100 ký tự."),
            (nameof(value.Degree), !new[] { "Thạc sĩ", "Tiến sĩ", "PGS. Tiến sĩ", "Giáo sư" }.Contains(value.Degree), "Vui lòng chọn học vị trong danh mục."),
            (nameof(value.AvatarKey), !Regex.IsMatch(value.AvatarKey, @"^portrait-(?:0[1-9]|[1-3][0-9]|4[0-8])$"), "Vui lòng chọn ảnh đại diện từ bộ 48 ảnh có sẵn."),
            (nameof(value.Status), !new[] { "Đang giảng dạy", "Tạm nghỉ" }.Contains(value.Status), "Vui lòng chọn trạng thái."),
            (nameof(value.ActiveClasses), value.ActiveClasses < 0, "Số lớp phụ trách không được âm.")
        };
        return rules.Where(rule => rule.Invalid).GroupBy(rule => rule.Key)
            .ToImmutableDictionary(group => group.Key, group => group.First().Message);
    }

    private static Lecturer Normalize(Lecturer value)
    {
        var phone = (value.Phone ?? "").Trim();
        return value with {
            Id = (value.Id ?? "").Trim().ToUpperInvariant(), Name = (value.Name ?? "").Trim(),
            Gender = (value.Gender ?? "").Trim(), Email = (value.Email ?? "").Trim(),
            Phone = phone.StartsWith("+84", StringComparison.Ordinal) ? "0" + phone[3..] : phone,
            Faculty = (value.Faculty ?? "").Trim(), Degree = (value.Degree ?? "").Trim(),
            Status = (value.Status ?? "").Trim(), AvatarKey = (value.AvatarKey ?? "").Trim()
        };
    }

    private static bool Same(string left, string? right) => string.Equals(left, right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ValidEmail(string email) => MailAddress.TryCreate(email, out var address)
        && address.Address.Equals(email, StringComparison.OrdinalIgnoreCase)
        && email.Contains('@') && address.Host.Contains('.');
}
