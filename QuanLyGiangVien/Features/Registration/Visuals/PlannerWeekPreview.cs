using System.Collections.Immutable;
using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration.Visuals;

/// <summary>A dated projection of the selected option; this control never creates recurring meetings.</summary>
internal sealed class PlannerWeekPreview : UserControl
{
    private readonly ComboBox choice = new() { Name = "cboPlannerPreviewOption", DropDownStyle = ComboBoxStyle.DropDownList, Width = AppTheme.S(260) };
    private readonly ComboBox week = new() { Name = "cboPlannerPreviewWeek", FormattingEnabled = true, DropDownStyle = ComboBoxStyle.DropDownList, Width = AppTheme.S(225) };
    private readonly TableLayoutPanel days = new() { Name = "pnlPlannerWeekDays", Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, BackColor = Color.White };
    private readonly Label detail = new() { Name = "lblPlannerMeetingDetail", Dock = DockStyle.Bottom, Height = AppTheme.S(32), Padding = new(AppTheme.S(4)), BackColor = AppTheme.SubtleLilac, AutoEllipsis = true };
    private readonly Label summary = new() { Name = "lblPlannerPreviewSummary", AutoSize = true, ForeColor = AppTheme.Muted, Margin = new(AppTheme.S(5)) };
    private readonly Button previous = Ui.Button("‹ Tuần trước"), next = Ui.Button("Tuần sau ›");
    private ImmutableArray<SchedulePlannerOption> options = [];
    private ImmutableArray<SchedulePlannerClass> classes = [];
    private bool binding;
    public event Action<int>? OptionSelected;
    public int MeetingCardCount { get; private set; }

    public PlannerWeekPreview()
    {
        Name = "PlannerWeekPreview"; Dock = DockStyle.Fill; BackColor = Color.White;
        var toolbar = Ui.Toolbar(); toolbar.Padding = new(AppTheme.S(3)); toolbar.AutoSize = false; toolbar.Height = AppTheme.S(52); toolbar.WrapContents = false; toolbar.AutoScroll = true;
        choice.AccessibleName = "So sánh phương án"; week.AccessibleName = "Tuần xem trước";
        previous.Name = "btnPlannerPreviousWeek"; next.Name = "btnPlannerNextWeek";
        foreach (var button in new[] { previous, next }) { button.MinimumSize = new(AppTheme.S(100), AppTheme.S(32)); button.Height = AppTheme.S(32); button.Margin = new(AppTheme.S(2)); }
        toolbar.Controls.AddRange([choice, previous, week, next]);
        var meta = new FlowLayoutPanel { Dock = DockStyle.Top, Height = AppTheme.S(26), BackColor = Color.White, WrapContents = false, AutoScroll = true };
        meta.Controls.Add(summary);
        for (var i = 0; i < 7; i++) days.ColumnStyles.Add(new(SizeType.Percent, 100f / 7));
        Controls.Add(days); Controls.Add(detail); Controls.Add(meta); Controls.Add(toolbar);
        choice.SelectedIndexChanged += (_, _) => { if (binding) return; SetWeeks(); OptionSelected?.Invoke(choice.SelectedIndex); };
        week.SelectedIndexChanged += (_, _) => { if (!binding) RenderWeek(); };
        previous.Click += (_, _) => { if (week.SelectedIndex > 0) week.SelectedIndex--; };
        next.Click += (_, _) => { if (week.SelectedIndex + 1 < week.Items.Count) week.SelectedIndex++; };
        ClearPreview();
    }

    public void SetOptions(SchedulePlannerResult result, SchedulePlannerSnapshot? snapshot)
    {
        binding = true;
        try
        {
            options = result.Options; classes = snapshot?.Classes ?? [];
            choice.Items.Clear();
            for (var i = 0; i < options.Length; i++)
            {
                var item = options[i];
                choice.Items.Add($"Phương án {i + 1} · {item.TotalCredits} TC · {item.DaysPerWeek} ngày/tuần");
            }
            if (choice.Items.Count > 0) choice.SelectedIndex = 0;
        }
        finally { binding = false; }
        SetWeeks();
    }

    public void SelectOption(int index)
    {
        if (index < 0 || index >= options.Length || choice.SelectedIndex == index) return;
        binding = true; choice.SelectedIndex = index; binding = false; SetWeeks();
    }

    public void ClearPreview()
    {
        binding = true; options = []; classes = []; choice.Items.Clear(); week.Items.Clear(); binding = false;
        summary.Text = "Sinh phương án để xem và so sánh lịch tuần.";
        detail.Text = "Chọn một buổi học để xem tên môn và ngày học thực tế. Phương án chưa giữ chỗ.";
        ClearDays(); previous.Enabled = next.Enabled = false;
    }

