using System.Diagnostics;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in repeatable native rendering workload, not a measurement of screen FPS.</summary>
internal static class MotionPaintBenchmark
{
    internal static void Run(MainForm form, string folder, string phase)
    {
        var hero = Descendants(form).OfType<DashboardHero>().Single();
        var card = Descendants(form).OfType<LecturerCard>().First(item => item.Visible);
        var samples = new[] {
            Measure("Hero float + native paint", hero, frame => hero.ApplyAmbient(frame / 60d, true)),
            Measure("Lecturer hover + native paint", card, frame => card.SetHoverProgressForCheck(frame / 47f))
        };
        card.SetHoverProgressForCheck(0);
        hero.ApplyAmbient(0, false);
        File.WriteAllText(Path.Combine(folder, $"paint-performance-{phase}.json"), JsonSerializer.Serialize(new {
            form.DeviceDpi, AppTheme.K, Samples = samples,
            Method = "48 synchronous DrawToBitmap paints after 8 warmup frames, reusing one destination bitmap; includes preparing each animation state. Not screen FPS."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static object Measure(string name, Control control, Action<int> prepare)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        for (int index = 0; index < 8; index++) { prepare(index); control.DrawToBitmap(bitmap, control.ClientRectangle); }
        var samples = new double[48];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < samples.Length; index++)
        {
            long started = Stopwatch.GetTimestamp();
            prepare(index); control.DrawToBitmap(bitmap, control.ClientRectangle);
            samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Array.Sort(samples);
        return new { Name = name, control.Size, MedianMilliseconds = samples[samples.Length / 2],
            P95Milliseconds = samples[(int)Math.Ceiling(samples.Length * .95) - 1], ManagedBytesPerPaint = allocated / samples.Length };
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
