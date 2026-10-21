using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Academics;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration.Visuals;

internal sealed class AcademicProgressOverview : UserControl
{
    private readonly FlowLayoutPanel content = new() { Name = "pnlAcademicProgressContent", Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new(AppTheme.S(10)), BackColor = Color.White };
    private FlowLayoutPanel? groupCards, nextSteps;
    public event Action<string>? CourseSelected;
    public int GroupCardCount { get; private set; }

    public AcademicProgressOverview()
    {
        Name = "AcademicProgressOverview"; Dock = DockStyle.Fill; BackColor = Color.White;
        Controls.Add(content); content.SizeChanged += (_, _) => FitWidth();
    }

    public void ShowProgress(AcademicPlanningSnapshot snapshot, DegreeAuditResult result, IEnumerable<AcademicPlanningIssue> checks)
    {
        content.SuspendLayout();
        try
        {
            ClearProgress();
            if (snapshot.Curriculum is null)
            {
                AddText("Hành trình học tập của bạn", 34, true);
                AddText("Chưa có CTĐT chính thức để tính tiến độ", 38, true, AppTheme.SubtleLilac);
                AddText("Phòng đào tạo cần nhập nguồn thật, công bố và gắn chương trình đúng ngành/khóa. Khi có dữ liệu, tiến độ và gợi ý tiếp theo sẽ xuất hiện tại đây.", 78);
                return;
            }
            AddText((snapshot.IsWhatIf ? "Mô phỏng · " : "Hành trình học tập · ") + ProgramName(snapshot), 28, true);
            var total = snapshot.Curriculum.RequiredCredits;
            var percent = total == 0 ? 100 : Math.Clamp((int)Math.Round(100d * result.AllocatedDone / total), 0, 100);
            var progress = new SoftSurface { Radius = 12, Height = AppTheme.S(44), BackColor = AppTheme.Mint, Padding = new(AppTheme.S(9), AppTheme.S(3), AppTheme.S(9), AppTheme.S(7)), Margin = new(0, 0, 0, AppTheme.S(5)) };
            var completion = new Label { Name = "lblAcademicCompletion", Text = $"{percent}% hoàn thành   ·   {result.AllocatedDone}/{total} tín chỉ đã đạt", Dock = DockStyle.Top, Height = AppTheme.S(24), ForeColor = AppTheme.Ink };
            completion.Font = Ui.OwnFont(completion, AppTheme.Font(12, true));
            progress.Controls.Add(new CreditProgressBar(new("total", "Tổng tiến độ", total, result.AllocatedDone, result.AllocatedInProgress, result.AllocatedPlanned)) { Dock = DockStyle.Fill });
            progress.Controls.Add(completion); content.Controls.Add(progress);
            var metrics = new TableLayoutPanel { Height = AppTheme.S(62), ColumnCount = 3, RowCount = 1, Margin = new(0, 0, 0, AppTheme.S(5)) };
            metrics.RowStyles.Add(new(SizeType.Percent, 100));
            for (var i = 0; i < 3; i++) metrics.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
            metrics.Controls.Add(Metric($"{result.AllocatedInProgress} TC", "Đang học / chờ kết quả", AppTheme.Lilac), 0, 0);
            metrics.Controls.Add(Metric($"{result.AllocatedPlanned} TC", "Trong kế hoạch", AppTheme.Cyan), 1, 0);
            metrics.Controls.Add(Metric($"{Math.Max(0, total - result.AllocatedDone)} TC", "Còn thiếu kết quả đạt", AppTheme.SubtlePeach), 2, 0);
            content.Controls.Add(metrics);
            AddText("Tiến độ từng nhóm", 24, true);
            groupCards = CardRow("pnlAcademicGroups"); content.Controls.Add(groupCards);
            foreach (var group in result.Groups) AddGroup(group);
            AddText("Bước tiếp theo", 28, true);
            nextSteps = CardRow("pnlAcademicNextSteps"); content.Controls.Add(nextSteps);
            var problems = checks.ToArray();
            foreach (var issue in problems.Take(4))
                AddAction(CourseName(snapshot, issue.Course), HumanMessage(snapshot, issue.Message), issue.Course, issue.Blocking ? AppTheme.AccentSoft : AppTheme.Cyan);
            foreach (var course in result.Courses.Where(c => c.Requirement.Required && c.Done < c.Requirement.Credits).Take(Math.Max(0, 4 - problems.Length)))
            {
                var hint = course.InProgress > 0 ? "Đang học · cần kết quả đạt để hoàn tất yêu cầu." : course.Planned > 0 ? "Đã có trong kế hoạch · xem kỳ dự kiến và điều kiện mở môn." : "Môn bắt buộc còn thiếu · chọn để xếp vào kế hoạch.";
                AddAction(CourseName(snapshot, course.Requirement.Course), hint, course.Requirement.Course, AppTheme.Lilac);
            }
            if (nextSteps.Controls.Count == 0)
                AddText(result.Satisfied ? "Các yêu cầu đối soát hiện đã được đáp ứng. Kết quả này chưa thay quyết định tốt nghiệp chính thức." : "Xem nhóm còn thiếu tín chỉ, chọn môn tự chọn phù hợp và kiểm tra căn cứ dự kiến mở môn.", 56, false, AppTheme.SubtleLilac);
            AddText("Xanh lá: đã đạt · tím: đang học · xanh nhạt: dự kiến. Kế hoạch không giữ chỗ; tín chỉ dự kiến chưa phải kết quả đạt.", 46);
        }
        finally { FitWidth(); content.ResumeLayout(true); }
    }

