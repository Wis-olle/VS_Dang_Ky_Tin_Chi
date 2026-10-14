using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>
/// Native adaptation of the rounded bar, hover/focus and keyboard pattern from
/// https://21st.dev/@kevingirelli/components/bar-chart. No React dependencies.
/// Source: KevinGirelli/corsair-ui registry/default/ui/bar-chart.tsx, MIT.
/// Copyright (c) 2026 Kevin Girelli. The source MIT notice is retained below.
/// </summary>
internal sealed class ScheduleAnalytics : Control
{
    private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    private readonly Motion reveal = new(), hoverMotion = new();
    private readonly ToolTip tooltip = new() { InitialDelay = 250, ReshowDelay = 100, AutoPopDelay = 5000 };
    private Font titleFont = AppTheme.Font(10, true), labelFont = AppTheme.Font(8), numberFont = AppTheme.Font(8, true);
    private int[] counts = new int[7];
    private readonly float[] displayed = new float[7], highlights = new float[7];
    private readonly RectangleF[] bars = new RectangleF[7], hitboxes = new RectangleF[7];
    private DateOnly week;
    private bool hasData;
    private int activeDay = -1, titleHeight, dayHeight, numberHeight;
    private float maximum = 1, metricsDpi = -1, metricsScale = -1;
    private Size metricsSize;
    private RectangleF plot;
    private Rectangle titleBounds, badgeBounds;
    private int labelTop;
    internal int TotalSessions => counts.Sum();
    internal IReadOnlyList<int> CountsForCheck => Array.AsReadOnly(counts.ToArray());
    internal IReadOnlyList<float> DisplayedCountsForCheck => Array.AsReadOnly(displayed.ToArray());
    internal IReadOnlyList<Font> OwnedFontsForCheck => Array.AsReadOnly(new[] { titleFont, labelFont, numberFont });
    internal IReadOnlyList<RectangleF> BarBoundsForCheck { get { EnsureForRead(); return Array.AsReadOnly(bars.ToArray()); } }
    internal bool ZeroHasNoFillForCheck { get { EnsureForRead(); return bars.Where((bar, index) => counts[index] == 0).All(bar => bar.Height <= .001f); } }
    internal bool MotionRunning => reveal.IsRunning || hoverMotion.IsRunning;
    internal int ActiveDayForCheck => activeDay;
    internal void NavigateForCheck(Keys key) => Navigate(key);
    internal void SetActiveDayForCheck(int index) => SetActiveDay(index);

