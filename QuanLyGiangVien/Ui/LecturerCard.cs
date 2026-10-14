using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Ui;

public sealed class LecturerCard : Control
{
    public const int CardHeight = 220;
    public Lecturer Lecturer { get; }
    public event Action<Lecturer>? Selected;
    public event Action<Lecturer>? EditRequested;
    public event Action<Lecturer, Control>? MenuRequested;
    private readonly Motion motion = new();
    private float hover;
    private bool selected;
    private Bitmap? contentCache;
    private float cacheScale;
    private float cacheDpiX, cacheDpiY;
    private float hoverTarget;
    internal int ContentCacheBuilds { get; private set; }
    internal float RenderTop => AppTheme.S(7) + (1 - Entrance) * AppTheme.S(9) - hover * AppTheme.S(3);
    internal float HoverProgress => hover;
    internal float CachedContentDpiX => contentCache?.HorizontalResolution ?? 0;
    internal void PaintForCheck(Graphics graphics) => PaintCard(graphics);
    internal void SetHoverProgressForCheck(float progress) { hover = Math.Clamp(progress, 0, 1); Invalidate(); }
    internal void AnimateHoverForCheck(bool value) => AnimateHover(value ? 1 : 0);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float Entrance { get; set; } = 1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsSelected { get => selected; set { selected = value; Invalidate(); } }

