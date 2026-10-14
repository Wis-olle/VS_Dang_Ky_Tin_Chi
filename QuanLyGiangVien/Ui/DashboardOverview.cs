using System.Drawing.Drawing2D;
using System.Diagnostics;
using QuanLyGiangVien.Domain;
using Label = AntdUI.Label;

namespace QuanLyGiangVien.Ui;

public sealed class DashboardOverview : AntdUI.Panel
{
    private readonly Label title = AppTheme.Label("Tình hình giảng dạy", 11, true);
    private readonly Label note = AppTheme.Label("Trạng thái hồ sơ hiện tại", 8, false, AppTheme.Muted);
    private readonly Label active = AppTheme.Label("", 9, true);
    private readonly Label paused = AppTheme.Label("", 9, false, AppTheme.Muted);
    private readonly LecturerStatusRing ring = new();

    public DashboardOverview()
    {
        Radius = 20;
        Back = Color.White;
        BackColor = Color.Transparent;
        Controls.AddRange([title, note, ring, active, paused]);
        Resize += (_, _) => Arrange();
    }
    public void Refresh(IReadOnlyList<Lecturer> lecturers)
    {
        int count = lecturers.Count(item => item.Status == "Đang giảng dạy");
        active.Text = $"{count} đang giảng dạy";
        paused.Text = $"{lecturers.Count(item => item.Status == "Tạm nghỉ")} tạm nghỉ";
        ring.ShowCounts(count, lecturers.Count);
        Arrange();
    }
    public void AnimateFromZero(int delayMilliseconds = 0) => ring.AnimateFromZero(delayMilliseconds);
    internal bool IsAnimating => ring.IsAnimating;
    internal float DisplayedFraction => ring.DisplayedFraction;
    internal float ActualFraction => ring.ActualFraction;
    internal float RingHoverEmphasis => ring.HoverEmphasis;
    internal double RingPaintMilliseconds => ring.PaintMilliseconds;
    internal void ResetHover() => ring.ResetHover();
    internal void SetRingHoverForCheck(bool hovered) => ring.SetHover(hovered);
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) ring.ResetHover();
    }
    private void Arrange()
    {
        int pad = AppTheme.S(22), width = Width - pad * 2;
        title.SetBounds(pad, AppTheme.S(17), width, AppTheme.S(25));
        note.SetBounds(pad, AppTheme.S(43), width, AppTheme.S(20));
        int size = Math.Min(AppTheme.S(104), Height - AppTheme.S(77));
        size = Math.Max(AppTheme.S(66), size);
        ring.SetBounds(pad, AppTheme.S(70), size, size);
        int x = pad + size + AppTheme.S(16), labelsWidth = Math.Max(AppTheme.S(80), Width - x - AppTheme.S(12));
        active.SetBounds(x, AppTheme.S(78), labelsWidth, AppTheme.S(32));
        paused.SetBounds(x, AppTheme.S(115), labelsWidth, AppTheme.S(28));
    }
}

