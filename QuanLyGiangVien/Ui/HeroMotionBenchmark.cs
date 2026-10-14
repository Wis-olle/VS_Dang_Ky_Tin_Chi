using System.Diagnostics;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Observes real WM_PAINT dispatch while the normal UI message pump animates.</summary>
internal static class HeroMotionBenchmark
{
    internal static async Task Run(MainForm form, string folder, string phase)
    {
        var hero = Descendants(form).OfType<DashboardHero>().Single();
        var artwork = Descendants(hero).OfType<FloatingArtwork>().Single();
        long origin = Stopwatch.GetTimestamp();
        int ticks = PageMotionCoordinator.Current?.AmbientTicks ?? 0;
        using var heroProbe = new PaintProbe(hero, origin);
        using var artworkProbe = new PaintProbe(artwork, origin);
        long allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
        await Task.Delay(4000);
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
        double elapsed = Stopwatch.GetElapsedTime(origin).TotalMilliseconds;
        heroProbe.Dispose();
        artworkProbe.Dispose();
        var report = new {
            form.DeviceDpi, AppTheme.K, DurationMilliseconds = elapsed,
            form.Visible, form.WindowState,
            AmbientTicks = (PageMotionCoordinator.Current?.AmbientTicks ?? 0) - ticks,
            Hero = heroProbe.Report(elapsed), Artwork = artworkProbe.Report(elapsed),
            ManagedUiThreadBytes = allocatedBytes,
            Method = "4 seconds of live WM_PAINT dispatch after entrance settling. No screenshots or DrawToBitmap. " +
                "Paint durations include base WndProc dispatch; gaps are between WM_PAINT starts. " +
                "Invalidation gaps observe the actual UI animation update cadence even when hidden startup suppresses WM_PAINT. " +
                "These are UI message-pump measurements, not compositor FPS. Allocations include the observer and other UI-thread work."
        };
        File.WriteAllText(Path.Combine(folder, $"hero-motion-{phase}.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed record PaintSample(double StartedMilliseconds, double DurationMilliseconds);

    private sealed class PaintProbe : NativeWindow, IDisposable
    {
        private const int WmPaint = 0x000F;
        private readonly List<PaintSample> samples = new(320);
        private readonly List<double> invalidations = new(320);
        private readonly Control control;
        private readonly long origin;
        private bool disposed;

        internal PaintProbe(Control control, long origin)
        {
            this.control = control;
            this.origin = origin;
            control.Invalidated += RecordInvalidation;
            AssignHandle(control.Handle);
        }

        private void RecordInvalidation(object? sender, InvalidateEventArgs args) =>
            invalidations.Add(Stopwatch.GetElapsedTime(origin).TotalMilliseconds);

        protected override void WndProc(ref Message message)
        {
            if (message.Msg != WmPaint) { base.WndProc(ref message); return; }
            long started = Stopwatch.GetTimestamp();
            try { base.WndProc(ref message); }
            finally
            {
                samples.Add(new(Stopwatch.GetElapsedTime(origin, started).TotalMilliseconds,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            }
        }

        internal object Report(double observedMilliseconds)
        {
            double[] durations = samples.Select(sample => sample.DurationMilliseconds).Order().ToArray();
            double[] gaps = samples.Zip(samples.Skip(1),
                (previous, current) => current.StartedMilliseconds - previous.StartedMilliseconds).Order().ToArray();
            double[] updateGaps = invalidations.Zip(invalidations.Skip(1), (first, second) => second - first).Order().ToArray();
            return new {
                control.Size, PaintCount = samples.Count,
                PaintDispatchesPerSecond = samples.Count / (observedMilliseconds / 1000),
                MedianDurationMilliseconds = Percentile(durations, .5), P95DurationMilliseconds = Percentile(durations, .95),
                MedianGapMilliseconds = Percentile(gaps, .5), P95GapMilliseconds = Percentile(gaps, .95),
                InvalidationCount = invalidations.Count,
                MedianUpdateGapMilliseconds = Percentile(updateGaps, .5), P95UpdateGapMilliseconds = Percentile(updateGaps, .95),
                Samples = samples.ToArray()
            };
        }

        private static double? Percentile(double[] sorted, double percentile) => sorted.Length == 0 ? null :
            sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            control.Invalidated -= RecordInvalidation;
            ReleaseHandle();
        }
    }
}
