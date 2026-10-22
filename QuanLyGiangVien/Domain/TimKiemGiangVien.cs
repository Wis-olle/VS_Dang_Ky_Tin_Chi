using System.Globalization;
using System.Text;

namespace QuanLyGiangVien.Domain;

public static class LecturerQuery
{
    public static IReadOnlyList<Lecturer> Filter(IEnumerable<Lecturer> items, string query, string faculty, string status)
    {
        ArgumentNullException.ThrowIfNull(items);
        var search = Fold(query);
        var allFaculties = string.IsNullOrWhiteSpace(faculty) || faculty == "Tất cả khoa";
        var allStatuses = string.IsNullOrWhiteSpace(status) || status == "Tất cả trạng thái";
        return Array.AsReadOnly(items.Where(item =>
            (allFaculties || item.Faculty.Equals(faculty, StringComparison.OrdinalIgnoreCase)) &&
            (allStatuses || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase)) &&
            (search.Length == 0 || new[] { item.Id, item.Name, item.Email, item.Phone, item.Faculty }
                .Any(field => Fold(field).Contains(search, StringComparison.Ordinal))))
            .ToArray());
    }

    private static string Fold(string? value)
    {
        var decomposed = (value ?? "").Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character)
            != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }
}