    public void ClearProgress()
    {
        groupCards = nextSteps = null;
        foreach (Control child in content.Controls.Cast<Control>().ToArray()) { Ui.DisposeOwnedFonts(child); child.Dispose(); }
        content.Controls.Clear(); GroupCardCount = 0;
    }

    private static FlowLayoutPanel CardRow(string name) => new() { Name = name, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = Padding.Empty, Margin = new(0, 0, 0, AppTheme.S(5)), BackColor = Color.White };

    private void AddGroup(DegreeGroupProgress group)
    {
        var panel = new SoftSurface { Radius = 12, Name = "pnlAcademicGroup_" + group.Id, Height = AppTheme.S(84), BackColor = Color.FromArgb(255, 249, 250), Padding = new(AppTheme.S(6)), Margin = new(0, 0, AppTheme.S(8), AppTheme.S(6)) };
        var title = new Label { Text = group.Name, Dock = DockStyle.Top, Height = AppTheme.S(22), ForeColor = AppTheme.Ink, AutoEllipsis = true };
        title.Font = Ui.OwnFont(title, AppTheme.Font(9, true));
        var notes = new Label { Text = $"{group.Done}/{group.Required} TC đã đạt · Còn thiếu {Math.Max(0, group.Required - group.Done)}\n{group.InProgress} đang học · {group.Planned} dự kiến", Dock = DockStyle.Bottom, Height = AppTheme.S(40), ForeColor = AppTheme.Muted, AutoEllipsis = true };
        var bar = new CreditProgressBar(group) { Dock = DockStyle.Fill };
        notes.Font = Ui.OwnFont(notes, AppTheme.Font(9));
        panel.AccessibleName = title.Text + ". " + notes.Text;
        panel.Controls.Add(bar); panel.Controls.Add(notes); panel.Controls.Add(title); groupCards!.Controls.Add(panel); GroupCardCount++;
    }

