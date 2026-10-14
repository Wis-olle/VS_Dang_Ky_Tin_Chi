using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.StudentTimetable.TimetableStyle;

namespace QuanLyGiangVien.Features.StudentTimetable;

internal sealed class StudentWeekCalendar : Control
{
    private readonly List<SessionCard> cards = [];
    private readonly Font dayFont = AppTheme.Font(9, true), smallFont = AppTheme.Font(8);
    private DateOnly monday;
    internal event Action<ScheduledOccurrence>? SessionSelected;
    internal IReadOnlyList<SessionCard> Cards => cards;
    internal int GridTop => S(64);
    internal StudentWeekCalendar()
    {
        DoubleBuffered = true; BackColor = Color.White; TabStop = false;
        SetStyle(ControlStyles.ResizeRedraw, true);
        AccessibleName = "Thời khóa biểu tuần, từ thứ hai đến chủ nhật";
    }
    internal void SetWeek(ScheduleStore store, DateOnly start, IReadOnlyList<ScheduledOccurrence> occurrences)
    {
        SuspendLayout();
        foreach (var card in cards) card.Dispose();
        cards.Clear(); monday = start;
        foreach (var occurrence in occurrences)
        {
            var course = store.Catalog.Courses.First(c => c.Id == occurrence.Class.CourseId);
            var lecturer = store.Catalog.Lecturers.FirstOrDefault(l => l.Id == occurrence.LecturerId)?.Name ?? occurrence.LecturerId;
            var card = new SessionCard(occurrence, course.Name, lecturer);
            card.Click += (_, _) => SelectSession(card);
            cards.Add(card); Controls.Add(card);
        }
        if (cards.Count > 0) cards[0].Selected = true;
        ResumeLayout(); LayoutCards(); Invalidate();
    }
    internal void SelectSession(SessionCard selected)
    {
        foreach (var card in cards) { card.Selected = card == selected; card.Invalidate(); }
        SessionSelected?.Invoke(selected.Occurrence);
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutCards(); }
    private void LayoutCards()
    {
        float column = Math.Max(1, Width - S(52)) / 7f;
        float row = Math.Max(1, Height - GridTop - S(8)) / 12f;
        foreach (var dayCards in cards.GroupBy(c => c.Occurrence.Date))
        {
            var lanes = new List<int>();
            var assignments = new List<(SessionCard Card, int Lane)>();
            foreach (var card in dayCards.OrderBy(c => c.Occurrence.Meeting.StartPeriod).ThenBy(c => c.Occurrence.Class.Id))
            {
                var meeting = card.Occurrence.Meeting;
                int lane = lanes.FindIndex(end => end < meeting.StartPeriod);
                if (lane < 0) { lane = lanes.Count; lanes.Add(meeting.EndPeriod); }
                else lanes[lane] = meeting.EndPeriod;
                assignments.Add((card, lane));
            }
            foreach (var (card, lane) in assignments)
            {
                int day = card.Occurrence.Date.DayNumber - monday.DayNumber;
                var meeting = card.Occurrence.Meeting;
                float laneWidth = column / lanes.Count;
                card.SetBounds((int)(S(52) + day * column + lane * laneWidth + S(4)),
                    (int)(GridTop + (meeting.StartPeriod - 1) * row + S(4)),
                    Math.Max(1, (int)laneWidth - S(8)), Math.Max(1, (int)((meeting.EndPeriod - meeting.StartPeriod + 1) * row) - S(8)));
            }
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float col = Math.Max(1, Width - S(52)) / 7f;
        float row = Math.Max(1, Height - GridTop - S(8)) / 12f;
        using var line = new Pen(Line);
        for (int day = 0; day < 7; day++)
        {
            var date = monday.AddDays(day);
            var rect = new Rectangle((int)(S(52) + day * col), S(8), (int)col, S(44));
            if (date == DateOnly.FromDateTime(DateTime.Today))
            {
                using var path = AppTheme.Round(rect, S(8)); using var fill = new SolidBrush(Soft); g.FillPath(fill, path);
            }
            TextRenderer.DrawText(g, day == 6 ? "CN" : $"Thứ {day + 2}", dayFont,
                rect with { Height = S(22) }, PurpleDark, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, date.ToString("dd/MM"), smallFont,
                rect with { Y = S(30), Height = S(18) }, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            g.DrawLine(line, S(52) + day * col, GridTop, S(52) + day * col, Height - S(8));
        }
        for (int period = 0; period <= 12; period++)
        {
            float y = GridTop + period * row;
            g.DrawLine(line, S(52), y, Width, y);
            if (period < 12) TextRenderer.DrawText(g, $"{period + 1:00}", smallFont,
                new Rectangle(0, (int)y, S(44), (int)row), Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        TextRenderer.DrawText(g, "Tiết", smallFont, new Rectangle(0, S(18), S(48), S(28)), Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (cards.Count == 0)
        {
            using var fill = new SolidBrush(Color.White);
            var area = new Rectangle(S(100), S(160), Width - S(150), S(100)); g.FillRectangle(fill, area);
            TextRenderer.DrawText(g, "Tuần này chưa có lịch học\nCác lớp đã đăng ký sẽ hiển thị tại đây.", dayFont,
                area, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { dayFont.Dispose(); smallFont.Dispose(); }
        base.Dispose(disposing);
    }

    internal sealed class SessionCard : Button
    {
        internal ScheduledOccurrence Occurrence { get; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Selected { get; set; }
        private readonly string course, lecturer;
        private readonly Font title = AppTheme.Font(9, true), small = AppTheme.Font(7.5f);
        private readonly Motion hover = new();
        private readonly ToolTip tooltip = new() { InitialDelay = 350, AutoPopDelay = 10000 };
        private float tint;
        internal bool IsAnimating => hover.IsRunning;
        internal void HoverForCheck(bool entered) => Animate(entered ? 1 : 0);
        internal void SpaceForCheck()
        {
            OnKeyDown(new KeyEventArgs(Keys.Space)); OnKeyUp(new KeyEventArgs(Keys.Space));
        }
        internal SessionCard(ScheduledOccurrence occurrence, string course, string lecturer)
        {
            Occurrence = occurrence; this.course = course; this.lecturer = lecturer;
            DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Color.White; Cursor = Cursors.Hand; TabStop = true;
            AccessibleName = $"{course}, {occurrence.Date:dd/MM/yyyy}, tiết {occurrence.Meeting.StartPeriod} đến {occurrence.Meeting.EndPeriod}, phòng {occurrence.RoomId}, {lecturer}";
            AccessibleDescription = "Bấm hoặc Enter/Space để xem chi tiết buổi học.";
            tooltip.SetToolTip(this, AccessibleName);
        }
        private void Animate(float target)
        {
            float start = tint;
            if (!Visible || !Enabled) { hover.Cancel(false); tint = target; return; }
            hover.Run(160, p => { tint = start + (target - start) * p; Invalidate(); });
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Animate(1); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Animate(0); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (!Visible) { hover.Cancel(false); tint = 0; } }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Color.White); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 3 || Height < 3) return;
            var background = Selected ? SelectedFill : SessionHover(tint);
            using var path = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), S(8));
            using var fill = new SolidBrush(background); g.FillPath(fill, path);
            using var border = new Pen(Selected ? SelectedFill : SessionLine); g.DrawPath(border, path);
            var color = Selected ? Color.White : Ink; var muted = Selected ? Color.White : Muted;
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            if (Width < S(28)) return; // Dense conflicts remain selectable and expose full metadata via accessibility/tooltip.
            if (Height < S(60))
            {
                TextRenderer.DrawText(g, course, small, new Rectangle(S(6), 0, Width - S(12), Height), color,
                    flags | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -S(3), -S(3)), color, background);
                return;
            }
            using var pill = AppTheme.Round(new RectangleF(S(8), S(8), Math.Min(S(52), Width - S(16)), S(20)), S(8));
            using var pillFill = new SolidBrush(Selected ? Color.White : SelectedFill);
            g.FillPath(pillFill, pill);
            TextRenderer.DrawText(g, Occurrence.RoomId, small, new Rectangle(S(9), S(8), Math.Min(S(50), Width - S(18)), S(20)),
                Selected ? SessionInk : Color.White, flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Height >= S(75))
            {
                TextRenderer.DrawText(g, course, title, new Rectangle(S(10), S(32), Width - S(20), Math.Min(S(46), Height - S(55))),
                    color, flags | TextFormatFlags.WordBreak);
                TextRenderer.DrawText(g, $"Tiết {Occurrence.Meeting.StartPeriod}–{Occurrence.Meeting.EndPeriod}", small,
                    new Rectangle(S(10), Height - S(23), Width - S(20), S(18)), muted, flags);
            }
            else TextRenderer.DrawText(g, course, small, new Rectangle(S(10), S(29), Width - S(20), S(20)), color, flags);
            if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -S(4), -S(4)), color, background);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { hover.Dispose(); tooltip.Dispose(); title.Dispose(); small.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
