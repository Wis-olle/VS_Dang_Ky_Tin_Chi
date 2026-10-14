namespace QuanLyGiangVien.Domain.Scheduling;

public sealed class TeachingCatalog
{
    private readonly Func<IReadOnlyList<Lecturer>> lecturers;
    public IReadOnlyList<Course> Courses { get; }
    public IReadOnlyList<Semester> Semesters { get; }
    public IReadOnlyList<Lecturer> Lecturers => Array.AsReadOnly(lecturers().ToArray());

    public TeachingCatalog(IEnumerable<Course> courses, IEnumerable<Semester> semesters,
        Func<IReadOnlyList<Lecturer>> lecturers)
    {
        ArgumentNullException.ThrowIfNull(courses);
        ArgumentNullException.ThrowIfNull(semesters);
        ArgumentNullException.ThrowIfNull(lecturers);
        this.lecturers = lecturers;
        var courseArray = courses.ToArray();
        var semesterArray = semesters.ToArray();
        if (courseArray.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Faculty)
            || item.Credits is < 1 or > 12) || HasDuplicates(courseArray.Select(item => item.Id)))
            throw new ArgumentException("Danh mục học phần không hợp lệ.", nameof(courses));
        if (semesterArray.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(item.Name) || item.StartDate == default || item.EndDate < item.StartDate
            || item.EndDate.DayNumber - item.StartDate.DayNumber > 366)
            || HasDuplicates(semesterArray.Select(item => item.Id)))
            throw new ArgumentException("Danh mục học kỳ không hợp lệ.", nameof(semesters));
        Courses = Array.AsReadOnly(courseArray.Select(item => item with {
            Id = ScheduleRules.Id(item.Id), Name = item.Name.Trim(), Faculty = item.Faculty.Trim() }).ToArray());
        Semesters = Array.AsReadOnly(semesterArray.Select(item => item with {
            Id = ScheduleRules.Id(item.Id), Name = item.Name.Trim() }).ToArray());
    }

    private static bool HasDuplicates(IEnumerable<string> ids) =>
        ids.Select(ScheduleRules.Id).GroupBy(id => id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1);
}
