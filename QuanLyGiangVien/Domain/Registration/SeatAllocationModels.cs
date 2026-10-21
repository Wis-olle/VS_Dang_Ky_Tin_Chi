using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace QuanLyGiangVien.Domain.Registration;

public sealed record SeatAllocationChange(string Kind,string Id,string? Version,string Payload);
public static class SeatAllocationValidation
{
    public static Guid ClaimRequest(string accountId,long offerId,string version)
    {
        RegistrationValidation.RequiredCode(accountId);Entry(offerId);
        if(version is not {Length:16}||!version.All(Uri.IsHexDigit))throw new ArgumentException("Phiên bản lời mời không hợp lệ.");
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"Campus:SeatClaim:{accountId}:{offerId}:{version.ToUpperInvariant()}"))[..16]);
    }
    public static void Change(SeatAllocationChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if(change.Kind is not ("ROUND" or "GROUP" or "MEMBER" or "TICKET" or "QUOTA"))throw new ArgumentException("Loại cấu hình chỗ không hợp lệ.");
        RegistrationValidation.RequiredCode(change.Id);
        if(change.Version is {} v && (v.Length!=16||!v.All(Uri.IsHexDigit)))throw new ArgumentException("Phiên bản không hợp lệ. Hãy tải lại.");
        if(change.Payload is null||change.Payload.Length>16000)throw new ArgumentException("Nội dung cấu hình quá dài.");
        try{using var json=JsonDocument.Parse(change.Payload);if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Cấu hình cần các trường dữ liệu có tên.");}
        catch(JsonException){throw new ArgumentException("Cấu hình không đúng định dạng.");}
    }
    public static long Entry(long id)=>id>0?id:throw new ArgumentException("Chọn một yêu cầu chờ hợp lệ.");
    public static int Batch(int count)=>count is >=1 and <=100?count:throw new ArgumentException("Mỗi lượt xử lý từ 1 đến 100 yêu cầu.");
    public static string Classes(IEnumerable<string> classes)
    {
        var json=RegistrationWorkflowValidation.ClassesJson(classes);
        if(json=="[]")throw new ArgumentException("Chọn đầy đủ lớp học phần hoặc tổ hợp muốn chờ.");return json;
    }
}
