using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace QuanLyGiangVien.Ui;

/// <summary>Opaque fixed HWND; only a cached raster's floating-point destination moves.</summary>
internal sealed class FloatingArtwork : Control
{
    private readonly Bitmap? source;
    private Bitmap? raster;
    private LinearGradientBrush? backdrop;
    private Color start, end;
    private int backdropWidth, backdropLeft;
    private float cacheScale;
    internal float Offset { get; private set; }
    internal int CacheBuilds { get; private set; }
    internal Size RasterSize => raster?.Size ?? Size.Empty;
    internal FloatingArtwork(Bitmap? artwork)
    {
        source = artwork;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        TabStop = false;
        AccessibleName = "Minh họa người làm việc với laptop theo nét vẽ đen trắng";
    }
    internal void SetBackdrop(Color from, Color to, int parentWidth, int slotLeft)
    {
        if (backdrop != null && start == from && end == to && backdropWidth == parentWidth && backdropLeft == slotLeft) return;
        start = from; end = to; backdropWidth = parentWidth; backdropLeft = slotLeft;
        backdrop?.Dispose();
        backdrop = new(new RectangleF(-slotLeft, 0, Math.Max(1, parentWidth), Math.Max(1, Height)), from, to, 0f);
        Invalidate();
    }
    internal void SetOffset(float offset)
    {
        if (Math.Abs(Offset - offset) < .001f) return;
        Offset = offset; Invalidate();
    }
    private void EnsureRaster()
    {
        if (source == null || Width <= 0 || Height <= 0) return;
        if (raster != null && Math.Abs(cacheScale - AppTheme.K) < .001f) return;
        raster?.Dispose();
        float factor = Math.Min(Width / (float)source.Width, Math.Max(1, Height - AppTheme.S(8)) / (float)source.Height);
        raster = new(Math.Max(1, (int)Math.Round(source.Width * factor)), Math.Max(1, (int)Math.Round(source.Height * factor)), PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(raster);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(source, new Rectangle(0, 0, raster.Width, raster.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        cacheScale = AppTheme.K; CacheBuilds++;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (backdrop != null) e.Graphics.FillRectangle(backdrop, ClientRectangle);
        else e.Graphics.Clear(AppTheme.Peach);
        EnsureRaster();
        if (raster == null) return;
        e.Graphics.InterpolationMode = InterpolationMode.Bilinear;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(raster, new RectangleF((Width - raster.Width) / 2f, (Height - raster.Height) / 2f + Offset, raster.Width, raster.Height),
            new RectangleF(0, 0, raster.Width, raster.Height), GraphicsUnit.Pixel);
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        raster?.Dispose(); raster = null; Invalidate();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { raster?.Dispose(); raster = null; backdrop?.Dispose(); backdrop = null; }
        base.Dispose(disposing);
    }
}
