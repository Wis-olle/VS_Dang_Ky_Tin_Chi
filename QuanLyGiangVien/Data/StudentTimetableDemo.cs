using System.Collections.Immutable;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data;

/// <summary>Demo only; replace with registration data from Huệ's module after authentication integration.</summary>
public static class StudentTimetableDemo
{
    public const string StudentId = "SV2024001";
    public const string StudentName = "Bùi Quang Minh";
    public static ImmutableArray<StudentRegistration> Registrations { get; } =
    [new(StudentId, "LHP0001"), new(StudentId, "LHP0002"), new(StudentId, "LHP0003"),
     new(StudentId, "LHP0007"), new("SV2024002", "LHP0004")];
}