internal sealed class LecturerStatusRing : Control
{
    private int active;
    private int total;
    private readonly Motion sweep = new();
    private readonly Motion hoverMotion = new();
    private float displayedFraction;
    private float targetFraction;
    private float hoverEmphasis;
    private RingResources? resources;
    private Pen? hoverFill;
    internal bool IsAnimating => sweep.IsRunning;
    internal float DisplayedFraction => displayedFraction;
    internal float ActualFraction => targetFraction;
    internal float HoverEmphasis => hoverEmphasis;
    internal double PaintMilliseconds { get; private set; }
    public LecturerStatusRing()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        BackColor = Color.White;
        AccessibleRole = AccessibleRole.Graphic;
    }
    public void ShowCounts(int activeCount, int totalCount)
    {
        float nextFraction = OverviewMotionMath.Fraction(activeCount, totalCount);
        active = activeCount;
        total = totalCount;
        AccessibleName = $"{active} trên {total} giảng viên đang giảng dạy";
        AccessibleDescription = total == 0 ? "Chưa có giảng viên" : $"Tỷ lệ thực tế: {Math.Round(nextFraction * 100)}%";
        if (targetFraction == nextFraction) { Invalidate(); return; }
        targetFraction = nextFraction;
        AnimateSweep(displayedFraction);
    }
    public void AnimateFromZero(int delayMilliseconds = 0) => AnimateSweep(0, delayMilliseconds);
    private void AnimateSweep(float from, int delayMilliseconds = 0)
    {
        sweep.Cancel(false);
        float to = targetFraction;
        if (!IsHandleCreated || !AntdUI.Config.Animation || from == to) { ShowFraction(to); return; }
        sweep.Run(650, progress => ShowFraction(from + (to - from) * progress), delayMilliseconds);
    }
    private void ShowFraction(float fraction)
    {
        displayedFraction = Math.Clamp(fraction, 0, 1);
        Invalidate();
    }
    internal void ResetHover()
    {
        if (IsDisposed || Disposing) return;
        hoverMotion.Cancel(false);
        if (hoverEmphasis == 0) return;
        hoverEmphasis = 0;
        Invalidate(ClientRectangle);
    }
    internal void SetHover(bool hovered)
    {
        if (IsDisposed || Disposing) return;
        if (!AntdUI.Config.Animation)
        {
            ResetHover();
            return;
        }
        float from = hoverEmphasis, target = hovered ? 1 : 0;
        hoverMotion.Run(160, p => { hoverEmphasis = from + (target - from) * p; Invalidate(ClientRectangle); });
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); SetHover(true); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHover(false); }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) ResetHover();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        long started = Stopwatch.GetTimestamp();
        try { PaintRing(e.Graphics); }
        finally { PaintMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
    }
    private void PaintRing(Graphics graphics)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var drawing = GetResources();
        int inset = drawing.Thickness / 2 + AppTheme.S(3);
        var rect = new Rectangle(inset, inset, Width - inset * 2, Height - inset * 2);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        graphics.DrawEllipse(drawing.Track, rect);
        if (displayedFraction > 0)
        {
            graphics.DrawArc(GetFill(drawing), rect, -90, displayedFraction * 360);
        }
        graphics.DrawString(total == 0 ? "—" : $"{Math.Round(displayedFraction * 100)}%", drawing.ValueFont,
            drawing.Ink, new RectangleF(0, Height * .25f, Width, Height * .37f), drawing.Format);
        graphics.DrawString("đang dạy", drawing.LabelFont, drawing.Muted,
            new RectangleF(0, Height * .58f, Width, Height * .2f), drawing.Format);
    }
    private Pen GetFill(RingResources drawing)
    {
        if (hoverEmphasis <= 0) return drawing.Fill;
        float width = drawing.Thickness + AppTheme.S(2) * hoverEmphasis;
        if (hoverFill == null || hoverFill.Width != width)
        {
            hoverFill?.Dispose();
            hoverFill = RingResources.ArcPen(AppTheme.Accent, width);
        }
        return hoverFill;
    }
    private RingResources GetResources()
    {
        if (resources == null || resources.Scale != AppTheme.K || resources.Dpi != DeviceDpi)
        {
            resources?.Dispose();
            hoverFill?.Dispose(); hoverFill = null;
            resources = new(AppTheme.K, DeviceDpi);
        }
        return resources;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            sweep.Dispose(); hoverMotion.Dispose();
            resources?.Dispose(); resources = null;
            hoverFill?.Dispose(); hoverFill = null;
        }
        base.Dispose(disposing);
    }

    private sealed class RingResources : IDisposable
    {
        internal readonly float Scale;
        internal readonly int Dpi;
        internal readonly int Thickness;
        internal readonly Pen Track;
        internal readonly Pen Fill;
        internal readonly Font ValueFont = AppTheme.Font(17, true);
        internal readonly Font LabelFont = AppTheme.Font(7.5f);
        internal readonly StringFormat Format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        internal readonly SolidBrush Ink = new(AppTheme.Ink);
        internal readonly SolidBrush Muted = new(AppTheme.Muted);
        internal RingResources(float scale, int dpi)
        {
            Scale = scale; Dpi = dpi; Thickness = Math.Max(1, AppTheme.S(10));
            Track = new(AppTheme.AccentSoft, Thickness);
            Fill = ArcPen(AppTheme.Accent, Thickness);
        }
        internal static Pen ArcPen(Color color, float width) => new(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        public void Dispose()
        {
            Track.Dispose(); Fill.Dispose();
            ValueFont.Dispose(); LabelFont.Dispose(); Format.Dispose();
            Ink.Dispose(); Muted.Dispose();
        }
    }
}
