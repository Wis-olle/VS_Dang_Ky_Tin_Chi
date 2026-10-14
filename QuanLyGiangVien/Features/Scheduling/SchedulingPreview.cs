using System.Drawing.Drawing2D;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Two explicit text rows while retaining native button click and keyboard behavior.</summary>
internal sealed class SchedulingPreview : System.Windows.Forms.Button
{
    private readonly Font titleFont = AppTheme.Font(9, true);
    private readonly Font metadataFont = AppTheme.Font(8);
    private readonly Font dayFont = AppTheme.Font(8, true);
    private Color tone = SchedulingTheme.Cyan;
    private readonly Motion hoverMotion = new();
    private string title = "", metadata = "", day = "", detail = "";
    private bool hovered, pressed;
    private float tint;

    internal SchedulingPreview()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Color.White;
        ForeColor = SchedulingTheme.Ink;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(AppTheme.S(298), AppTheme.S(46));
    }
    internal void SetTone(Color color) { tone = color; Invalidate(); }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        string text = Text.Replace("\r", "", StringComparison.Ordinal);
        int separator = text.IndexOf('\n');
        title = separator < 0 ? text : text[..separator];
        metadata = separator < 0 ? "" : text[(separator + 1)..].Replace('\n', ' ');
        int daySeparator = metadata.IndexOf('·');
        day = daySeparator > 0 && metadata[..daySeparator].Trim().Length <= 12 ? metadata[..daySeparator].Trim() : "";
        detail = day.Length == 0 ? metadata : metadata[(daySeparator + 1)..].Trim();
        AccessibleName = title;
        AccessibleDescription = metadata;
        Invalidate();
    }

    private void AnimateTint()
    {
        float from = tint, target = Enabled && (hovered || Focused) ? 1 : 0;
        hoverMotion.Run(180, p => { tint = from + (target - from) * p; Invalidate(); });
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e); hovered = true; AnimateTint();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e); hovered = false; AnimateTint();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e); AnimateTint();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e); pressed = false; AnimateTint();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); pressed = false; Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) { pressed = false; Invalidate(); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode == Keys.Space) { pressed = false; Invalidate(); }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled) { hoverMotion.Cancel(false); pressed = false; tint = 0; }
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { hoverMotion.Cancel(false); hovered = pressed = false; tint = 0; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        using var canvas = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(canvas, ClientRectangle);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = AppTheme.Round(new RectangleF(0, 0, Width - 1, Height - 1), AppTheme.S(12));
        float amount = pressed && Enabled ? .35f : tint * .18f;
        using var background = new SolidBrush(Blend(Color.White, tone, amount));
        e.Graphics.FillPath(background, shape);
        int pad = AppTheme.S(10), textX = day.Length == 0 ? pad : AppTheme.S(60);
        int width = Math.Max(1, Width - textX - pad);
        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
        if (day.Length > 0) {
            var dayBounds = new Rectangle(pad, AppTheme.S(10), AppTheme.S(36), AppTheme.S(26));
            using var dayShape = AppTheme.Round(dayBounds, AppTheme.S(8));
            using var dayFill = new SolidBrush(Blend(Color.White, tone, .65f));
            e.Graphics.FillPath(dayFill, dayShape);
            TextRenderer.DrawText(e.Graphics, day, dayFont, dayBounds, SchedulingTheme.Muted,
                flags | TextFormatFlags.HorizontalCenter);
        }
        TextRenderer.DrawText(e.Graphics, title, titleFont,
            new Rectangle(textX, AppTheme.S(4), width, AppTheme.S(23)),
            Enabled ? SchedulingTheme.Ink : SchedulingTheme.Muted, flags);
        TextRenderer.DrawText(e.Graphics, detail, metadataFont,
            new Rectangle(textX, AppTheme.S(28), width, AppTheme.S(18)), SchedulingTheme.Muted, flags);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(AppTheme.S(3), AppTheme.S(3),
                Width - AppTheme.S(6), Height - AppTheme.S(6)), SchedulingTheme.Primary, Color.White);
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hoverMotion.Dispose(); titleFont.Dispose(); metadataFont.Dispose(); dayFont.Dispose(); }
        base.Dispose(disposing);
    }
}
