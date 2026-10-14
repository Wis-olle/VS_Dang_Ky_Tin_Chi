using System.Diagnostics;
using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed record SchedulingVisual(string Key, Bitmap? Image, string Caption, Color Tone);

/// <summary>The scheduling page owns the source images and reusable table thumbnails.</summary>
internal sealed class SchedulingVisualCatalog : IDisposable
{
    private readonly Dictionary<string, Bitmap> images = [];
    private readonly Dictionary<string, Bitmap> thumbnails = [];
    private bool disposed;
    internal IReadOnlyCollection<string> LoadedKeysForCheck => images.Keys;
    internal IReadOnlyList<Bitmap> OwnedBitmapsForCheck => images.Values.Concat(thumbnails.Values).ToArray();

    internal SchedulingVisualCatalog()
    {
        foreach (string key in new[] { "coding", "finance", "language", "engineering", "classroom", "lab", "hall" })
        {
            try
            {
                using var source = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling", "resource-visuals", key + ".png"));
                images.Add(key, new Bitmap(source));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException)
            { Trace.TraceWarning("Scheduling resource image unavailable ({0}): {1}", key, error.Message); }
        }
    }

    internal SchedulingVisual GetCourse(Course? course) => course?.Faculty switch {
        "Công nghệ thông tin" => Get("coding", course.Faculty, SchedulingTheme.Cyan),
        "Kinh tế" => Get("finance", course.Faculty, SchedulingTheme.Amber),
        "Ngoại ngữ" => Get("language", course.Faculty, SchedulingTheme.Lavender),
        "Kỹ thuật" => Get("engineering", course.Faculty, SchedulingTheme.Mint),
        _ => Get("language", course?.Faculty ?? "Học phần", SchedulingTheme.Lavender)
    };

    internal SchedulingVisual GetRoom(Room? room) => room?.Type switch {
        SchedulingLabels.Lab => Get("lab", room.Type, SchedulingTheme.Lavender),
        SchedulingLabels.Hall => Get("hall", room.Type, SchedulingTheme.Amber),
        _ => Get("classroom", room?.Type ?? "Phòng học", SchedulingTheme.Cyan)
    };

    private SchedulingVisual Get(string key, string caption, Color tone)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return new(key, images.GetValueOrDefault(key), caption, tone);
    }

    internal Bitmap Thumbnail(SchedulingVisual visual)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string cacheKey = visual.Key + ":" + AppTheme.K;
        if (thumbnails.TryGetValue(cacheKey, out var cached)) return cached;
        var bitmap = new Bitmap(AppTheme.S(48), AppTheme.S(36));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(visual.Tone);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (visual.Image is { } image)
            {
                float scale = Math.Min((bitmap.Width - AppTheme.S(8)) / (float)image.Width, (bitmap.Height - AppTheme.S(6)) / (float)image.Height);
                float width = image.Width * scale, height = image.Height * scale;
                graphics.DrawImage(image, new RectangleF((bitmap.Width - width) / 2, (bitmap.Height - height) / 2, width, height));
            }
        }
        thumbnails.Add(cacheKey, bitmap);
        return bitmap;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var bitmap in thumbnails.Values) bitmap.Dispose();
        foreach (var bitmap in images.Values) bitmap.Dispose();
        thumbnails.Clear(); images.Clear();
    }
}
