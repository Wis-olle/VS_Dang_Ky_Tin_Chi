using System.Text.Json;
using System.Text.RegularExpressions;
namespace QuanLyGiangVien.Domain.Academics;

public sealed record AcademicChange(string Kind, string Id, string? Version, string Payload);
public sealed record AcademicEdge(string Course, string RequiredCourse, string Kind);

public static class AcademicValidation
{
 public static readonly string[] Kinds = ["PROGRAM","GROUP","COURSE","REQUIREMENT","ASSIGNMENT","RESULT","POLICY","OVERRIDE","HOLD","STANDING"];
 public static string Code(string value)
 {
  var code=value.Trim().ToUpperInvariant();
  if(!Regex.IsMatch(code,"^[A-Z0-9_-]{1,20}$")) throw new ArgumentException("Mã phải gồm 1–20 chữ ASCII, số, dấu gạch dưới hoặc gạch ngang.");
  return code;
 }
 public static void Change(AcademicChange value)
 {
  Code(value.Id);
  if(!Kinds.Contains(value.Kind)) throw new ArgumentException("Loại dữ liệu học vụ không hợp lệ.");
  if(value.Version is {} v && (v.Length!=16 || !v.All(Uri.IsHexDigit))) throw new ArgumentException("Phiên bản học vụ không hợp lệ.");
  if(value.Payload.Length>32000) throw new ArgumentException("Nội dung học vụ quá dài.");
  try { using var json=JsonDocument.Parse(value.Payload); if(json.RootElement.ValueKind!=JsonValueKind.Object) throw new ArgumentException("Nội dung phải là một đối tượng."); }
  catch(JsonException){throw new ArgumentException("Nội dung học vụ không đúng định dạng.");}
 }
 public static string[] Classes(string[] values)
 {
  if(values.Length is <1 or >30) throw new ArgumentException("Chọn từ 1 đến 30 lớp học phần.");
  var codes=values.Select(Code).ToArray();
  if(codes.Distinct(StringComparer.Ordinal).Count()!=codes.Length) throw new ArgumentException("Danh sách lớp bị trùng.");
  return codes;
 }
 public static bool Acyclic(IEnumerable<AcademicEdge> edges)
 {
  var adjacency=edges.Where(x=>x.Kind is "PRE" or "PRIOR").GroupBy(x=>x.Course).ToDictionary(x=>x.Key,x=>x.Select(e=>e.RequiredCourse).ToArray());
  var visited=new HashSet<string>(); var visiting=new HashSet<string>();
  bool Visit(string code)
  {
   if(visiting.Contains(code)) return false;
   if(visited.Contains(code)) return true;
   visiting.Add(code);
   if(adjacency.TryGetValue(code,out var next) && next.Any(x=>!Visit(x))) return false;
   visiting.Remove(code); visited.Add(code); return true;
  }
  return adjacency.Keys.All(Visit);
 }
}