    internal ScheduleAnalytics()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.White; TabStop = true; AccessibleRole = AccessibleRole.Chart;
        AccessibleName = "Số buổi học theo ngày trong tuần";
    }

    internal void SetData(IReadOnlyList<ScheduledOccurrence> sessions, DateOnly monday, bool animate)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var next = Enumerable.Range(0, 7).Select(index => sessions.Count(item => item.Date == monday.AddDays(index))).ToArray();
        bool same = hasData && week == monday && next.SequenceEqual(counts);
        if (same) { if (!animate || !AntdUI.Config.Animation) FinishData(); return; }
        var from = displayed.ToArray(); float fromMaximum = maximum;
        counts = next; week = monday; hasData = true;
        float targetMaximum = Math.Max(1, counts.Max());
        AccessibleDescription = string.Join("; ", Enumerable.Range(0, 7).Select(DescribeDay));
        UpdateTooltip();
        if (animate && Visible && !from.SequenceEqual(counts.Select(value => (float)value)))
            reveal.Run(500, progress => {
                for (int index = 0; index < 7; index++) displayed[index] = from[index] + (counts[index] - from[index]) * progress;
                maximum = fromMaximum + (targetMaximum - fromMaximum) * progress;
                UpdateBars(); Invalidate();
            });
        else { reveal.Cancel(false); for (int index = 0; index < 7; index++) displayed[index] = counts[index]; maximum = targetMaximum; UpdateBars(); Invalidate(); }
    }

    private void FinishData()
    {
        reveal.Cancel(false);
        for (int index = 0; index < 7; index++) displayed[index] = counts[index];
        maximum = Math.Max(1, counts.Max()); UpdateBars(); Invalidate();
    }

    private string DescribeDay(int index) => $"{(index == 6 ? "Chủ nhật" : $"Thứ {index + 2}")} · {week.AddDays(index):dd/MM/yyyy}: {counts[index]} buổi";
    private int TodayIndex => hasData && week.DayOfWeek == DayOfWeek.Monday
        ? DateOnly.FromDateTime(DateTime.Today).DayNumber - week.DayNumber : -1;

    private void SetActiveDay(int index)
    {
        if (index is < -1 or > 6) throw new ArgumentOutOfRangeException(nameof(index));
        if (IsDisposed || Disposing || activeDay == index) return;
        activeDay = index;
        var from = highlights.ToArray();
        if (Visible) hoverMotion.Run(180, progress => {
            for (int day = 0; day < 7; day++) highlights[day] = from[day] + ((day == activeDay ? 1 : 0) - from[day]) * progress;
            Invalidate();
        });
        else {
            hoverMotion.Cancel(false);
            for (int day = 0; day < 7; day++) highlights[day] = day == activeDay ? 1 : 0;
        }
        AccessibleName = index < 0 ? "Số buổi học theo ngày trong tuần" : "Số buổi học theo ngày trong tuần · " + DescribeDay(index);
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        if (activeDay < 0 || !hasData) { tooltip.Hide(this); tooltip.SetToolTip(this, null); return; }
        tooltip.SetToolTip(this, DescribeDay(activeDay));
        if (Focused && Visible && IsHandleCreated)
        {
            EnsureForRead();
            tooltip.Show(DescribeDay(activeDay), this, new Point((int)hitboxes[activeDay].Left, Math.Max(0, labelTop - AppTheme.S(22))), 2500);
        }
    }

    private bool Navigate(Keys key)
    {
        int current = activeDay >= 0 ? activeDay : TodayIndex is >= 0 and < 7 ? TodayIndex : 0;
        int next = key switch {
            Keys.Left or Keys.Down or Keys.PageDown => Math.Max(0, current - 1),
            Keys.Right or Keys.Up or Keys.PageUp => Math.Min(6, current + 1),
            Keys.Home => 0, Keys.End => 6, _ => -1
        };
        if (next < 0) return false;
        SetActiveDay(next); return true;
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down
        or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (Navigate(e.KeyCode)) e.Handled = e.SuppressKeyPress = true; }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (activeDay < 0) SetActiveDay(TodayIndex is >= 0 and < 7 ? TodayIndex : 0); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); SetActiveDay(-1); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); EnsureForRead();
        int day = Array.FindIndex(hitboxes, bounds => bounds.Contains(e.Location));
        if (day >= 0) SetActiveDay(day); else if (!Focused) SetActiveDay(-1);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Focus(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); tooltip.Hide(this); if (!Focused) SetActiveDay(-1); }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) {
            FinishData(); hoverMotion.Cancel(false); Array.Clear(highlights); activeDay = -1;
            AccessibleName = "Số buổi học theo ngày trong tuần"; tooltip.Hide(this);
        }
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); metricsSize = Size.Empty; tooltip.Hide(this); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); metricsDpi = -1; tooltip.Hide(this); }

    private void EnsureForRead()
    {
        if (metricsSize != Size || metricsScale != AppTheme.K || metricsDpi < 0)
        { using var graphics = CreateGraphics(); EnsureGeometry(graphics); }
        UpdateBars();
    }

    private void EnsureGeometry(Graphics graphics)
    {
        bool dpiChanged = metricsDpi != graphics.DpiX || metricsScale != AppTheme.K;
        if (!dpiChanged && metricsSize == Size) return;
        if (dpiChanged) {
            titleFont.Dispose(); labelFont.Dispose(); numberFont.Dispose();
            titleFont = AppTheme.Font(10, true); labelFont = AppTheme.Font(8); numberFont = AppTheme.Font(8, true);
        }
        metricsDpi = graphics.DpiX; metricsScale = AppTheme.K; metricsSize = Size;
        titleHeight = TextRenderer.MeasureText(graphics, "Ag", titleFont, Size.Empty, TextFlags).Height;
        dayHeight = TextRenderer.MeasureText(graphics, "T2", labelFont, Size.Empty, TextFlags).Height;
        numberHeight = TextRenderer.MeasureText(graphics, "88", numberFont, Size.Empty, TextFlags).Height;
        int pad = AppTheme.S(8), headerHeight = Math.Max(titleHeight, numberHeight) + AppTheme.S(4);
        titleBounds = new(pad, AppTheme.S(2), Math.Max(1, Width - pad * 2 - AppTheme.S(65)), titleHeight);
        badgeBounds = new(Width - pad - AppTheme.S(60), 0, AppTheme.S(60), headerHeight);
        labelTop = Height - dayHeight - AppTheme.S(1);
        int plotTop = headerHeight + numberHeight + AppTheme.S(7), plotBottom = labelTop - AppTheme.S(4);
        plot = new(pad, plotTop, Math.Max(1, Width - pad * 2), Math.Max(0, plotBottom - plotTop));
        float slot = plot.Width / 7;
        for (int index = 0; index < 7; index++) hitboxes[index] = new(plot.Left + slot * index,
            headerHeight, slot, Math.Max(1, Height - headerHeight));
        UpdateBars();
    }

    private void UpdateBars()
    {
        float slot = plot.Width / 7, width = Math.Min(slot * .6f, Math.Clamp(slot * .48f, AppTheme.S(18), AppTheme.S(22)));
        for (int index = 0; index < 7; index++) {
            float height = plot.Height * Math.Clamp(displayed[index] / Math.Max(1, maximum), 0, 1);
            bars[index] = new(plot.Left + slot * (index + .5f) - width / 2, plot.Bottom - height, width, height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2) return;
        if (!AntdUI.Config.Animation && MotionRunning) { FinishData(); hoverMotion.Cancel(); }
        var graphics = e.Graphics; graphics.SmoothingMode = SmoothingMode.AntiAlias;
        EnsureGeometry(graphics); UpdateBars();
        TextRenderer.DrawText(graphics, "Nhịp học tuần", titleFont, titleBounds, SchedulingTheme.Ink, TextFlags | TextFormatFlags.EndEllipsis);
        using (var badge = AppTheme.Round(badgeBounds, AppTheme.S(7))) {
            using var tone = new SolidBrush(SchedulingTheme.Rose); graphics.FillPath(tone, badge);
            TextRenderer.DrawText(graphics, $"{TotalSessions} buổi", numberFont, badgeBounds, SchedulingTheme.Primary,
                TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        using var guide = new Pen(Color.FromArgb(120, SchedulingTheme.Line), 1);
        for (int line = 0; line < 3; line++) {
            float y = plot.Top + plot.Height * line / 2;
            graphics.DrawLine(guide, plot.Left, y, plot.Right, y);
        }
        for (int index = 0; index < 7; index++) DrawDay(graphics, index);
    }

    private void DrawDay(Graphics graphics, int index)
    {
        var bar = bars[index]; float emphasis = highlights[index];
        bool today = index == TodayIndex, strongest = counts[index] > 0 && counts[index] == counts.Max();
        var trackBounds = new RectangleF(bar.Left, plot.Top, bar.Width, plot.Height);
        if (plot.Height > 0) {
            using var track = TopRounded(trackBounds, AppTheme.S(5));
            using var background = new SolidBrush(today ? SchedulingTheme.Lavender : Color.FromArgb(130, SchedulingTheme.Canvas));
            graphics.FillPath(background, track);
        }
        if (bar.Height > .01f) {
            using var shape = TopRounded(bar, AppTheme.S(5));
            Color color = strongest || today ? AppTheme.Primary : AppTheme.Accent;
            using var fill = new SolidBrush(Blend(color, AppTheme.PrimaryPressed, emphasis * .35f)); graphics.FillPath(fill, shape);
        }
        int center = (int)Math.Round(bar.Left + bar.Width / 2), top = Math.Max((int)Math.Ceiling(bar.Top) - numberHeight - AppTheme.S(3), badgeBounds.Bottom + AppTheme.S(2));
        TextRenderer.DrawText(graphics, counts[index].ToString(), numberFont, new Rectangle(center - AppTheme.S(18), top, AppTheme.S(36), numberHeight),
            counts[index] == 0 ? SchedulingTheme.Muted : SchedulingTheme.Ink, TextFlags | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(graphics, index == 6 ? "CN" : $"T{index + 2}", labelFont,
            new Rectangle(center - AppTheme.S(18), labelTop, AppTheme.S(36), dayHeight),
            today || emphasis > .5f ? SchedulingTheme.Primary : SchedulingTheme.Muted, TextFlags | TextFormatFlags.HorizontalCenter);
        if (emphasis > .01f) {
            using var outline = new Pen(Color.FromArgb((int)(emphasis * 100), SchedulingTheme.Primary), 1);
            var focus = hitboxes[index]; focus.Inflate(-AppTheme.S(2), -AppTheme.S(1));
            using var shape = AppTheme.Round(focus, AppTheme.S(6)); graphics.DrawPath(outline, shape);
        }
    }

    private static GraphicsPath TopRounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Top + diameter / 2);
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddLine(bounds.Right, bounds.Top + diameter / 2, bounds.Right, bounds.Bottom); path.CloseFigure(); return path;
    }

    private static Color Blend(Color from, Color to, float progress) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * progress), (int)(from.G + (to.G - from.G) * progress), (int)(from.B + (to.B - from.B) * progress));
    protected override void Dispose(bool disposing)
    {
        if (disposing) { reveal.Dispose(); hoverMotion.Dispose(); tooltip.Dispose(); titleFont.Dispose(); labelFont.Dispose(); numberFont.Dispose(); }
        base.Dispose(disposing);
    }
}

/* Source pattern license:
MIT License — Copyright (c) 2026 Kevin Girelli
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
