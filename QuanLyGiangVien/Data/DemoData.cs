using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data;

public sealed record TeachingClass(string Code, string Name, string Room, string Schedule);

public static class DemoData
{
    public static IReadOnlyList<string> Faculties { get; } = Array.AsReadOnly(new[] { "Công nghệ thông tin", "Kinh tế", "Ngoại ngữ", "Kỹ thuật" });
    public static IReadOnlyList<string> Degrees { get; } = Array.AsReadOnly(new[] { "Thạc sĩ", "Tiến sĩ", "PGS. Tiến sĩ", "Giáo sư" });
    public static IReadOnlyList<Lecturer> Create() => Array.AsReadOnly(new[]
    {
        new Lecturer("GV0001", "Nguyễn Hoàng Minh", new(1986, 4, 12), "Nam", "minh.nh@campus.edu.vn", "0901234567", "Công nghệ thông tin", "Tiến sĩ", "Đang giảng dạy", "portrait-04", 3),
        new Lecturer("GV0002", "Trần Ngọc Anh", new(1990, 8, 21), "Nữ", "anh.tn@campus.edu.vn", "0912345678", "Ngoại ngữ", "Thạc sĩ", "Đang giảng dạy", "portrait-16", 2),
        new Lecturer("GV0003", "Lê Quốc Bảo", new(1982, 11, 3), "Nam", "bao.lq@campus.edu.vn", "0923456789", "Kinh tế", "PGS. Tiến sĩ", "Đang giảng dạy", "portrait-10", 4),
        new Lecturer("GV0004", "Phạm Thùy Linh", new(1992, 3, 16), "Nữ", "linh.pt@campus.edu.vn", "0934567890", "Công nghệ thông tin", "Thạc sĩ", "Đang giảng dạy", "portrait-01", 2),
        new Lecturer("GV0005", "Đặng Đức Huy", new(1987, 6, 9), "Nam", "huy.dd@campus.edu.vn", "0945678901", "Kỹ thuật", "Tiến sĩ", "Đang giảng dạy", "portrait-06", 3),
        new Lecturer("GV0006", "Vũ Mai Phương", new(1991, 9, 27), "Nữ", "phuong.vm@campus.edu.vn", "0956789012", "Kinh tế", "Thạc sĩ", "Tạm nghỉ", "portrait-25", 0),
        new Lecturer("GV0007", "Bùi Tuấn Kiệt", new(1988, 2, 18), "Nam", "kiet.bt@campus.edu.vn", "0967890123", "Công nghệ thông tin", "Tiến sĩ", "Đang giảng dạy", "portrait-20", 3),
        new Lecturer("GV0008", "Đỗ Hải Yến", new(1993, 12, 5), "Nữ", "yen.dh@campus.edu.vn", "0978901234", "Ngoại ngữ", "Thạc sĩ", "Đang giảng dạy", "portrait-27", 2),
        new Lecturer("GV0009", "Hoàng Anh Dũng", new(1981, 7, 22), "Nam", "dung.ha@campus.edu.vn", "0989012345", "Kỹ thuật", "PGS. Tiến sĩ", "Đang giảng dạy", "portrait-23", 4),
        new Lecturer("GV0010", "Ngô Thanh Hà", new(1994, 5, 11), "Nữ", "ha.nt@campus.edu.vn", "0890123456", "Kinh tế", "Thạc sĩ", "Đang giảng dạy", "portrait-31", 1),
        new Lecturer("GV0011", "Dương Gia Khánh", new(1989, 10, 30), "Nam", "khanh.dg@campus.edu.vn", "0881234567", "Công nghệ thông tin", "Thạc sĩ", "Tạm nghỉ", "portrait-08", 0),
        new Lecturer("GV0012", "Mai Khánh Chi", new(1990, 1, 8), "Nữ", "chi.mk@campus.edu.vn", "0872345678", "Ngoại ngữ", "Tiến sĩ", "Đang giảng dạy", "portrait-11", 3)
    });
    public static IReadOnlyList<TeachingClass> Classes(Lecturer lecturer)
    {
        var titles = lecturer.Faculty switch
        {
            "Công nghệ thông tin" => new[] { "Lập trình hướng đối tượng", "Cơ sở dữ liệu", "Công nghệ phần mềm", "Lập trình C#" },
            "Ngoại ngữ" => new[] { "Tiếng Anh chuyên ngành", "Tiếng Anh học thuật", "Giao tiếp tiếng Anh", "Biên dịch" },
            "Kinh tế" => new[] { "Kinh tế vi mô", "Quản trị học", "Marketing căn bản", "Kinh tế vĩ mô" },
            _ => new[] { "Kỹ thuật điện", "Cơ học ứng dụng", "Thiết kế kỹ thuật", "Vật liệu học" }
        };
        return Enumerable.Range(0, lecturer.ActiveClasses).Select(i => new TeachingClass(
            $"{lecturer.Id[2..]}-{i + 1:00}", titles[i % titles.Length], $"A{201 + i}",
            $"Thứ {i + 2} · Tiết {i % 2 * 3 + 1}–{i % 2 * 3 + 3}" )).ToArray();
    }
}
