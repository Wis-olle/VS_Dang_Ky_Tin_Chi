using System.Collections.Immutable;
using System.Text.Json;
using QuanLyGiangVien.Domain.Academics;

namespace QuanLyGiangVien.Domain.Operations;

public sealed record ImportRow(int Index, string Kind, string Id, string? Version, string Payload);
public sealed record ImportError(int Row, string Message);
public sealed record ImportBatch(Guid Id, string Source, ImmutableArray<ImportRow> Rows, string Json);

public static class DataImport
{
    public static readonly ImmutableArray<string> Kinds = ["PROGRAM", "GROUP", "COURSE", "REQUIREMENT", "ASSIGNMENT", "RESULT", "STUDENT", "CLASS"];
    public static ImportBatch Parse(string text)
    {
        var errors = Inspect(text);
        if (errors.Length > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors.Take(12).Select(e => $"Dòng {e.Row + 1}: {e.Message}")));
        using var doc = Open(text);
        var root = doc.RootElement;
        var rows = root.GetProperty("rows").EnumerateArray().Select((r, i) => new ImportRow(i, Read(r, "kind"), Read(r, "id"),
            r.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null, r.GetProperty("payload").GetRawText())).ToImmutableArray();
        return new(root.GetProperty("batchId").GetGuid(), Read(root, "source").Trim(), rows, JsonSerializer.Serialize(root));
    }
    public static ImmutableArray<ImportError> Inspect(string text)
    {
        using var doc = Open(text);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("batchId", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var guid) || guid == Guid.Empty)
            throw new ArgumentException("Tệp cần batchId UUID khác rỗng.");
        var source = Read(root, "source");
        if (string.IsNullOrWhiteSpace(source) || source.Length > 500) throw new ArgumentException("Ghi nguồn xác nhận từ 1–500 ký tự.");
        if (!root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() is < 1 or > 100)
            throw new ArgumentException("Mỗi batch cần 1–100 dòng, theo thứ tự phụ thuộc.");
        var errors = ImmutableArray.CreateBuilder<ImportError>(); var identities = new HashSet<string>(StringComparer.Ordinal); var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            try
            {
                if (row.ValueKind != JsonValueKind.Object) throw new ArgumentException("Dòng cần là một đối tượng.");
                var kind = Read(row, "kind"); var code = Read(row, "id");
                if (!Kinds.Contains(kind)) throw new ArgumentException("Loại dữ liệu không được phép nhập.");
                if (AcademicValidation.Code(code) != code) throw new ArgumentException("Mã cần viết hoa và không có khoảng trắng.");
                if (!identities.Add(kind + ":" + code)) throw new ArgumentException("Định danh bị trùng trong batch.");
                if (row.TryGetProperty("version", out var version) && version.ValueKind != JsonValueKind.Null &&
                    (version.ValueKind != JsonValueKind.String || version.GetString() is not { } value || value.Length != (kind == "STUDENT" ? 64 : 16) || !value.All(Uri.IsHexDigit)))
                    throw new ArgumentException("Sai phiên bản dữ liệu; dùng phiên bản đọc mới nhất hoặc null khi tạo mới.");
                if (!row.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object || payload.GetRawText().Length > 32000)
                    throw new ArgumentException("payload cần là đối tượng tối đa 32000 ký tự.");
            }
            catch (ArgumentException error) { errors.Add(new(index, error.Message)); }
            index++;
        }
        return errors.ToImmutable();
    }
    public static string Template(string kind)
    {
        if (!Kinds.Contains(kind)) throw new ArgumentException("Loại mẫu không hợp lệ.");
        object payload = kind switch
        {
            "PROGRAM" => new { name = "", major = "", cohort = 0, requiredCredits = 0 },
            "GROUP" => new { program = "", name = "", requiredCredits = 0 },
            "COURSE" => new { program = "", course = "", group = (string?)null, required = true, equivalence = (string?)null },
            "REQUIREMENT" => new { program = "", course = "", requiredCourse = "", kind = "PRE", group = 1, minGrade = 5 },
            "ASSIGNMENT" => new { student = "", program = "" },
            "RESULT" => new { student = "", course = "", term = "", attempt = 1, recognition = "GRADED", grade = (decimal?)null, passed = false, source = "", publication = "", reason = "" },
            "STUDENT" => new { name = "", birthDate = (string?)null, gender = (string?)null, email = (string?)null, phone = (string?)null, address = (string?)null, major = "", @class = "", active = true },
            _ => new { course = "", lecturer = "", term = "", capacity = 0, status = "DONG", meetings = Array.Empty<object>() }
        };
        return JsonSerializer.Serialize(new { batchId = Guid.NewGuid(), source = "", rows = new[] { new { kind, id = "", version = (string?)null, payload } } }, new JsonSerializerOptions { WriteIndented = true });
    }
    private static JsonDocument Open(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2_000_000) throw new ArgumentException("Tệp JSON rỗng hoặc vượt giới hạn 2 MB ký tự.");
        try { return JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 }); }
        catch (JsonException) { throw new ArgumentException("Tệp không phải JSON hợp lệ."); }
    }
    private static string Read(JsonElement value, string name) => value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
}