    private static Control Metric(string value, string label, Color fill)
    {
        var panel = new SoftSurface { Radius = 10, Dock = DockStyle.Fill, BackColor = fill, Margin = new(AppTheme.S(3)), Padding = new(AppTheme.S(10), AppTheme.S(4), AppTheme.S(10), AppTheme.S(8)) };
        var number = new Label { Text = value, Dock = DockStyle.Top, Height = AppTheme.S(23), ForeColor = AppTheme.Ink };
        number.Font = Ui.OwnFont(number, AppTheme.Font(12, true));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = AppTheme.Muted, AutoEllipsis = true }); panel.Controls.Add(number); return panel;
    }

    private void AddAction(string courseName, string hint, string code, Color fill)
    {
        var action = new ShowcaseButton { Name = "btnAcademicNext_" + code, Text = courseName + "\n" + hint, Height = AppTheme.S(68), FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, BackColor = fill, ForeColor = AppTheme.Ink, Padding = new(AppTheme.S(6)), Margin = new(0, 0, AppTheme.S(8), AppTheme.S(6)), AutoEllipsis = true, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        action.FlatAppearance.BorderColor = AppTheme.Line; action.AccessibleName = courseName + ". " + hint;
        action.Click += (_, _) => CourseSelected?.Invoke(code); nextSteps!.Controls.Add(action);
    }

    private void AddText(string value, int height, bool bold = false, Color? fill = null)
    {
        var label = new Label { Text = value, Height = AppTheme.S(height), BackColor = fill ?? Color.White, ForeColor = bold ? AppTheme.Ink : AppTheme.Muted, Padding = new(AppTheme.S(5), AppTheme.S(2), AppTheme.S(5), AppTheme.S(2)), Margin = new(0, 0, 0, AppTheme.S(3)), AutoEllipsis = true };
        label.Font = Ui.OwnFont(label, AppTheme.Font(bold ? 10 : 9, bold)); content.Controls.Add(label);
    }

    private void FitWidth()
    {
        var width = Math.Max(AppTheme.S(200), content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - AppTheme.S(3));
        foreach (Control child in content.Controls) child.Width = width;
        FitCards(groupCards, width >= AppTheme.S(690) ? 3 : width >= AppTheme.S(440) ? 2 : 1, AppTheme.S(84));
        FitCards(nextSteps, width >= AppTheme.S(600) ? 2 : 1, AppTheme.S(68));
    }

    private static void FitCards(FlowLayoutPanel? row, int columns, int cardHeight)
    {
        if (row is null) return;
        var gap = AppTheme.S(8); var cardWidth = Math.Max(AppTheme.S(150), (row.Width - columns * gap) / columns);
        row.SuspendLayout();
        foreach (Control child in row.Controls) child.Size = new(cardWidth, cardHeight);
        row.Height = (int)Math.Ceiling(row.Controls.Count / (double)columns) * (cardHeight + AppTheme.S(6));
        row.ResumeLayout(true);
    }

    internal static string CourseName(AcademicPlanningSnapshot snapshot, string code)
    {
        var choices = snapshot.Data.Tables["CourseChoices"];
        var row = choices?.Rows.Cast<DataRow>().FirstOrDefault(r => Convert.ToString(r["course"]) == code);
        return row is null ? code : Convert.ToString(row["name"]) ?? code;
    }

    internal static string ProgramName(AcademicPlanningSnapshot snapshot)
    {
        var row = snapshot.Data.Tables["Programs"]?.Rows.Cast<DataRow>().FirstOrDefault(r => Convert.ToString(r["id"]) == snapshot.SelectedProgram);
        return row is null ? snapshot.SelectedProgram ?? "Chương trình đào tạo" : Convert.ToString(row["name"]) ?? snapshot.SelectedProgram!;
    }

    private static string HumanMessage(AcademicPlanningSnapshot snapshot, string message)
    {
        foreach (var rule in snapshot.Rules.Select(r => r.RequiredCourse).Distinct().OrderByDescending(c => c.Length)) message = message.Replace(" " + rule + ".", " " + CourseName(snapshot, rule) + ".", StringComparison.Ordinal).Replace(" " + rule + " hoặc", " " + CourseName(snapshot, rule) + " hoặc", StringComparison.Ordinal);
        return message.Replace("PRIOR ", "học trước ", StringComparison.Ordinal).Replace("PRE ", "tiên quyết ", StringComparison.Ordinal).Replace("CO ", "đồng hành ", StringComparison.Ordinal);
    }

    private sealed class CreditProgressBar(DegreeGroupProgress progress) : Control
    {
        public CreditProgressBar() : this(new("", "", 0, 0, 0, 0)) { }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = new Rectangle(0, Math.Max(0, Height / 2 - AppTheme.S(4)), Width, AppTheme.S(8));
            using var track = new SolidBrush(AppTheme.Line); e.Graphics.FillRectangle(track, rect);
            if (progress.Required == 0) return;
            var remaining = progress.Required; float x = 0;
            foreach (var stage in new[] { (progress.Done, AppTheme.Green), (progress.InProgress, Color.FromArgb(139, 111, 195)), (progress.Planned, Color.FromArgb(138, 205, 209)) })
            {
                var amount = Math.Clamp(stage.Item1, 0, remaining); var width = Width * amount / (float)progress.Required;
                using var brush = new SolidBrush(stage.Item2); e.Graphics.FillRectangle(brush, x, rect.Y, width, rect.Height); x += width; remaining -= amount;
            }
        }
    }
}
