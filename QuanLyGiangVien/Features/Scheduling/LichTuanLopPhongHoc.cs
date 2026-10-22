using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>A scoped period grid with bounded overlap summaries and accessible details.</summary>
internal sealed class WeeklySchedule : UserControl
{
    private readonly System.Windows.Forms.Panel viewport = new() { AutoScroll = true, BackColor = Color.White };
    private readonly PeriodDay[] days = Enumerable.Range(0, 7).Select(_ => new PeriodDay()).ToArray();
    private readonly List<WeeklySessionButton> cards = [];
    private readonly List<Button> blocks = [];
    private readonly Label notice = new() { AutoEllipsis = true, ForeColor = SchedulingTheme.Muted, BackColor = Color.White };
    private readonly CalendarAxis header = new(), periods = new();
    private readonly Font smallFont = AppTheme.Font(8), dayFont = AppTheme.Font(9, true);
    private IReadOnlyList<TimetableDisplayGroup> groups = [];
    private ScheduleStore? currentStore;
    private WeeklyOverlapDialog? groupDialog;
    private DateOnly monday;
    private bool arranging;
    private int rowHeight;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal SchedulingVisualCatalog? Visuals { get; set; }
    internal event Action<CourseClass>? ClassSelected;
    internal int OccurrenceCount => cards.Count;
    internal int RenderedBlockCountForCheck => blocks.Count;
    internal int HorizontalExtentForCheck => viewport.AutoScrollMinSize.Width;
    internal Form? OpenedGroupDialogForCheck => groupDialog;
    internal bool CardsReadableForCheck => blocks.All(block => block is WeeklySessionButton card
        ? card.ContentFitsForCheck : block.Width >= AppTheme.S(130) && block.Height >= AppTheme.S(38));
    internal int VisualThumbnailsForCheck => blocks.OfType<WeeklySessionButton>().Count(card => card.HasThumbnail);
    internal bool ThumbnailsFitForCheck => blocks.OfType<WeeklySessionButton>().All(card => card.ThumbnailFitsForCheck);
    internal bool HasPeriodAxisForCheck => rowHeight >= AppTheme.S(44) && periods.Visible;
    internal bool HorizontalScrollForCheck => viewport.HorizontalScroll.Visible;
    internal bool HeadersTrackScrollForCheck => header.ScrollOffset == viewport.AutoScrollPosition.X
        && periods.ScrollOffset == viewport.AutoScrollPosition.Y;
    internal IReadOnlyList<(ScheduledOccurrence Occurrence, Rectangle Bounds, string[] Conflicts)> PlacementsForCheck =>
        cards.Select((card, index) => {
            int groupIndex = Enumerable.Range(0, groups.Count).First(i => groups[i].SessionIndexes.Contains(index));
            var block = blocks[groupIndex];
            return (card.Occurrence, new Rectangle(block.Parent!.Left - viewport.AutoScrollPosition.X + block.Left,
                block.Top, block.Width, block.Height), card.Conflicts.ToArray());
        }).ToArray();
    internal void ScrollForCheck(int x, int y) { viewport.AutoScrollPosition = new(x, y); SyncAxes(); }
    internal void ActivateSessionForCheck(int index) => ClassSelected?.Invoke(cards[index].Occurrence.Class);
    internal void OpenBlockForCheck(int groupIndex) => blocks[groupIndex].PerformClick();
    internal void ScrollFirstDayForCheck() => ScrollForCheck(0, rowHeight * 6);
    internal void ActivateFirstSessionForCheck() => ActivateSessionForCheck(0);
    internal bool FirstDayAlignedForCheck => groups.Select((group, index) => blocks[index].Top ==
        (group.StartPeriod - 1) * rowHeight + AppTheme.S(4)).All(value => value);

    internal WeeklySchedule()
    {
        BackColor = Color.White;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleName = "Lịch giảng dạy theo ngày và tiết học";
        notice.Font = smallFont;
        header.PaintAxis = PaintDays; periods.PaintAxis = PaintPeriods;
        viewport.Controls.AddRange(days);
        // Keyboard focus can scroll a native child into view without a Scroll event.
        foreach (var day in days) day.LocationChanged += (_, _) => SyncAxes();
        Controls.AddRange([viewport, header, periods, notice]);
        viewport.Scroll += (_, _) => SyncAxes();
        Resize += (_, _) => LayoutGrid();
    }

