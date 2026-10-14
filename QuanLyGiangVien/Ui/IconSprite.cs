using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>One locally packaged sprite sheet; owned and disposed by its icon control.</summary>
internal sealed class IconSprite(Bitmap bitmap, int frameSize, IconTiming timing) : IDisposable
{
    public Bitmap Bitmap { get; } = bitmap;
    public int FrameSize { get; } = frameSize;
    public IconTiming Timing { get; } = timing;

    private sealed record Metadata(string Id, int FrameSize, int FrameCount,
        int DurationMilliseconds, int IdleFrame, string Sheet);

    public static IconSprite Load(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Any(c => !char.IsAsciiLetterLower(c) && c != '-'))
            throw new ArgumentException("Mã icon không hợp lệ.", nameof(id));
        var folder = Path.Combine(AppContext.BaseDirectory, "Assets", "animated-icons");
        var json = File.ReadAllText(Path.Combine(folder, id + ".json"));
        var metadata = JsonSerializer.Deserialize<Metadata>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (metadata is null || metadata.Id != id || metadata.FrameSize is < 32 or > 256 ||
            metadata.FrameCount is < 2 or > 120 || metadata.DurationMilliseconds is < 100 or > 5000 ||
            metadata.Sheet != id + ".png" || metadata.IdleFrame != metadata.FrameCount - 1)
            throw new InvalidDataException("Thông tin icon động không hợp lệ: " + id);
        var timing = new IconTiming(metadata.FrameCount, metadata.DurationMilliseconds, metadata.IdleFrame);
        using var source = new Bitmap(Path.Combine(folder, metadata.Sheet));
        if (source.Width != metadata.FrameSize * metadata.FrameCount || source.Height != metadata.FrameSize)
            throw new InvalidDataException("Kích thước ảnh icon không khớp: " + id);
        return new(new Bitmap(source), metadata.FrameSize, timing);
    }

    public void Dispose() => Bitmap.Dispose();
}
