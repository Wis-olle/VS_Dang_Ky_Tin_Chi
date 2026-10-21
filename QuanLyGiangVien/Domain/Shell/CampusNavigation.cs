using System.Collections.Immutable;

namespace QuanLyGiangVien.Domain.Shell;

public enum CampusModule { Lecturers, Scheduling, Catalogs, Registration, Reports, Timetable, Students, Academics }

/// <summary>Navigation reflects implemented modules. SQL still enforces each operation's rights.</summary>
public static class CampusNavigation
{
    public static ImmutableArray<CampusModule> ForRole(string role) => role switch {
        "DAO_TAO" or "QUAN_TRI" => [CampusModule.Lecturers, CampusModule.Scheduling, CampusModule.Catalogs, CampusModule.Students, CampusModule.Academics, CampusModule.Reports],
        "SINH_VIEN" => [CampusModule.Registration, CampusModule.Timetable],
        _ => []
    };

    public static bool CanOpen(string role, CampusModule module) => ForRole(role).Contains(module);

    public static string Title(CampusModule module) => module switch {
        CampusModule.Lecturers => "Giảng viên", CampusModule.Scheduling => "Lớp & phòng học",
        CampusModule.Catalogs => "Học phần & học kỳ", CampusModule.Registration => "Tra cứu & đăng ký",
        CampusModule.Reports => "Đăng ký & vận hành", CampusModule.Timetable => "Thời khóa biểu",
        CampusModule.Students => "Sinh viên",
        CampusModule.Academics => "Học vụ",
        _ => throw new ArgumentOutOfRangeException(nameof(module))
    };
}