    internal void SetData(ScheduleStore store, IReadOnlyList<ScheduledOccurrence> sessions, DateOnly weekStart,
        IReadOnlyList<ScheduledOccurrence>? allSessions = null, bool personalSchedule = false, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sessions);
        SuspendLayout();
        CloseGroupDialog();
        foreach (var block in blocks) if (block is not WeeklySessionButton) block.Dispose();
        blocks.Clear();
        foreach (var card in cards) card.Dispose();
        cards.Clear(); monday = weekStart; currentStore = store;
        // Do not turn an off-week lesson into a weekly recurrence.
        var visible = sessions.Where(s => s.Date.DayNumber >= monday.DayNumber
            && s.Date.DayNumber - monday.DayNumber < 7).ToArray();
        groups = TimetableScope.GroupForDisplay(visible);
        var complete = allSessions ?? visible;
        var global = WeeklyTimetableLayout.Arrange(complete);
        foreach (var session in visible)
        {
            var course = store.Catalog.Courses.First(c => c.Id == session.Class.CourseId);
            var lecturer = store.Catalog.Lecturers.FirstOrDefault(l => ScheduleRules.Same(l.Id, session.LecturerId));
            var conflicts = global.Where(p => complete[p.SessionIndex] == session).SelectMany(p => p.Conflicts)
                .Concat(personalSchedule ? visible.Where(other => other != session && other.Date == session.Date
                    && other.Meeting.StartPeriod <= session.Meeting.EndPeriod && session.Meeting.StartPeriod <= other.Meeting.EndPeriod)
                    .Select(other => $"Trùng lịch sinh viên với lớp {other.Class.Id}.") : [])
                .Distinct().ToArray();
            var tone = session.Meeting.StartPeriod <= 4 ? SchedulingTheme.Cyan
                : session.Meeting.StartPeriod <= 8 ? SchedulingTheme.Lavender : SchedulingTheme.Rose;
            var card = new WeeklySessionButton(session, course.Name, lecturer?.Name ?? session.LecturerId,
                conflicts, tone, Visuals?.GetCourse(course).Image);
            card.Click += (_, _) => ClassSelected?.Invoke(session.Class);
            cards.Add(card);
        }
        for (int index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            int selectedGroup = index;
            Button block;
            if (group.SessionIndexes.Length == 1) block = cards[group.SessionIndexes[0]];
            else
            {
                bool warning = group.SessionIndexes.Any(i => cards[i].Conflicts.Count > 0);
                block = new WeeklyGroupButton(group.SessionIndexes.Length, group.StartPeriod, group.EndPeriod, warning);
                block.Click += (_, _) => OpenGroup(selectedGroup);
            }
            block.Enter += (_, _) => { if (IsHandleCreated) BeginInvoke(() => { if (!IsDisposed) SyncAxes(); }); };
            blocks.Add(block); days[group.Date.DayNumber - monday.DayNumber].Controls.Add(block);
        }
        int conflictCount = cards.Count(c => c.Conflicts.Count > 0);
        string summary = cards.Count == 0 ? "Tuần này chưa có lịch trong phạm vi đã chọn."
            : conflictCount > 0 ? $"{cards.Count} buổi · {conflictCount} buổi có xung đột. Chọn ô lịch để xem chi tiết."
            : $"{cards.Count} buổi · Chọn ô có nhiều buổi giao giờ để xem đầy đủ.";
        notice.Text = cards.Count == 0 && !string.IsNullOrWhiteSpace(caption) ? caption
            : string.IsNullOrWhiteSpace(caption) ? summary : $"{caption} · {summary}";
        notice.ForeColor = conflictCount > 0 ? SchedulingTheme.Danger : SchedulingTheme.Muted;
        notice.AccessibleName = notice.Text;
        ResumeLayout(false); LayoutGrid();
    }

    private void LayoutGrid()
    {
        if (arranging || Width <= 0 || Height <= 0) return;
        arranging = true;
        try
        {
            int s(int n) => AppTheme.S(n);
            int gutter = s(48), noticeHeight = s(36), headingHeight = s(42);
            notice.SetBounds(0, 0, Width, noticeHeight);
            viewport.SetBounds(gutter, noticeHeight + headingHeight,
                Math.Max(1, Width - gutter), Math.Max(1, Height - noticeHeight - headingHeight));
            header.SetBounds(gutter, noticeHeight, viewport.Width, headingHeight);
            periods.SetBounds(0, noticeHeight + headingHeight, gutter, viewport.Height);
            rowHeight = s(48);
            int canvasHeight = 12 * rowHeight + s(2);
            var originalScroll = new Point(-viewport.AutoScrollPosition.X, -viewport.AutoScrollPosition.Y);
            viewport.SuspendLayout(); viewport.AutoScrollPosition = Point.Empty;
            int available = Math.Max(1, viewport.Width - SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi));
            int columnWidth = Math.Max(s(140), available / 7);
            int x = 0;
            for (int day = 0; day < 7; day++)
            {
                int width = columnWidth;
                days[day].RowHeight = rowHeight;
                days[day].SetBounds(x, 0, width, canvasHeight);
                days[day].Invalidate();
                x += width;
            }
            for (int index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                blocks[index].SetBounds(s(4), (group.StartPeriod - 1) * rowHeight + s(4),
                    Math.Max(1, columnWidth - s(8)), (group.EndPeriod - group.StartPeriod + 1) * rowHeight - s(8));
            }
            viewport.AutoScrollMinSize = new(x, canvasHeight);
            viewport.ResumeLayout(true); viewport.AutoScrollPosition = originalScroll;
            SyncAxes();
        }
        finally { arranging = false; }
    }

    private void SyncAxes()
    {
        header.ScrollOffset = viewport.AutoScrollPosition.X;
        periods.ScrollOffset = viewport.AutoScrollPosition.Y;
        header.Invalidate(); periods.Invalidate();
    }

    private void OpenGroup(int index)
    {
        if (currentStore is null) return;
        CloseGroupDialog();
        var group = groups[index];
        var items = group.SessionIndexes.Select(i => (cards[i].Occurrence, cards[i].Conflicts)).ToArray();
        var dialog = new WeeklyOverlapDialog(currentStore, group, items, selected => {
            CloseGroupDialog();
            ClassSelected?.Invoke(selected);
        });
        groupDialog = dialog;
        dialog.FormClosed += (_, _) => { if (ReferenceEquals(groupDialog, dialog)) groupDialog = null; };
        if (FindForm() is { } owner) dialog.Show(owner); else dialog.Show();
    }

    private void CloseGroupDialog()
    {
        var dialog = groupDialog;
        groupDialog = null;
        if (dialog is null || dialog.IsDisposed) return;
        dialog.Close(); dialog.Dispose();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) CloseGroupDialog();
    }

    private void PaintDays(Graphics g)
    {
        using var line = new Pen(SchedulingTheme.Line);
        for (int day = 0; day < 7; day++)
        {
            var column = days[day];
            var rect = new Rectangle(column.Left, 0, column.Width, header.Height);
            var date = monday.AddDays(day);
            if (date == DateOnly.FromDateTime(DateTime.Today))
            {
                using var brush = new SolidBrush(AppTheme.SubtleLilac);
                g.FillRectangle(brush, rect);
            }
            string text = (day == 6 ? "Chủ nhật" : $"Thứ {day + 2}") + $" · {date:dd/MM}";
            TextRenderer.DrawText(g, text, dayFont, rect, SchedulingTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            g.DrawLine(line, rect.Right - 1, 0, rect.Right - 1, rect.Bottom);
        }
    }

    private void PaintPeriods(Graphics g)
    {
        using var line = new Pen(SchedulingTheme.Line);
        for (int period = 0; period < 12; period++)
        {
            int y = periods.ScrollOffset + period * rowHeight;
            TextRenderer.DrawText(g, $"Tiết {period + 1}", smallFont,
                new Rectangle(0, y, periods.Width - AppTheme.S(3), rowHeight), SchedulingTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            g.DrawLine(line, 0, y, periods.Width, y);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseGroupDialog();
            // Group members are metadata controls until their summary is opened.
            foreach (var card in cards) if (card.Parent is null) card.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) { smallFont.Dispose(); dayFont.Dispose(); }
    }

    private sealed class CalendarAxis : Control
    {
        internal int ScrollOffset;
        internal Action<Graphics>? PaintAxis;
        internal CalendarAxis() { DoubleBuffered = true; BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintAxis?.Invoke(e.Graphics); }
    }

    private sealed class PeriodDay : System.Windows.Forms.Panel
    {
        internal int RowHeight;
        internal PeriodDay()
        {
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (RowHeight <= 0) return;
            using var line = new Pen(SchedulingTheme.Line);
            for (int period = 0; period <= 12; period++)
                e.Graphics.DrawLine(line, 0, period * RowHeight, Width, period * RowHeight);
            e.Graphics.DrawLine(line, Width - 1, 0, Width - 1, Height);
        }
    }
}
