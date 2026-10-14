using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>A locally packaged illustration with a size-aware raster cache.</summary>
internal sealed class SchedulingArtwork : Control
{
    private readonly Bitmap? artwork;
    private Bitmap? raster;
    private Size rasterSize;
    private int rasterDpi;
    internal bool HasArtworkForCheck { get { EnsureRaster(); return raster != null; } }

    internal SchedulingArtwork(string file)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = file.StartsWith("date-", StringComparison.Ordinal) ? "Minh họa lịch học" : "Minh họa sách học tập";
        try
        {
            using var source = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling", "illustrations", file));
            artwork = new Bitmap(source);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            Trace.TraceWarning("Scheduling illustration unavailable: {0}", error.Message);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        EnsureRaster();
        if (raster == null) return;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImageUnscaled(raster, Point.Empty);
    }

    private void EnsureRaster()
    {
        if (artwork == null || Width < 1 || Height < 1) return;
        if (raster == null || rasterSize != Size || rasterDpi != DeviceDpi)
        {
            raster?.Dispose(); raster = null;
            raster = new Bitmap(Width, Height);
            using (var graphics = Graphics.FromImage(raster))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Min(Width / (float)artwork.Width, Height / (float)artwork.Height);
                float width = artwork.Width * scale, height = artwork.Height * scale;
                graphics.DrawImage(artwork, new RectangleF((Width - width) / 2, (Height - height) / 2, width, height));
            }
            rasterSize = Size; rasterDpi = DeviceDpi;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { raster?.Dispose(); raster = null; artwork?.Dispose(); }
        base.Dispose(disposing);
    }
}
