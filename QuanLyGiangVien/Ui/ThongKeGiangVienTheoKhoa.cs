using AntdUI;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using Label = AntdUI.Label;

namespace QuanLyGiangVien.Ui;

public sealed class DashboardFaculty : AntdUI.Panel
{
    public event Action<string>? FacultyRequested;
    private readonly Label title = AppTheme.Label("Theo khoa", 11, true);
    private readonly System.Windows.Forms.Panel rows = new() { BackColor = Color.White };
    private IReadOnlyList<int> lastCounts = [];
    private IReadOnlyList<string> facultyNames = DemoData.Faculties;
    public void SetFaculties(IEnumerable<string> names)
    {
        facultyNames = Array.AsReadOnly(names.Distinct().ToArray());
        lastCounts = [];
    }
    public DashboardFaculty()
    {
        Radius = 20;
        Back = Color.White;
        BackColor = Color.Transparent;
        Controls.AddRange([title, rows]);
        AuthCardMotion.Bind(this, AppTheme.FacultyHoverFill, hoverEnabled: false);
        Resize += (_, _) => Arrange();
    }
    public void Refresh(IReadOnlyList<Lecturer> lecturers)
    {
        var nextCounts = facultyNames.Select(faculty => lecturers.Count(item => item.Faculty == faculty)).ToArray();
        if (lastCounts.SequenceEqual(nextCounts)) return;
        lastCounts = Array.AsReadOnly(nextCounts);
        foreach (Control child in rows.Controls.Cast<Control>().ToArray()) child.Dispose();
        int index = 0;
        foreach (var faculty in facultyNames)
        {
            var row = new DashboardActionPanel { Back = Color.White, BackColor = Color.White, Radius = 12, Tag = index++ };
            var (iconId, fallback) = faculty switch
            {
                "Công nghệ thông tin" => ("folder-code", "CodeOutlined"),
                "Kinh tế" => ("chart-column-increasing", "BarChartOutlined"),
                "Ngoại ngữ" => ("languages", "GlobalOutlined"),
                _ => ("cog", "SettingOutlined")
            };
            var tone = AppTheme.Tone(faculty);
            var icon = new AnimatedDashboardIcon(iconId, faculty, tone, fallback, 8);
            var name = AppTheme.Label(faculty == "Công nghệ thông tin" ? "CNTT" : faculty, 7.5f, false, AppTheme.Muted);
            name.TextAlign = ContentAlignment.MiddleCenter;
            var count = AppTheme.Label(lecturers.Count(item => item.Faculty == faculty).ToString(), 11, true);
            count.TextAlign = ContentAlignment.MiddleLeft;
            row.AccessibleName = $"{faculty}: {count.Text} giảng viên";
            row.Controls.AddRange([icon, name, count]);
            row.AccessibleDescription = "Lọc danh sách giảng viên theo khoa " + faculty + ".";
            row.BindChildClicks();
            row.Click += (_, _) => FacultyRequested?.Invoke(faculty);
            icon.BindHover(row, name, count);
            var hoverFill = Color.FromArgb(255, (255 + tone.R) / 2, (255 + tone.G) / 2, (255 + tone.B) / 2);
            AuthCardMotion.Bind(row, hoverFill, entranceEnabled: false);
            rows.Controls.Add(row);
        }
        Arrange();
    }
    private void Arrange()
    {
        int pad = AppTheme.S(18), width = Math.Max(1, Width - pad * 2);
        title.SetBounds(pad, AppTheme.S(11), width, AppTheme.S(25));
        rows.SetBounds(pad, AppTheme.S(42), width, Math.Max(1, Height - AppTheme.S(49)));
        int gap = AppTheme.S(8), count = Math.Max(1, rows.Controls.Count);
        int rowWidth = (width - gap * (count - 1)) / count;
        foreach (Control row in rows.Controls)
        {
            row.SetBounds((int)row.Tag! * (rowWidth + gap), 0, rowWidth, rows.Height);
            int iconX = Math.Max(0, (rowWidth - AppTheme.S(54)) / 2);
            row.Controls[0].SetBounds(iconX, AppTheme.S(4), AppTheme.S(28), AppTheme.S(28));
            row.Controls[1].SetBounds(0, AppTheme.S(35), rowWidth, AppTheme.S(20));
            row.Controls[2].SetBounds(iconX + AppTheme.S(34), AppTheme.S(4), AppTheme.S(20), AppTheme.S(28));
        }
    }
}
