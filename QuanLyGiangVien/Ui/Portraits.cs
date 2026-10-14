namespace QuanLyGiangVien.Ui;

/// <summary>Offline lecturer avatars; no network requests or personal data transmission at runtime.</summary>
public static class Portraits
{
    private static readonly Dictionary<string, Image> cache = new();
    public static Image Get(string key)
    {
        if (cache.TryGetValue(key, out var found)) return found;
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "avatars", key + ".png");
        Image image;
        if (File.Exists(path))
        {
            using var source = Image.FromFile(path);
            image = new Bitmap(source);
        }
        else
        {
            image = new Bitmap(256, 256);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(AppTheme.Lilac);
            using var font = new Font("Segoe UI", 48, FontStyle.Bold);
            TextRenderer.DrawText(graphics, "GV", font, new Rectangle(0, 0, 256, 256), AppTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        cache.Add(key, image);
        return image;
    }
    public static void Release()
    {
        foreach (var image in cache.Values) image.Dispose();
        cache.Clear();
    }
}
