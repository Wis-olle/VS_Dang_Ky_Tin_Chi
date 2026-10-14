using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Captures only the app's own native client controls for opt-in verification.</summary>
internal static class UiCapture
{
    internal static Bitmap Client(Control control)
    {
        var bitmap = new Bitmap(control.ClientSize.Width, control.ClientSize.Height);
        if (control is not Form)
        {
            control.DrawToBitmap(bitmap, control.ClientRectangle);
            using var canvas = Graphics.FromImage(bitmap);
            SurfaceTransition.PaintForCapture(control, canvas);
            AuthCardMotion.PaintForCapture(control, canvas);
            return bitmap;
        }
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(control.BackColor);
        foreach (Control child in control.Controls.Cast<Control>().Reverse())
        {
            if (!child.Visible || child.Width <= 0 || child.Height <= 0) continue;
            using var childBitmap = new Bitmap(child.Width, child.Height);
            child.DrawToBitmap(childBitmap, child.ClientRectangle);
            graphics.DrawImageUnscaled(childBitmap, child.Location);
        }
        SurfaceTransition.PaintForCapture(control, graphics);
        AuthCardMotion.PaintForCapture(control, graphics);
        return bitmap;
    }
    internal static async Task Entrance(MainForm form, string folder)
    {
        string frames = Path.Combine(folder, "page-motion-frames");
        Directory.CreateDirectory(frames);
        var timestamps = new List<long>();
        var clock = Stopwatch.StartNew();
        await Task.Delay(20); // Let the Shown/BeginInvoke entrance start before the first capture.
        for (int index = 0; index < 28; index++)
        {
            timestamps.Add(clock.ElapsedMilliseconds);
            using var bitmap = Client(form);
            bitmap.Save(Path.Combine(frames, $"frame-{index:00}.png"), ImageFormat.Png);
            await Task.Delay(45);
        }
        File.WriteAllText(Path.Combine(frames, "timings.json"), JsonSerializer.Serialize(timestamps));
    }
}
