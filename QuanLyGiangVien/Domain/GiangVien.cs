namespace QuanLyGiangVien.Domain;

public sealed record Lecturer(
    string Id, string Name, DateTime BirthDate, string Gender,
    string Email, string Phone, string Faculty, string Degree,
    string Status, string AvatarKey, int ActiveClasses, string Version = "");
