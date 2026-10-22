using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Original-resolution CC0 artwork, sampled once with room for the complete hover envelope.</summary>
internal sealed class SchedulingServiceArtwork : Control
{
    private readonly Motion motion = new();
    private readonly ImageAttributes sampling = new();
    private readonly PointF[] destination = new PointF[3];
    private Bitmap? source, raster;
    private SizeF drawSize;
    private Rectangle alphaBounds;
    private Rectangle[] exclusions = [];
    private Size cacheSize;
    private float cacheScale = -1, hover;
    private int cacheDpi = -1;
    private bool hovered;
    internal bool ReadyForCheck => source is not null && !alphaBounds.IsEmpty;
    internal int CacheBuildsForCheck { get; private set; }
    internal IReadOnlyList<Bitmap> OwnedBitmapsForCheck => Array.AsReadOnly(new[] { source, raster }.OfType<Bitmap>().ToArray());
    internal Rectangle AlphaBoundsForCheck => alphaBounds;
    internal (float Progress, float Scale, float Rotation, float OffsetX, bool Running) StateForCheck =>
        (hover, 1 + .1f * hover, 3 * hover, AppTheme.S(10) * hover, motion.IsRunning);
    internal object VisualStateForCheck() => new { Ready = ReadyForCheck, SourceAlpha = alphaBounds,
        Raster = raster?.Size, DrawSize = drawSize, Bounds, State = StateForCheck, CacheBuilds = CacheBuildsForCheck };

    internal SchedulingServiceArtwork(string key)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; TabStop = false;
        sampling.SetWrapMode(WrapMode.TileFlipXY);
        AccessibleRole = AccessibleRole.Graphic; AccessibleName = "Minh họa dịch vụ " + key;
        string name = key switch { "book-open" => "courses", "door-open" => "rooms", "calendar-days" => "schedule", "users-round" => "enrollment", _ => throw new ArgumentException("Mã minh họa không hợp lệ.", nameof(key)) };
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling", "service-cards", name + ".png");
        try {
            using var original = new Bitmap(path);
            // The PNG masters use 29 DPI. DrawImageUnscaled would copy their physical
            // size and miss this pixel canvas; Clone preserves the actual alpha pixels.
            source = original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format32bppPArgb);
            alphaBounds = FindAlphaBounds(source);
            if (alphaBounds.IsEmpty) throw new InvalidDataException("Minh họa không có pixel hiển thị.");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            source?.Dispose(); source = null;
            System.Diagnostics.Trace.TraceWarning("Scheduling service artwork {0}: {1}", name, error.Message);
            AccessibleDescription = "Không tải được minh họa cục bộ.";
        }
    }

    internal void SetExclusions(IEnumerable<Rectangle> rectangles) => exclusions = rectangles.Select(rectangle => {
        rectangle.Inflate(AppTheme.S(3), AppTheme.S(3)); rectangle.Offset(-Left, -Top); return rectangle;
    }).ToArray();

    internal void AnimateHover(bool entered)
    {
        if (IsDisposed || Disposing) return;
        if (!Visible || !AntdUI.Config.Animation) { motion.Cancel(false); hover = 0; hovered = false; Invalidate(); return; }
        if (hovered == entered) return;
        hovered = entered; float from = hover, target = entered ? 1 : 0;
        motion.Run(400, progress => { hover = from + (target - from) * progress; Invalidate(); },
            easing: progress => (1 - MathF.Cos(progress * MathF.PI)) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2 || source is null) return;
        if (!AntdUI.Config.Animation && (hover != 0 || motion.IsRunning)) { motion.Cancel(false); hover = 0; hovered = false; }
        EnsureRaster();
        if (raster is null) return;
        var graphics = e.Graphics; var state = graphics.Save();
        if (Parent is { Width: > 2, Height: > 2 } parent) {
            using var cardClip = AppTheme.Round(new RectangleF(1 - Left, 1 - Top, parent.Width - 2, parent.Height - 2), AppTheme.S(16));
            graphics.SetClip(cardClip, CombineMode.Intersect);
        }
        foreach (var exclusion in exclusions) graphics.ExcludeClip(exclusion);
        var animation = StateForCheck;
        graphics.TranslateTransform(Width / 2f - AppTheme.S(10) / 2f + animation.OffsetX, Height / 2f);
        graphics.RotateTransform(animation.Rotation); graphics.ScaleTransform(animation.Scale, animation.Scale);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        destination[0] = new(-drawSize.Width / 2, -drawSize.Height / 2);
        destination[1] = new(drawSize.Width / 2, -drawSize.Height / 2);
        destination[2] = new(-drawSize.Width / 2, drawSize.Height / 2);
        graphics.DrawImage(raster, destination, new RectangleF(0, 0, raster.Width, raster.Height), GraphicsUnit.Pixel, sampling);
        graphics.Restore(state);
    }

    private void EnsureRaster()
    {
        if (source is null || Width < 1 || Height < 1) return;
        if (raster is not null && cacheSize == Size && cacheScale == AppTheme.K && cacheDpi == DeviceDpi) return;
        raster?.Dispose();
        raster = source.Clone(alphaBounds, PixelFormat.Format32bppPArgb);
        float sine = MathF.Sin(3 * MathF.PI / 180);
        float rotatedWidth = alphaBounds.Width + alphaBounds.Height * sine;
        float rotatedHeight = alphaBounds.Height + alphaBounds.Width * sine;
        int gutter = AppTheme.S(8);
        float factor = Math.Min(Math.Max(1, Width - gutter - AppTheme.S(10)) / (1.1f * rotatedWidth),
            Math.Max(1, Height - gutter) / (1.1f * rotatedHeight));
        drawSize = new(alphaBounds.Width * factor, alphaBounds.Height * factor);
        cacheSize = Size; cacheScale = AppTheme.K; cacheDpi = DeviceDpi; CacheBuildsForCheck++;
    }

    private static Rectangle FindAlphaBounds(Bitmap bitmap)
    {
        var pixels = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try {
            var row = new byte[bitmap.Width * 4]; int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
            for (int y = 0; y < bitmap.Height; y++) {
                Marshal.Copy(IntPtr.Add(pixels.Scan0, y * pixels.Stride), row, 0, row.Length);
                for (int x = 0; x < bitmap.Width; x++) {
                    if (row[x * 4 + 3] == 0) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            }
            return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally { bitmap.UnlockBits(pixels); }
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); raster?.Dispose(); raster = null; }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); raster?.Dispose(); raster = null; }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { motion.Cancel(false); hover = 0; hovered = false; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { motion.Dispose(); raster?.Dispose(); raster = null; source?.Dispose(); source = null; sampling.Dispose(); }
        base.Dispose(disposing);
    }
}
