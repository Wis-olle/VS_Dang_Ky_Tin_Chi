using AntdUI;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed class CourseClassRow
{
    internal CourseClass Model { get; }
    public CellImage Artwork { get; }
    public CellText Id => new(Model.Id) { Fore = SchedulingTheme.Muted };
    public string Course { get; }
    public CellText Lecturer { get; }
    public string Seats => $"{Model.Enrolled}/{Model.Capacity}";
    public CellText Schedule { get; }
    public CellTag Status { get; }
    public CellButton[] Actions { get; }
    internal CourseClassRow(CourseClass item, ScheduleStore store, SchedulingVisualCatalog visuals)
    {
        Model = item;
        var course = store.Catalog.Courses.FirstOrDefault(c => c.Id == item.CourseId);
        var visual = visuals.GetCourse(course);
        Artwork = new(visuals.Thumbnail(visual)) { Size = new Size(48, 36), Radius = 8, Tooltip = visual.Caption };
        Course = course?.Name ?? item.CourseId;
        Lecturer = new(store.Catalog.Lecturers.FirstOrDefault(l => l.Id == item.LecturerId)?.Name ?? item.LecturerId) { Fore = SchedulingTheme.Muted };
        Schedule = new(item.Meetings.IsEmpty ? "Chưa xếp lịch" : string.Join(" · ", item.Meetings.Select(m => $"{Day(m.Day)} / {m.RoomId}"))) { Fore = SchedulingTheme.Muted };
        Status = Tag(item.Status, item.Status == SchedulingLabels.Canceled ? SchedulingTheme.Rose : item.Status == SchedulingLabels.Closed ? SchedulingTheme.Amber : SchedulingTheme.Mint);
        Actions = [Action("edit", "Sửa lớp", "EditOutlined"), Action("delete", "Xóa lớp", "DeleteOutlined")];
    }
    internal static string Day(DayOfWeek day) => day == DayOfWeek.Sunday ? "CN" : $"T{(int)day + 1}";
    internal static CellTag Tag(string text, Color tone) => new(text) { Back = tone, Fore = SchedulingTheme.Ink, BorderWidth = 0 };
    internal static CellButton Action(string id, string tip, string icon) => new(id, TTypeMini.Default)
    {
        Tooltip = tip, IconSvg = icon, Fore = SchedulingTheme.Muted,
        Radius = 10, BorderWidth = 0, DefaultBack = Color.Transparent, IsLink = true,
        BackHover = id == "delete" ? SchedulingTheme.Rose : SchedulingTheme.Canvas,
        BackActive = SchedulingTheme.Amber, DefaultBorderColor = SchedulingTheme.Line, Gap = 10
    };
}

internal sealed class RoomRow
{
    internal Room Model { get; }
    public CellImage Artwork { get; }
    public CellText Id => new(Model.Id) { Fore = SchedulingTheme.Muted };
    public string Building => Model.Building;
    public int Capacity => Model.Capacity;
    public string Type => Model.Type;
    public CellTag Status { get; }
    public CellButton[] Actions { get; }
    internal RoomRow(Room item, SchedulingVisualCatalog visuals)
    {
        Model = item;
        var visual = visuals.GetRoom(item);
        Artwork = new(visuals.Thumbnail(visual)) { Size = new Size(48, 36), Radius = 8, Tooltip = visual.Caption };
        Status = CourseClassRow.Tag(item.Status, item.Status == SchedulingLabels.Maintenance ? SchedulingTheme.Amber : SchedulingTheme.Mint);
        Actions = [CourseClassRow.Action("edit", "Sửa phòng", "EditOutlined"), CourseClassRow.Action("delete", "Xóa phòng", "DeleteOutlined")];
    }
}
