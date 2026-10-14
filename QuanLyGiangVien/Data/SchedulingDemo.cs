using System.Collections.Immutable;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data;

public static class SchedulingDemo
{
    public static ScheduleStore Create(Func<IReadOnlyList<Lecturer>>? lecturers = null)
    {
        var first = new Semester("HK1-2627", "Học kỳ I · 2026–2027", new(2026, 9, 7), new(2026, 12, 27));
        var second = new Semester("HK2-2627", "Học kỳ II · 2026–2027", new(2027, 1, 11), new(2027, 5, 23));
        var courses = new[] {
            new Course("IT101", "Lập trình hướng đối tượng", 3, "Công nghệ thông tin"),
            new Course("IT102", "Cơ sở dữ liệu", 3, "Công nghệ thông tin"),
            new Course("IT103", "Công nghệ phần mềm", 3, "Công nghệ thông tin"),
            new Course("EN101", "Tiếng Anh chuyên ngành", 3, "Ngoại ngữ"),
            new Course("EN102", "Tiếng Anh học thuật", 2, "Ngoại ngữ"),
            new Course("EC101", "Kinh tế vi mô", 3, "Kinh tế"),
            new Course("EC102", "Marketing căn bản", 3, "Kinh tế"),
            new Course("ET101", "Kỹ thuật điện", 3, "Kỹ thuật")
        };
        var rooms = new[] {
            new Room("A201", "Tòa A", 60, SchedulingLabels.Theory, SchedulingLabels.Ready),
            new Room("A202", "Tòa A", 50, SchedulingLabels.Theory, SchedulingLabels.Ready),
            new Room("A301", "Tòa A", 80, SchedulingLabels.Theory, SchedulingLabels.Ready),
            new Room("B101", "Tòa B", 40, SchedulingLabels.Lab, SchedulingLabels.Ready),
            new Room("B102", "Tòa B", 40, SchedulingLabels.Lab, SchedulingLabels.Ready),
            new Room("B201", "Tòa B", 60, SchedulingLabels.Theory, SchedulingLabels.Ready),
            new Room("C101", "Tòa C", 120, SchedulingLabels.Hall, SchedulingLabels.Ready),
            new Room("C202", "Tòa C", 45, SchedulingLabels.Theory, SchedulingLabels.Maintenance)
        };
        var classes = new[] {
            Make("LHP0001", "IT101", "GV0001", 40, 32, first, SchedulingLabels.Open,
                Meeting("A201", DayOfWeek.Monday, 1, 3, first), Meeting("B101", DayOfWeek.Thursday, 4, 6, first)),
            Make("LHP0002", "IT102", "GV0004", 40, 40, first, SchedulingLabels.Open,
                Meeting("B102", DayOfWeek.Tuesday, 1, 3, first), Meeting("A202", DayOfWeek.Friday, 4, 6, first)),
            Make("LHP0003", "EN101", "GV0002", 50, 38, first, SchedulingLabels.Open,
                Meeting("A202", DayOfWeek.Monday, 4, 6, first)),
            Make("LHP0004", "EC101", "GV0003", 60, 48, first, SchedulingLabels.Open,
                Meeting("A301", DayOfWeek.Wednesday, 1, 3, first)),
            Make("LHP0005", "ET101", "GV0005", 60, 22, first, SchedulingLabels.Open,
                Meeting("B201", DayOfWeek.Tuesday, 7, 9, first)),
            Make("LHP0006", "EN102", "GV0008", 40, 0, first, SchedulingLabels.Open),
            Make("LHP0007", "IT103", "GV0007", 60, 45, first, SchedulingLabels.Closed,
                Meeting("A201", DayOfWeek.Wednesday, 4, 6, first)),
            Make("LHP0008", "EC102", "GV0010", 50, 0, first, SchedulingLabels.Canceled,
                Meeting("A202", DayOfWeek.Monday, 4, 6, first)),
            Make("LHP0009", "IT101", "GV0001", 40, 0, second, SchedulingLabels.Open,
                Meeting("B101", DayOfWeek.Monday, 1, 3, second)),
            Make("LHP0010", "EN101", "GV0012", 50, 0, second, SchedulingLabels.Open)
        };
        return new(new TeachingCatalog(courses, [first, second], lecturers ?? DemoData.Create), rooms, classes);
    }

    private static ClassMeeting Meeting(string room, DayOfWeek day, int start, int end, Semester semester) =>
        new(room, day, start, end, semester.StartDate, semester.EndDate);
    private static CourseClass Make(string id, string course, string lecturer, int capacity, int enrolled,
        Semester semester, string status, params ClassMeeting[] meetings) =>
        new(id, course, semester.Id, lecturer, capacity, enrolled, status, meetings.ToImmutableArray());
}
