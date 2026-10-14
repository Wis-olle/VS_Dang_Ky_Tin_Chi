using System.Drawing.Drawing2D;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>WinForms service card adaptation of the user's supplied React reference.</summary>
internal sealed class SchedulingStat : Control
{
    private readonly Motion countMotion = new(), hoverMotion = new(), ratioMotion = new();
    private readonly SchedulingServiceArtwork artworkControl;
    private readonly ServiceActionButton actionControl;
    private Font captionFont = AppTheme.Font(10.5f, true), numberFont = AppTheme.Font(30, true);
    private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    private Rectangle captionBounds, numberBounds;
    private Size layoutSize;
    private float layoutScale = -1, layoutDpi = -1;
    private int layoutValue, numberReference;
    private bool layoutPercent;
    private readonly string caption, ratioMeaning;
    private readonly Color background, foreground;
    private readonly bool primary;
    private string note = "";
    private int value;
    private float displayed, hover, ratio, displayedRatio;
    private bool percent, hovered;
    private Action? action;
    internal SchedulingServiceArtwork ArtworkForCheck => artworkControl;
    internal System.Windows.Forms.Button ActionForCheck => actionControl;
    internal bool MotionRunningForCheck => countMotion.IsRunning || hoverMotion.IsRunning || ratioMotion.IsRunning
        || artworkControl.StateForCheck.Running || actionControl.MotionRunning;
    internal float RatioForCheck => ratio;
    internal (float Target, float Displayed, bool Running) RatioStateForCheck => (ratio, displayedRatio, ratioMotion.IsRunning);
    internal IReadOnlyList<Font> OwnedFontsForCheck => Array.AsReadOnly(new[] { captionFont, numberFont, actionControl.Font });
    internal (float TextRight, float ArrowLeft, float ArrowRight) ActionGlyphsForCheck => actionControl.GlyphsForCheck;
    internal IReadOnlyList<float> SegmentsForCheck => Array.Empty<float>();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool ShowSeparator { get; set; }

    internal bool ContentFitsForCheck
    {
        get {
            using var graphics = CreateGraphics(); EnsureTextLayout(graphics);
            bool InCard(Rectangle bounds) => bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= Width && bounds.Bottom <= Height;
            var captionSize = TextRenderer.MeasureText(graphics, caption, captionFont, Size.Empty, TextFlags);
            var numberSize = TextRenderer.MeasureText(graphics, ValueText(), numberFont, Size.Empty, TextFlags);
            return InCard(captionBounds) && InCard(numberBounds)
                && captionSize.Width <= captionBounds.Width && captionSize.Height <= captionBounds.Height
                && numberSize.Width <= numberBounds.Width && numberSize.Height <= numberBounds.Height
                && captionBounds.Bottom <= numberBounds.Top && numberBounds.Bottom <= actionControl.Top
                && (!actionControl.Visible || (InCard(actionControl.Bounds) && actionControl.TextFitsForCheck));
        }
    }
    internal bool NumberTextFitsForCheck
    {
        get {
            using var graphics = CreateGraphics(); EnsureTextLayout(graphics);
            var measured = TextRenderer.MeasureText(graphics, ValueText(), numberFont, Size.Empty, TextFlags);
            return measured.Height <= numberBounds.Height && measured.Width <= numberBounds.Width
                && captionBounds.Bottom <= numberBounds.Top && numberBounds.Bottom <= actionControl.Top;
        }
    }
    internal object VisualStateForCheck()
    {
        using var graphics = CreateGraphics(); EnsureTextLayout(graphics);
        return new { Caption = captionBounds, Number = numberBounds, Note = Rectangle.Empty, Action = actionControl.Bounds,
            Rail = RectangleF.Empty, Ring = RectangleF.Empty, Matrix = RectangleF.Empty, RatioLabel = Rectangle.Empty, FooterBand = RectangleF.Empty,
            Ratio = ratio, DisplayedRatio = displayedRatio, NumberFontSize = numberFont.Size, RatioIsAnimating = ratioMotion.IsRunning,
            Primary = primary, Artwork = artworkControl.VisualStateForCheck(), Background = background.ToArgb() };
    }

