using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Native keyboard activation with borrowed artwork and fixed timeline geometry.</summary>
internal sealed class WeeklySessionButton : Button
{
    private readonly Motion hover = new();
    private readonly ToolTip tooltip = new() { InitialDelay = 300, AutoPopDelay = 15000 };
    private readonly Font titleFont = AppTheme.Font(9, true), metadataFont = AppTheme.Font(8);
    private readonly string course, lecturer;
    private readonly Color tone;
    private readonly Bitmap? thumbnail;
    private float tint;
    internal ScheduledOccurrence Occurrence { get; }
    internal IReadOnlyList<string> Conflicts { get; }
    internal bool HasThumbnail => thumbnail != null && Height >= AppTheme.S(100);
    internal bool ThumbnailFitsForCheck => !HasThumbnail || Width >= AppTheme.S(90);
    internal bool ContentFitsForCheck => Width >= AppTheme.S(130) && Height >= AppTheme.S(38)
        && !string.IsNullOrWhiteSpace(AccessibleName);

    internal WeeklySessionButton(ScheduledOccurrence occurrence, string course, string lecturer,
        IReadOnlyList<string> conflicts, Color tone, Bitmap? thumbnail)
    {
        Occurrence = occurrence; this.course = course; this.lecturer = lecturer;
        Conflicts = conflicts; this.tone = tone; this.thumbnail = thumbnail;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = metadataFont; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Color.White; Cursor = Cursors.Hand; UseVisualStyleBackColor = false; UseMnemonic = false;
        Text = $"{course}\n{occurrence.Class.Id}\n{occurrence.RoomId} · Tiết {occurrence.Meeting.StartPeriod}–{occurrence.Meeting.EndPeriod}";
        AccessibleName = $"{course}, {occurrence.Class.Id}, {occurrence.Date:dd/MM/yyyy}, tiết "
            + $"{occurrence.Meeting.StartPeriod} đến {occurrence.Meeting.EndPeriod}, phòng {occurrence.RoomId}, {lecturer}";
        AccessibleDescription = conflicts.Count == 0 ? "Bấm hoặc Enter/Space để mở lớp học phần."
            : string.Join(Environment.NewLine, conflicts);
        tooltip.SetToolTip(this, AccessibleName + Environment.NewLine + AccessibleDescription);
    }

    private void Animate(float target)
    {
        float start = tint;
        if (!Enabled || !Visible) { hover.Cancel(false); tint = 0; return; }
        hover.Run(160, p => { tint = start + (target - start) * p; Invalidate(); });
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Animate(1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Animate(Focused ? 1 : 0); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Animate(1); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Animate(0); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) { hover.Cancel(false); tint = 0; } Invalidate(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (!Visible) { hover.Cancel(false); tint = 0; } }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        var g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        bool conflict = Conflicts.Count > 0;
        using var shape = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), AppTheme.S(10));
        using var fill = new SolidBrush(Blend(Color.White, conflict ? AppTheme.AccentSoft : tone, .75f + tint * .2f));
        using var border = new Pen(conflict ? SchedulingTheme.Danger : SchedulingTheme.Line, conflict ? 1.5f : 1);
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        int s(int n) => AppTheme.S(n);
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        int pad = s(9), width = Math.Max(1, Width - pad * 2);
        var ink = Enabled ? SchedulingTheme.Ink : SchedulingTheme.Muted;
        if (Height < s(100))
        {
            TextRenderer.DrawText(g, (conflict ? "! " : "") + course, titleFont,
                new Rectangle(pad, s(5), width, s(17)), ink, flags | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, $"{Occurrence.RoomId} · Tiết {Occurrence.Meeting.StartPeriod}–{Occurrence.Meeting.EndPeriod}", metadataFont,
                new Rectangle(pad, s(23), width, s(15)), SchedulingTheme.Muted, flags | TextFormatFlags.SingleLine);
            if (Height >= s(70)) TextRenderer.DrawText(g, lecturer, metadataFont,
                new Rectangle(pad, s(45), width, s(17)), SchedulingTheme.Muted, flags | TextFormatFlags.SingleLine);
        }
        else
        {
            TextRenderer.DrawText(g, conflict ? "! Xung đột" : Occurrence.Class.Id, metadataFont,
                new Rectangle(pad, pad, width - (HasThumbnail ? s(27) : 0), s(20)),
                conflict ? SchedulingTheme.Danger : SchedulingTheme.Muted, flags | TextFormatFlags.SingleLine);
            if (HasThumbnail) DrawThumbnail(g, new(Width - pad - s(22), pad, s(22), s(22)));
            int bottom = Height - s(8), metadataTop = bottom - s(32);
            TextRenderer.DrawText(g, course, titleFont,
                new Rectangle(pad, s(33), width, Math.Max(s(17), metadataTop - s(36))), ink, flags | TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, lecturer, metadataFont,
                new Rectangle(pad, metadataTop, width, s(15)), SchedulingTheme.Muted, flags | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, $"{Occurrence.RoomId} · Tiết {Occurrence.Meeting.StartPeriod}–{Occurrence.Meeting.EndPeriod}", metadataFont,
                new Rectangle(pad, bottom - s(16), width, s(16)), SchedulingTheme.Muted, flags | TextFormatFlags.SingleLine);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -s(4), -s(4)), ink, BackColor);
    }

    private void DrawThumbnail(Graphics g, Rectangle bounds)
    {
        if (thumbnail == null) return;
        float scale = Math.Min(bounds.Width / (float)thumbnail.Width, bounds.Height / (float)thumbnail.Height);
        var target = new RectangleF(bounds.X + (bounds.Width - thumbnail.Width * scale) / 2,
            bounds.Y + (bounds.Height - thumbnail.Height * scale) / 2, thumbnail.Width * scale, thumbnail.Height * scale);
        var state = g.Save(); g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(thumbnail, target); g.Restore(state);
    }
    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    protected override void Dispose(bool disposing)
    {
        if (disposing) { hover.Dispose(); tooltip.Dispose(); titleFont.Dispose(); metadataFont.Dispose(); }
        base.Dispose(disposing);
    }
}
