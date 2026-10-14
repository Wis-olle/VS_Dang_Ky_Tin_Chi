using System.Drawing.Drawing2D;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Subject or room-type imagery stays separate from the inspector's interactive fields.</summary>
internal sealed class SchedulingResourceCover : Control
{
    private readonly Font captionFont = AppTheme.Font(9, true), labelFont = AppTheme.Font(7.5f);
    private SchedulingVisual? visual;
    private string label = "";
    internal string? VisualKeyForCheck => visual?.Key;
    internal IReadOnlyList<Font> OwnedFontResourcesForCheck => [captionFont, labelFont];

    internal SchedulingResourceCover()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White; TabStop = false; AccessibleRole = AccessibleRole.Graphic;
    }

    internal void SetVisual(SchedulingVisual? value, bool isRoom)
    {
        visual = value; label = isRoom ? "KHÔNG GIAN HỌC" : "NHÓM HỌC PHẦN";
        AccessibleName = value?.Caption ?? "Minh họa học tập";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (visual == null || Width < 2 || Height < 2) return;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), AppTheme.S(12));
        using var background = new SolidBrush(visual.Tone);
        graphics.FillPath(background, shape);
        int textWidth = Math.Max(1, Width - AppTheme.S(134));
        TextRenderer.DrawText(graphics, label, labelFont, new Rectangle(AppTheme.S(12), AppTheme.S(10), textWidth, AppTheme.S(16)), SchedulingTheme.Muted,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(graphics, visual.Caption, captionFont, new Rectangle(AppTheme.S(12), AppTheme.S(28), textWidth, Height - AppTheme.S(32)), SchedulingTheme.Ink,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
        if (visual.Image is { } image)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            float scale = Math.Min(AppTheme.S(112) / (float)image.Width, (Height - AppTheme.S(12)) / (float)image.Height);
            float width = image.Width * scale, height = image.Height * scale;
            graphics.DrawImage(image, new RectangleF(Width - AppTheme.S(12) - width, (Height - height) / 2, width, height));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { captionFont.Dispose(); labelFont.Dispose(); }
        base.Dispose(disposing);
    }
}