    internal SchedulingStat(string caption, string icon, Color tone)
    {
        this.caption = caption; primary = icon == "book-open";
        background = icon switch { "book-open" => AppTheme.Primary, "door-open" => AppTheme.Cyan, "calendar-days" => AppTheme.Lilac, _ => AppTheme.AccentSoft };
        foreground = primary ? Color.White : AppTheme.Ink;
        ratioMeaning = icon switch {
            "book-open" => "Tỷ lệ lớp đang mở trong tổng lớp", "door-open" => "Tỷ lệ phòng sẵn sàng trong tổng phòng",
            "calendar-days" => "Tỷ lệ lớp đã xếp lịch trong các lớp chưa hủy", _ => "Tỷ lệ chỗ đã đăng ký trong tổng sức chứa"
        };
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; AccessibleName = caption; TabStop = false;
        artworkControl = new SchedulingServiceArtwork(icon);
        actionControl = new ServiceActionButton(background, foreground) { Font = AppTheme.Font(8.5f, true), Visible = false };
        actionControl.Click += (_, _) => ActivateAction();
        artworkControl.Click += (_, _) => OnClick(EventArgs.Empty);
        foreach (var control in new Control[] { artworkControl, actionControl }) {
            control.MouseEnter += (_, _) => AnimateHover(true);
            control.MouseLeave += (_, _) => { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) AnimateHover(false); };
        }
        actionControl.GotFocus += (_, _) => AnimateHover(true);
        actionControl.LostFocus += (_, _) => { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) AnimateHover(false); };
        Controls.AddRange([artworkControl, actionControl]);
    }

    internal void SetAction(string label, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        action = string.IsNullOrWhiteSpace(label) ? null : callback;
        actionControl.Text = label; actionControl.Visible = action is not null;
        actionControl.AccessibleName = action is null ? "" : label + ", " + caption;
        actionControl.AccessibleDescription = "Mở nội dung " + caption + " trong trang Lớp học và Phòng học.";
        Cursor = action is null ? Cursors.Default : Cursors.Hand;
        layoutSize = Size.Empty; Invalidate();
    }
    private void ActivateAction()
    {
        if (!IsDisposed && !Disposing && Enabled && Visible) action?.Invoke();
    }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ActivateAction();
    }
    internal void SetValue(int number, string description, bool isPercent = false, bool animate = true)
    {
        numberReference = Math.Max(Math.Abs(number), (int)Math.Round(Math.Abs(displayed)));
        value = number; note = description; percent = isPercent; UpdateAccessibleDescription();
        float from = displayed;
        if (!animate || !Visible || Math.Abs(from - number) < .00001f)
        { countMotion.Cancel(false); displayed = number; numberReference = Math.Abs(number); Invalidate(); return; }
        countMotion.Run(650, progress => { displayed = from + (value - from) * progress; Invalidate(); });
    }
    internal void SetRatio(float fraction, bool animate = true)
    {
        ratio = float.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0; UpdateAccessibleDescription();
        float from = displayedRatio, target = ratio;
        if (!animate || !Visible || Math.Abs(target - from) < .00001f)
        { ratioMotion.Cancel(false); displayedRatio = target; Invalidate(); return; }
        ratioMotion.Run(650, progress => { displayedRatio = from + (target - from) * progress; Invalidate(); });
    }
    private void UpdateAccessibleDescription() => AccessibleDescription = $"{value}{(percent ? "%" : "")}; {note}; {ratioMeaning}: {Math.Round(ratio * 100)}%.";
    internal void AnimateHover(bool entered)
    {
        if (IsDisposed || Disposing) return;
        if (!Visible || !AntdUI.Config.Animation) { ResetHover(); return; }
        if (hovered == entered) return;
        hovered = entered;
        artworkControl.AnimateHover(entered); actionControl.AnimateHover(entered);
        float from = hover;
        hoverMotion.Run(300, progress => { hover = from + ((entered ? 1 : 0) - from) * progress; Invalidate(); });
    }
    internal void RevealValue()
    {
        displayed = 0; ratioMotion.Cancel(false); displayedRatio = 0;
        SetValue(value, note, percent); SetRatio(ratio);
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); AnimateHover(true); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) AnimateHover(false); }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e); layoutSize = Size.Empty;
        if (artworkControl is not null) ArrangeArtwork();
    }
    private void ArrangeArtwork()
    {
        int size = AppTheme.S(Width >= AppTheme.S(290) ? 140 : 112);
        artworkControl.SetBounds(Width - size - AppTheme.S(10), Height - size - AppTheme.S(10), size, size);
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { countMotion.Cancel(); ratioMotion.Cancel(); ResetHover(); }
    }
    private void ResetHover()
    {
        hoverMotion.Cancel(false); hover = 0; hovered = false;
        if (artworkControl is not null) artworkControl.AnimateHover(false);
        if (actionControl is not null) actionControl.ResetMotion();
    }

    private void EnsureTextLayout(Graphics graphics)
    {
        bool dpiChanged = layoutScale != AppTheme.K || layoutDpi != graphics.DpiX;
        if (!dpiChanged && layoutSize == Size && layoutValue == numberReference && layoutPercent == percent) return;
        if (dpiChanged) {
            captionFont.Dispose(); actionControl.Font.Dispose();
            captionFont = AppTheme.Font(10.5f, true); actionControl.Font = AppTheme.Font(8.5f, true);
        }
        layoutScale = AppTheme.K; layoutDpi = graphics.DpiX; layoutSize = Size; layoutValue = numberReference; layoutPercent = percent;
        ArrangeArtwork(); int pad = AppTheme.S(18);
        captionBounds = new(pad, AppTheme.S(14), Math.Max(1, Width - pad * 2), AppTheme.S(30));
        FitFont(ref captionFont, graphics, caption, captionBounds, 10.5f, true, TextFlags);
        captionBounds.Height = TextRenderer.MeasureText(graphics, caption, captionFont, Size.Empty, TextFlags).Height;
        int bodyWidth = Math.Max(1, Math.Min(AppTheme.S(126), artworkControl.Left - pad - AppTheme.S(6)));
        numberBounds = new(pad, AppTheme.S(52), Math.Min(AppTheme.S(114), bodyWidth), AppTheme.S(58));
        string numberText = numberReference + (percent ? "%" : "");
        FitFont(ref numberFont, graphics, numberText, numberBounds, 30, true, TextFlags);
        actionControl.SetBounds(pad, AppTheme.S(150), bodyWidth, AppTheme.S(24));
        artworkControl.SetExclusions([captionBounds, numberBounds, actionControl.Bounds]);
    }
    private static void FitFont(ref Font font, Graphics graphics, string text, Rectangle bounds, float logicalSize, bool bold, TextFormatFlags flags)
    {
        font.Dispose(); font = AppTheme.Font(logicalSize, bold);
        for (int attempt = 0; attempt < 16; attempt++) {
            var size = TextRenderer.MeasureText(graphics, string.IsNullOrEmpty(text) ? "Ag" : text, font,
                flags.HasFlag(TextFormatFlags.WordBreak) ? new Size(bounds.Width, int.MaxValue) : Size.Empty, flags);
            if (size.Height <= bounds.Height && size.Width <= bounds.Width) return;
            float ratio = Math.Min(bounds.Height / (float)Math.Max(1, size.Height), bounds.Width / (float)Math.Max(1, size.Width));
            logicalSize = Math.Max(.5f, logicalSize * Math.Min(.95f, ratio)); font.Dispose(); font = AppTheme.Font(logicalSize, bold);
        }
    }
    private string ValueText() => Math.Round(displayed).ToString() + (percent ? "%" : "");
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if (Width < 2 || Height < 2) return;
        if (!AntdUI.Config.Animation && MotionRunningForCheck) { countMotion.Cancel(); ratioMotion.Cancel(); ResetHover(); }
        var graphics = e.Graphics; graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppTheme.Round(new RectangleF(1, 1, Width - 2, Height - 2), AppTheme.S(16));
        using var fill = new SolidBrush(background); graphics.FillPath(fill, path);
        if (hover > 0) { using var border = new Pen(Color.FromArgb((int)(hover * 65), foreground), Math.Max(1, AppTheme.S(1))); graphics.DrawPath(border, path); }
        EnsureTextLayout(graphics);
        TextRenderer.DrawText(graphics, caption, captionFont, captionBounds, foreground, TextFlags);
        TextRenderer.DrawText(graphics, ValueText(), numberFont, numberBounds, foreground, TextFlags | TextFormatFlags.VerticalCenter);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { action = null; countMotion.Dispose(); hoverMotion.Dispose(); ratioMotion.Dispose(); captionFont.Dispose(); numberFont.Dispose(); actionControl.Font.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class ServiceActionButton : System.Windows.Forms.Button
    {
        private readonly Motion arrowMotion = new();
        private readonly Color background, foreground;
        private float arrow;
        private bool hovered;
        internal (float TextRight, float ArrowLeft, float ArrowRight) GlyphsForCheck {
            get { using var graphics = CreateGraphics(); return MeasureGlyphs(graphics); }
        }
        private (float TextRight, float ArrowLeft, float ArrowRight) MeasureGlyphs(Graphics graphics)
        {
            float textRight = TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, TextFlags).Width;
            float left = textRight + AppTheme.S(6) + AppTheme.S(5) * arrow;
            return (textRight, left, left + AppTheme.S(8));
        }
        internal bool MotionRunning => arrowMotion.IsRunning;
        internal bool TextFitsForCheck {
            get { using var graphics = CreateGraphics(); var measured = TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, TextFlags);
                return measured.Width + AppTheme.S(22) <= Width && measured.Height <= Height; }
        }
        internal ServiceActionButton(Color background, Color foreground)
        {
            this.background = background; this.foreground = foreground;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = background; ForeColor = foreground; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false; Cursor = Cursors.Hand; TextAlign = ContentAlignment.MiddleLeft; TabStop = true;
        }
        internal void AnimateHover(bool entered)
        {
            if (IsDisposed || Disposing) return;
            if (!Visible || !AntdUI.Config.Animation) { ResetMotion(); return; }
            if (hovered == entered) return;
            hovered = entered; float from = arrow, target = entered ? 1 : 0;
            arrowMotion.Run(300, progress => { arrow = from + (target - from) * progress; Invalidate(); });
        }
        internal void ResetMotion() { arrowMotion.Cancel(false); arrow = 0; hovered = false; if (!IsDisposed) Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            e.Graphics.Clear(background);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Math.Max(1, Width - AppTheme.S(22)), Height), foreground,
                TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            float x = MeasureGlyphs(e.Graphics).ArrowRight, y = Height / 2f + AppTheme.S(2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(foreground, Math.Max(1, AppTheme.S(1))) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(pen, x - AppTheme.S(8), y, x, y);
            e.Graphics.DrawLines(pen, [new PointF(x - AppTheme.S(4), y - AppTheme.S(4)), new PointF(x, y), new PointF(x - AppTheme.S(4), y + AppTheme.S(4))]);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(1, 1, Width - 2, Height - 2), foreground, background);
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (!Visible) ResetMotion(); }
        protected override void Dispose(bool disposing) { if (disposing) arrowMotion.Dispose(); base.Dispose(disposing); }
    }
}
