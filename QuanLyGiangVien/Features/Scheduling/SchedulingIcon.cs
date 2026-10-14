using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Xml.Linq;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed class SchedulingIcon : Control
{
    private readonly string key;
    private readonly Color tone;
    private readonly Motion motion = new();
    private readonly ImageAttributes alphaAttributes = new();
    private readonly ColorMatrix alphaMatrix = new() { Matrix00 = 1, Matrix11 = 1, Matrix22 = 1, Matrix33 = 1, Matrix44 = 1 };
    private Bitmap[] layers = [];
    private int cachedSize = -1, cachedDpi = -1;
    private float cachedScale = -1, progress = 1;
    private bool inside;
    internal bool IsRunning => motion.IsRunning;
    internal float Progress => progress;
    internal int CacheBuildsForCheck { get; private set; }
    internal int RasterLayerCountForCheck => layers.Length;
    internal int DurationMilliseconds => key == "calendar-days" ? 900 : key == "users-round" ? 1100 : 600;

    internal SchedulingIcon(string key, Color tone)
    {
        if (key is not ("book-open" or "door-open" or "calendar-days" or "users-round"))
            throw new ArgumentException("Icon scheduling không hợp lệ.", nameof(key));
        this.key = key; this.tone = tone;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Size = new(AppTheme.S(28), AppTheme.S(28));
        BackColor = Color.Transparent; TabStop = false; AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = key switch {
            "book-open" => "Lớp học phần", "door-open" => "Phòng học",
            "calendar-days" => "Lịch học", _ => "Sĩ số đăng ký"
        };
        AccessibleDescription = "Biểu tượng nét viền; chuyển động hữu hạn theo nguồn Lucide Animated.";
    }

    internal void Play()
    {
        if (IsDisposed || Disposing || motion.IsRunning) return;
        if (!AntdUI.Config.Animation || !CanPlay()) { Stop(); return; }
        motion.Run(DurationMilliseconds, value => {
            if (!CanPlay()) { Stop(); return; }
            progress = value; Invalidate();
        }, easing: value => value);
    }

    internal void Animate(bool entered)
    {
        if (IsDisposed || Disposing) return;
        if (entered && !inside) { inside = true; Play(); }
        else if (!entered) { inside = false; Stop(); }
    }
    internal void ReplayForCheck() => Play();

    private bool CanPlay()
    {
        if (!Visible || !Enabled || !IsHandleCreated || Width < 1 || Height < 1) return false;
        var visible = ClientRectangle;
        for (Control? current = this; current?.Parent is { } parent; current = parent)
        {
            if (!parent.Visible) return false;
            visible.Offset(current.Left, current.Top);
            visible = Rectangle.Intersect(visible, parent.ClientRectangle);
            if (visible.Width < 1 || visible.Height < 1) return false;
        }
        return FindForm() is { Visible: true, IsDisposed: false, WindowState: not FormWindowState.Minimized };
    }

    private void Stop()
    {
        motion.Cancel(false); progress = 1;
        if (!IsDisposed && !Disposing) Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Animate(true); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Animate(false); }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { inside = false; Stop(); }
    }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) Stop(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); ClearCache(); Stop(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); ClearCache(); Stop(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2) return;
        if (!AntdUI.Config.Animation && IsRunning) Stop();
        var graphics = e.Graphics; graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var chip = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), AppTheme.S(8));
        using var background = new SolidBrush(tone); graphics.FillPath(background, chip);
        EnsureCache();
        if (layers.Length == 0) return;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float x = (Width - layers[0].Width) / 2f, y = (Height - layers[0].Height) / 2f;
        if (key is "book-open" or "door-open") DrawBookMotion(graphics);
        else if (key == "calendar-days") DrawCalendar(graphics, x, y);
        else DrawUsers(graphics, x, y);
    }

    private void DrawBookMotion(Graphics graphics)
    {
        // Source book-text whole-SVG keyframes, adapted to each existing static glyph.
        float rotation = Frames(progress, [0, -8, 8, -8, 0], [0, .2f, .5f, .8f, 1]);
        float scale = Frames(progress, [1, 1.04f, 1], [0, .5f, 1]);
        float offset = Frames(progress, [0, -2, 0], [0, .5f, 1]) * layers[0].Width / 24;
        var state = graphics.Save();
        graphics.TranslateTransform(Width / 2f, Height / 2f + offset);
        graphics.RotateTransform(rotation); graphics.ScaleTransform(scale, scale);
        graphics.DrawImage(layers[0], new RectangleF(-layers[0].Width / 2f, -layers[0].Height / 2f, layers[0].Width, layers[0].Height));
        graphics.Restore(state);
    }

    private void DrawCalendar(Graphics graphics, float x, float y)
    {
        graphics.DrawImage(layers[0], new RectangleF(x, y, layers[0].Width, layers[0].Height));
        for (int index = 1; index < layers.Length; index++)
        {
            float elapsed = progress * DurationMilliseconds - (index - 1) * 100;
            float opacity = elapsed is > 0 and < 400 ? Frames(elapsed / 400, [1, .3f, 1], [0, .5f, 1]) : 1;
            DrawLayer(graphics, layers[index], x, y, opacity);
        }
    }

    private void DrawUsers(Graphics graphics, float x, float y)
    {
        graphics.DrawImage(layers[0], new RectangleF(x, y, layers[0].Width, layers[0].Height));
        double seconds = (progress * DurationMilliseconds - 100) / 1000d;
        double displacement = progress >= 1 || seconds < 0 ? 0 : Math.Exp(-6.5 * seconds) *
            (Math.Cos(Math.Sqrt(157.75) * seconds) + 6.5 / Math.Sqrt(157.75) * Math.Sin(Math.Sqrt(157.75) * seconds));
        float opacity = (float)Math.Clamp(1 - displacement, 0, 1);
        DrawLayer(graphics, layers[1], x - (float)displacement * 4 * layers[1].Width / 24, y, opacity);
    }

    private void DrawLayer(Graphics graphics, Bitmap bitmap, float x, float y, float opacity)
    {
        if (opacity <= 0) return;
        alphaMatrix.Matrix33 = opacity; alphaAttributes.SetColorMatrix(alphaMatrix);
        var state = graphics.Save(); graphics.TranslateTransform(x, y);
        graphics.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0,
            bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, alphaAttributes);
        graphics.Restore(state);
    }

    private void EnsureCache()
    {
        int size = Math.Max(1, (int)Math.Round(Math.Min(Width, Height) * 20d / 28));
        if (cachedSize == size && cachedDpi == DeviceDpi && cachedScale == AppTheme.K) return;
        ClearCache(); cachedSize = size; cachedDpi = DeviceDpi; cachedScale = AppTheme.K;
        var created = new List<Bitmap>();
        try
        {
            foreach (var svg in SvgLayers())
                created.Add(AntdUI.SvgExtend.SvgToBmp(svg, size, size, SchedulingTheme.Ink)
                    ?? throw new InvalidDataException("Không render được SVG scheduling."));
            layers = created.ToArray(); CacheBuildsForCheck++;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Xml.XmlException
            or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            foreach (var bitmap in created) bitmap.Dispose();
            AccessibleDescription = "Không tải được tài nguyên icon cục bộ.";
            System.Diagnostics.Trace.TraceWarning("Scheduling icon {0}: {1}", key, error.Message);
        }
    }

    private IEnumerable<string> SvgLayers()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling");
        if (key is "book-open" or "door-open") { yield return Prepare(XElement.Load(Path.Combine(root, key + ".svg"))); yield break; }
        string motionFolder = Path.Combine(root, "motion");
        if (key == "users-round")
        {
            yield return Prepare(XElement.Load(Path.Combine(motionFolder, "users-body.svg")));
            yield return Prepare(XElement.Load(Path.Combine(motionFolder, "users-companion.svg")));
            yield break;
        }
        yield return Prepare(XElement.Load(Path.Combine(motionFolder, "calendar-body.svg")));
        var dots = XElement.Load(Path.Combine(motionFolder, "calendar-dots.svg"));
        foreach (var dot in dots.Elements())
        {
            var svg = new XElement(dots.Name, dots.Attributes().Select(attribute => new XAttribute(attribute)), new XElement(dot));
            yield return Prepare(svg);
        }
    }

    private static string Prepare(XElement svg)
    {
        // Preserve explicit no-fill shapes when AntdUI applies its root fill color.
        var paths = svg.Elements().Select(element => new XElement(element)).ToArray();
        svg.ReplaceNodes(new XElement(svg.Name.Namespace + "g", new XAttribute("fill", "none"), paths));
        Color ink = SchedulingTheme.Ink;
        return svg.ToString().Replace("currentColor", $"#{ink.R:X2}{ink.G:X2}{ink.B:X2}", StringComparison.Ordinal);
    }

    private static float Frames(float elapsed, ReadOnlySpan<float> values, ReadOnlySpan<float> times)
    {
        if (elapsed >= 1) return values[^1];
        for (int index = 1; index < times.Length; index++)
        {
            if (elapsed > times[index]) continue;
            float fraction = Math.Clamp((elapsed - times[index - 1]) / (times[index] - times[index - 1]), 0, 1);
            return values[index - 1] + (values[index] - values[index - 1]) * EaseInOut(fraction);
        }
        return values[^1];
    }

    private static float EaseInOut(float value)
    {
        // CSS/Motion cubic-bezier(.42, 0, .58, 1), evaluated without allocating per frame.
        if (value <= 0 || value >= 1) return Math.Clamp(value, 0, 1);
        float low = 0, high = 1, parameter = value;
        for (int iteration = 0; iteration < 14; iteration++)
        {
            float inverse = 1 - parameter;
            float x = 3 * .42f * inverse * inverse * parameter + 3 * .58f * inverse * parameter * parameter + parameter * parameter * parameter;
            if (x < value) low = parameter; else high = parameter;
            parameter = (low + high) / 2;
        }
        return parameter * parameter * (3 - 2 * parameter);
    }

    private void ClearCache()
    {
        foreach (var bitmap in layers) bitmap.Dispose();
        layers = []; cachedSize = cachedDpi = -1; cachedScale = -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { motion.Dispose(); ClearCache(); alphaAttributes.Dispose(); }
        base.Dispose(disposing);
    }
}
