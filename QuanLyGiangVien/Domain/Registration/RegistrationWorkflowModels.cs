using System.Text.Json;
namespace QuanLyGiangVien.Domain.Registration;

public static class RegistrationWorkflowValidation
{
    public static string ClassesJson(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var codes=values.Select(RegistrationValidation.RequiredCode).Select(v=>v.ToUpperInvariant()).ToArray();
        if(codes.Length>30 || codes.Distinct(StringComparer.Ordinal).Count()!=codes.Length)
            throw new ArgumentException("Chọn tối đa 30 lớp khác nhau.");
        return JsonSerializer.Serialize(codes.Order(StringComparer.Ordinal));
    }
    public static string RemovalsJson(IEnumerable<long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var ids=values.Select(RegistrationValidation.RegistrationId).ToArray();
        if(ids.Length>30 || ids.Distinct().Count()!=ids.Length) throw new ArgumentException("Chọn tối đa 30 đăng ký khác nhau.");
        return JsonSerializer.Serialize(ids.Order());
    }
    public static byte[]? Version(byte[]? version)=>version is null?null:version.Length==8?version.ToArray():throw new ArgumentException("Phiên bản không hợp lệ. Hãy tải lại.");
    public static Guid Request(Guid value)=>value==Guid.Empty?throw new ArgumentException("Mã yêu cầu không hợp lệ."):value;
    public static string RolesJson(string classesJson,IReadOnlyDictionary<string,string>? roles)
    {
        if(roles is null) throw new ArgumentException("Khai báo rõ LT/TH/TL cho từng thành phần.");
        var normalized=roles.ToDictionary(p=>RegistrationValidation.RequiredCode(p.Key).ToUpperInvariant(),p=>p.Value?.Trim().ToUpperInvariant()??"",StringComparer.Ordinal);
        var classes=JsonSerializer.Deserialize<string[]>(classesJson)!;
        if(normalized.Count is <2 or >3 || !classes.Order().SequenceEqual(normalized.Keys.Order()) ||
            normalized.Values.Any(v=>v is not "LT" and not "TH" and not "TL") ||
            !normalized.Values.Contains("LT") || normalized.Values.Distinct().Count()!=normalized.Count)
            throw new ArgumentException("Mỗi tổ hợp phải có một lớp LT và một lớp TH/TL; vai trò của từng mã lớp phải khác nhau.");
        return JsonSerializer.Serialize(normalized.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value));
    }
}