    public LecturerCard(Lecturer lecturer)
    {
        Lecturer = lecturer;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = $"{lecturer.Name}, {lecturer.Id}, {lecturer.Faculty}, {lecturer.Status}. Nhấn Enter để xem hồ sơ.";
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        BackColor = Color.White;
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); AnimateHover(1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); AnimateHover(0); }
    private void AnimateHover(float target)
    {
        if (hoverTarget == target && motion.IsRunning) return;
        hoverTarget = target;
        float start = hover;
        motion.Run(180, p => { hover = start + (target - start) * p; Invalidate(); });
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { Selected?.Invoke(Lecturer); e.Handled = true; }
        if (e.KeyCode == Keys.F2) { EditRequested?.Invoke(Lecturer); e.Handled = true; }
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
        if (e.Button == MouseButtons.Right || (e.X > Width - AppTheme.S(50) && e.Y < AppTheme.S(52)))
            MenuRequested?.Invoke(Lecturer, this);
        else Selected?.Invoke(Lecturer);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && e.Y > AppTheme.S(52)) EditRequested?.Invoke(Lecturer);
    }
    protected override void OnPaint(PaintEventArgs e) => PaintCard(e.Graphics);
    private void PaintCard(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(BackColor);
        float top = RenderTop;
        var bounds = new RectangleF(AppTheme.S(2), top, Width - AppTheme.S(4), Height - top - AppTheme.S(2));
        using var outline = AppTheme.Round(bounds, AppTheme.S(16));
        using var white = new SolidBrush(Color.White);
        using var border = new Pen(selected || Focused ? AppTheme.Primary : Blend(AppTheme.Line, AppTheme.HoverLine, hover), AppTheme.S(1));
        g.FillPath(white, outline);
        EnsureContentCache(g.DpiX, g.DpiY);
        if (contentCache != null) g.DrawImage(contentCache, new RectangleF(0, top, contentCache.Width, contentCache.Height),
            new RectangleF(0, 0, contentCache.Width, contentCache.Height), GraphicsUnit.Pixel);
        g.DrawPath(border, outline);
    }
    private void EnsureContentCache(float dpiX, float dpiY)
    {
        if (Width <= 0 || Height <= 0) return;
        if (contentCache != null && Math.Abs(cacheScale - AppTheme.K) < .001f && cacheDpiX == dpiX && cacheDpiY == dpiY) return;
        DropContentCache();
        contentCache = new Bitmap(Width, Math.Max(1, Height - AppTheme.S(7)), PixelFormat.Format32bppRgb);
        contentCache.SetResolution(dpiX, dpiY);
        using var graphics = Graphics.FromImage(contentCache);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        DrawContents(graphics);
        cacheScale = AppTheme.K; cacheDpiX = dpiX; cacheDpiY = dpiY; ContentCacheBuilds++;
    }
    private void DropContentCache() { contentCache?.Dispose(); contentCache = null; }
    private static Color Blend(Color a, Color b, float p) => Color.FromArgb(
        (int)Math.Round(a.R + (b.R - a.R) * p), (int)Math.Round(a.G + (b.G - a.G) * p), (int)Math.Round(a.B + (b.B - a.B) * p));
    protected override void OnResize(EventArgs e) { base.OnResize(e); DropContentCache(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); DropContentCache(); }
    private void DrawContents(Graphics g)
    {
        int pad = AppTheme.S(16), contentWidth = Width - pad * 2;
        DrawLabel(g, Lecturer.Id, new(pad, AppTheme.S(10), contentWidth - AppTheme.S(25), AppTheme.S(18)), 8, true, AppTheme.Muted);
        using var menuDot = new SolidBrush(AppTheme.Ink);
        for (int i = 0; i < 3; i++)
            g.FillEllipse(menuDot, Width - AppTheme.S(34 - i * 5), AppTheme.S(16), AppTheme.S(3), AppTheme.S(3));
        DrawPortrait(g);
        DrawLabel(g, Lecturer.Name, new(pad, AppTheme.S(95), contentWidth, AppTheme.S(23)), 10.75f, true);
        DrawLabel(g, Lecturer.Faculty, new(pad, AppTheme.S(119), contentWidth, AppTheme.S(18)), 8.5f, false, AppTheme.Muted);
        DrawLabel(g, Lecturer.Email, new(pad, AppTheme.S(138), contentWidth, AppTheme.S(18)), 8, false, AppTheme.Muted);
        var ribbon = new RectangleF(AppTheme.S(12), AppTheme.S(158), Width - AppTheme.S(24), AppTheme.S(28));
        using var ribbonPath = AppTheme.Round(ribbon, AppTheme.S(9));
        using var tone = new SolidBrush(AppTheme.Tone(Lecturer.Faculty));
        g.FillPath(tone, ribbonPath);
        DrawLabel(g, Lecturer.Degree, new(AppTheme.S(22), AppTheme.S(165), Width / 2 - AppTheme.S(24), AppTheme.S(17)), 8, true);
        DrawLabel(g, $"{Lecturer.ActiveClasses} lớp phụ trách", new(Width / 2, AppTheme.S(165), Width / 2 - AppTheme.S(21), AppTheme.S(17)), 8, false);
        bool active = Lecturer.Status == "Đang giảng dạy";
        using var dot = new SolidBrush(active ? AppTheme.Green : AppTheme.Muted);
        g.FillEllipse(dot, AppTheme.S(17), AppTheme.S(198), AppTheme.S(5), AppTheme.S(5));
        DrawLabel(g, active ? "Đang giảng dạy" : "Tạm nghỉ", new(AppTheme.S(28), AppTheme.S(191), Width - AppTheme.S(76), AppTheme.S(18)), 8, false, active ? AppTheme.Green : AppTheme.Muted);
        DrawLabel(g, "↗", new(Width - AppTheme.S(35), AppTheme.S(187), AppTheme.S(22), AppTheme.S(24)), 13);
    }

    private void DrawPortrait(Graphics graphics)
    {
        int diameter = AppTheme.S(64);
        var bounds = new Rectangle((Width - diameter) / 2, AppTheme.S(29), diameter, diameter);
        var image = Portraits.Get(Lecturer.AvatarKey);
        int cropSize = Math.Min(image.Width, image.Height);
        var crop = new Rectangle((image.Width - cropSize) / 2, (image.Height - cropSize) / 2, cropSize, cropSize);
        using var circle = new GraphicsPath();
        circle.AddEllipse(bounds);
        using var tone = new SolidBrush(AppTheme.Tone(Lecturer.Faculty));
        graphics.FillEllipse(tone, bounds);
        var saved = graphics.Save();
        try
        {
            graphics.SetClip(circle, CombineMode.Intersect);
            graphics.DrawImage(image, bounds, crop, GraphicsUnit.Pixel);
        }
        finally { graphics.Restore(saved); }
    }

    private static void DrawLabel(Graphics g, string text, Rectangle bounds, float size, bool bold = false, Color? color = null)
    {
        using var font = AppTheme.Font(size, bold);
        using var brush = new SolidBrush(color ?? AppTheme.Ink);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, font, brush, bounds, format);
    }
    protected override void Dispose(bool disposing) { if (disposing) { motion.Dispose(); DropContentCache(); } base.Dispose(disposing); }
}