    private void SetWeeks()
    {
        var old = week.SelectedItem is DateOnly date ? date : (DateOnly?)null;
        binding = true; week.Items.Clear();
        if (choice.SelectedIndex >= 0)
        {
            var selected = options[choice.SelectedIndex];
            foreach (var monday in selected.Meetings.Select(m => Monday(m.Date)).Distinct().Order()) week.Items.Add(monday);
            week.Format -= FormatWeek; week.Format += FormatWeek;
            if (week.Items.Count > 0) week.SelectedIndex = old is not null && week.Items.Contains(old.Value) ? week.Items.IndexOf(old.Value) : Math.Max(0, week.Items.IndexOf(Monday(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))));
            summary.Text = $"{selected.AllClassIds.Length} lớp trong lịch · {selected.GapPeriods} tiết trống · Tím: lớp giữ nguyên · Xanh: lớp mới";
        }
        binding = false; RenderWeek();
    }

    private static void FormatWeek(object? sender, ListControlConvertEventArgs e)
    { if (e.ListItem is DateOnly d) e.Value = $"{d:dd/MM} – {d.AddDays(6):dd/MM/yyyy}"; }

    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private void RenderWeek()
    {
        ClearDays(); previous.Enabled = week.SelectedIndex > 0; next.Enabled = week.SelectedIndex >= 0 && week.SelectedIndex + 1 < week.Items.Count;
        if (choice.SelectedIndex < 0 || week.SelectedItem is not DateOnly monday)
        { detail.Text = options.IsEmpty ? "Chưa có lịch để xem trước." : "Phương án không có buổi học có ngày thực tế."; return; }
        var selected = options[choice.SelectedIndex];
        var names = new[] { "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy", "Chủ nhật" };
        for (var dayIndex = 0; dayIndex < 7; dayIndex++)
        {
            var date = monday.AddDays(dayIndex);
            var column = new SoftSurface { Radius = 12, Dock = DockStyle.Fill, Margin = new(AppTheme.S(2)), BackColor = Color.FromArgb(255, 250, 251) };
            var heading = new Label { Text = names[dayIndex] + "\n" + date.ToString("dd/MM"), Dock = DockStyle.Top, Height = AppTheme.S(40), TextAlign = ContentAlignment.MiddleCenter, BackColor = AppTheme.TableHeader, ForeColor = AppTheme.Ink };
            var agenda = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new(AppTheme.S(3)) };
            column.Controls.Add(agenda); column.Controls.Add(heading); days.Controls.Add(column, dayIndex, 0);
            foreach (var meeting in selected.Meetings.Where(m => m.Date == date).OrderBy(m => m.StartPeriod).ThenBy(m => m.ClassId))
                AddMeeting(agenda, meeting, selected.AddClassIds.Contains(meeting.ClassId));
            if (agenda.Controls.Count == 0) agenda.Controls.Add(new Label { Text = "Không có\nbuổi học", AutoSize = true, ForeColor = AppTheme.Muted, Margin = new(AppTheme.S(6), AppTheme.S(15), 0, 0) });
            void FitCards() {
                var cards = agenda.Controls.OfType<Button>().ToArray();
                var height = Math.Clamp((agenda.ClientSize.Height - agenda.Padding.Vertical) / Math.Max(1, cards.Length) - AppTheme.S(4), AppTheme.S(58), AppTheme.S(104));
                foreach (var item in cards) { item.Height = height; item.Width = Math.Max(AppTheme.S(60), agenda.ClientSize.Width - AppTheme.S(8) - (agenda.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0)); }
            }
            agenda.SizeChanged += (_, _) => FitCards(); FitCards();
        }
        detail.Text = "Chọn buổi học bằng chuột hoặc Tab rồi Enter để xem chi tiết · Lịch theo ngày thực tế, có cả lớp đang giữ.";
    }

    private void AddMeeting(FlowLayoutPanel agenda, SchedulePlannerMeeting meeting, bool isNew)
    {
        var subject = classes.FirstOrDefault(c => c.Id == meeting.ClassId);
        var name = subject?.CourseName ?? meeting.ClassId;
        var button = new ShowcaseButton
        {
            Name = "btnPlannerMeeting_" + meeting.ClassId + "_" + meeting.Date.ToString("yyyyMMdd"),
            Text = $"Tiết {meeting.StartPeriod}–{meeting.EndPeriod}\n{name}",
            AccessibleName = $"{name}, {meeting.Date:dd/MM/yyyy}, tiết {meeting.StartPeriod} đến {meeting.EndPeriod}, {(isNew ? "lớp mới" : "giữ nguyên")}",
            Width = AppTheme.S(112), Height = AppTheme.S(104), Margin = new(0, AppTheme.S(2), 0, AppTheme.S(2)),
            FlatStyle = FlatStyle.Flat, BackColor = isNew ? AppTheme.Cyan : AppTheme.Lilac, ForeColor = AppTheme.Ink,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new(AppTheme.S(6)), Cursor = Cursors.Hand,
            AutoEllipsis = true, UseVisualStyleBackColor = false
        };
        button.Font = Ui.OwnFont(button, AppTheme.Font(9));
        button.FlatAppearance.BorderColor = AppTheme.Line;
        button.Click += (_, _) => detail.Text = $"{name} · {meeting.ClassId} · {meeting.Date:dd/MM/yyyy} · Tiết {meeting.StartPeriod}–{meeting.EndPeriod} · {(isNew ? "Thêm vào phương án" : "Lớp đang đăng ký hoặc đã chọn giữ")}";
        agenda.Controls.Add(button); MeetingCardCount++;
    }

    private void ClearDays()
    {
        foreach (Control control in days.Controls.Cast<Control>().ToArray()) control.Dispose();
        days.Controls.Clear(); MeetingCardCount = 0;
    }
}
