using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Scrollable, keyboard-selectable actual occurrences; no truncated timetable lanes.</summary>
internal sealed class WeeklyOverlapDialog : Form
{
    private readonly Font headingFont = AppTheme.Font(12, true), itemFont = AppTheme.Font(9);
    private readonly System.Windows.Forms.Panel list = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
    private readonly List<Button> choices = [];

    internal WeeklyOverlapDialog(ScheduleStore store, TimetableDisplayGroup group,
        IReadOnlyList<(ScheduledOccurrence Occurrence, IReadOnlyList<string> Conflicts)> items,
        Action<CourseClass> selected)
    {
        AutoScaleMode = AutoScaleMode.None; Text = "Các buổi học giao giờ";
        BackColor = Color.White; StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
        MinimumSize = new(AppTheme.S(460), AppTheme.S(380)); ClientSize = new(AppTheme.S(640), AppTheme.S(620));
        var heading = new Label { Dock = DockStyle.Top, Height = AppTheme.S(78), Font = headingFont,
            ForeColor = SchedulingTheme.Ink, Padding = new(AppTheme.S(18), AppTheme.S(14), 0, 0),
            Text = $"{items.Count} buổi · {group.Date:dd/MM/yyyy}\nTiết {group.StartPeriod}–{group.EndPeriod} · Chọn một buổi để mở lớp" };
        var close = new Button { Dock = DockStyle.Bottom, Height = AppTheme.S(40), Text = "Đóng", Font = itemFont,
            FlatStyle = FlatStyle.Flat, BackColor = SchedulingTheme.Lavender, AccessibleName = "Đóng danh sách buổi học" };
        close.FlatAppearance.BorderSize = 0; close.Click += (_, _) => Close(); CancelButton = close;
        Controls.Add(list); Controls.Add(close); Controls.Add(heading);
        foreach (var (occurrence, conflicts) in items)
        {
            var course = store.Catalog.Courses.First(c => c.Id == occurrence.Class.CourseId);
            string teacher = store.Catalog.Lecturers.FirstOrDefault(l => ScheduleRules.Same(l.Id, occurrence.LecturerId))?.Name
                ?? occurrence.LecturerId;
            string metadata = $"{occurrence.Class.Id} · {course.Name}\nGiảng viên: {teacher}\n"
                + $"Ngày {occurrence.Date:dd/MM/yyyy} · Tiết {occurrence.Meeting.StartPeriod}–{occurrence.Meeting.EndPeriod} · Phòng {occurrence.RoomId}";
            string description = conflicts.Count == 0 ? "Mở lớp học phần" : string.Join("\n", conflicts);
            var button = new Button { Text = metadata + (conflicts.Count == 0 ? "" : "\n" + description),
                Font = itemFont, TextAlign = ContentAlignment.MiddleLeft, Padding = new(AppTheme.S(12)),
                FlatStyle = FlatStyle.Flat, BackColor = conflicts.Count == 0 ? SchedulingTheme.Cyan : AppTheme.AccentSoft,
                ForeColor = SchedulingTheme.Ink, Cursor = Cursors.Hand, AutoEllipsis = false,
                AccessibleName = metadata.Replace('\n', ' '), AccessibleDescription = description,
                Tag = occurrence, UseMnemonic = false };
            button.FlatAppearance.BorderSize = 1; button.FlatAppearance.BorderColor = conflicts.Count == 0
                ? SchedulingTheme.Line : SchedulingTheme.Danger;
            button.Click += (_, _) => selected(occurrence.Class);
            choices.Add(button); list.Controls.Add(button);
        }
        list.Resize += (_, _) => LayoutChoices(); Shown += (_, _) => { LayoutChoices(); choices.FirstOrDefault()?.Focus(); };
    }

    private void LayoutChoices()
    {
        int pad = AppTheme.S(12), width = Math.Max(AppTheme.S(200), list.ClientSize.Width - pad * 2
            - SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi));
        var scroll = new Point(-list.AutoScrollPosition.X, -list.AutoScrollPosition.Y);
        list.SuspendLayout(); list.AutoScrollPosition = Point.Empty;
        int y = pad;
        foreach (var button in choices)
        {
            int height = TextRenderer.MeasureText(button.Text, itemFont, new Size(width - pad * 2, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + pad * 2;
            height = Math.Max(AppTheme.S(108), height);
            button.SetBounds(pad, y, width, height); y += height + pad;
        }
        list.AutoScrollMinSize = new(0, y); list.ResumeLayout(); list.AutoScrollPosition = scroll;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { headingFont.Dispose(); itemFont.Dispose(); }
    }
}
