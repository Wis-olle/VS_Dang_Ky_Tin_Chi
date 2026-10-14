using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal static class SchedulingServiceUiCheck
{
    internal static void CheckWholeCardClicks(MainForm form, SchedulingPage page, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        using var input = new ClickDispatcher();
        int[] targets = [0, 1, 2, 0];
        var stats = page.StatsForCheck.Cast<SchedulingStat>().ToArray();
        for (int index = 0; index < stats.Length; index++)
        {
            var stat = stats[index];
            page.SwitchTab(targets[index] == 0 ? 1 : 0);
            input.Dispatch(stat);
            Expect(page.TabIndexForCheck == targets[index], "Clicking the card body opens its workspace: " + index);
            page.SwitchTab(targets[index] == 0 ? 1 : 0);
            input.Dispatch(stat.ArtworkForCheck);
            Expect(page.TabIndexForCheck == targets[index], "Clicking the artwork opens its card workspace: " + index);
            Expect(stat.Cursor == Cursors.Hand && stat.ArtworkForCheck.Cursor == Cursors.Hand,
                "Whole card and artwork display the hand cursor: " + index);
        }
        using var fixture = new SchedulingStat("Phòng học", "door-open", SchedulingTheme.Lavender)
            { Size = new(AppTheme.S(300), AppTheme.S(180)) };
        form.Controls.Add(fixture);
        int activations = 0;
        fixture.SetAction("Xem phòng", () => activations++);
        input.Dispatch(fixture); input.Dispatch(fixture.ArtworkForCheck); fixture.ActionForCheck.PerformClick();
        Expect(activations == 3, "Body, artwork and link each invoke exactly one card action");
        fixture.Enabled = false;
        input.Dispatch(fixture); input.Dispatch(fixture.ArtworkForCheck); fixture.ActionForCheck.PerformClick();
        Expect(activations == 3, "Disabled card does not activate through any click surface");
        fixture.Enabled = true; fixture.SetAction("", () => activations++);
        input.Dispatch(fixture); input.Dispatch(fixture.ArtworkForCheck);
        Expect(activations == 3, "Card without an action stays inactive");
        page.SwitchTab(0);
    }

    private sealed class ClickDispatcher : Control
    {
        internal void Dispatch(Control target) => InvokeOnClick(target, EventArgs.Empty);
    }

    internal static void CheckAssets(SchedulingPage page, List<string> checks)
    {
        bool ready = page.StatsForCheck.Cast<SchedulingStat>().All(stat => stat.ArtworkForCheck.ReadyForCheck);
        if (!ready) throw new InvalidOperationException("All four Service Cards load their local transparent 3D artwork");
        checks.Add("All four Service Cards load their local transparent 3D artwork");
        foreach (var stat in page.StatsForCheck.Cast<SchedulingStat>())
        {
            using (var rendered = UiCapture.Client(stat)) { }
            if (stat.ArtworkForCheck.OwnedBitmapsForCheck.Last().Size != stat.ArtworkForCheck.AlphaBoundsForCheck.Size)
                throw new InvalidOperationException("Hover renderer retains original artwork pixels instead of upscaling a reduced thumbnail: " + stat.AccessibleName);
            checks.Add("Hover renderer retains original artwork pixels instead of upscaling a reduced thumbnail: " + stat.AccessibleName);
            var source = stat.ArtworkForCheck.OwnedBitmapsForCheck.First();
            int colored = 0;
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    if (pixel.A > 220 && Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) > 35) colored++;
                }
            if (colored < 200) throw new InvalidOperationException("Service artwork contains actual colored pixels: " + stat.AccessibleName);
            checks.Add("Service artwork contains actual colored pixels: " + stat.AccessibleName);
        }
    }

    internal static async Task Run(MainForm form, SchedulingPage page, string folder, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        page.ScrollToTopForCheck();
        var stats = page.StatsForCheck.Cast<SchedulingStat>().ToArray();
        foreach (var stat in stats)
        {
            var art = stat.ArtworkForCheck;
            Expect(art.AlphaBoundsForCheck is { Width: > 0, Height: > 0, X: > 0, Y: > 0 }, "3D artwork uses its actual alpha bounds: " + stat.AccessibleName);
            using (var image = UiCapture.Client(stat))
            {
                using var state = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(stat.VisualStateForCheck()));
                var background = Color.FromArgb(state.RootElement.GetProperty("Background").GetInt32());
                int coloredPixels = 0;
                for (int y = image.Height / 3; y < image.Height - 8; y++)
                    for (int x = image.Width * 2 / 3; x < image.Width - 8; x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        int saturation = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                        int difference = Math.Max(Math.Abs(pixel.R - background.R), Math.Max(Math.Abs(pixel.G - background.G), Math.Abs(pixel.B - background.B)));
                        if (pixel.A > 220 && saturation > 35 && difference > 40) coloredPixels++;
                    }
                Expect(coloredPixels > 80, "Service Card paints colored 3D artwork distinct from its background: " + stat.AccessibleName);
            }
            int cacheBuilds = art.CacheBuildsForCheck;
            using (var image = UiCapture.Client(stat)) { }
            Expect(art.CacheBuildsForCheck == cacheBuilds, "Repeated card paint reuses the artwork raster: " + stat.AccessibleName);
        }
        int[] targets = [0, 1, 2, 0];
        for (int index = 0; index < stats.Length; index++)
        {
            stats[index].ActionForCheck.PerformClick();
            Expect(page.TabIndexForCheck == targets[index], "Service Card native action opens its existing workspace: " + stats[index].ActionForCheck.Text);
            Expect(stats[index].ActionForCheck.TabStop && stats[index].ActionForCheck.AccessibleName?.Length > 0,
                "Service Card action remains a named native keyboard button: " + index);
            var glyphs = stats[index].ActionGlyphsForCheck;
            Expect(glyphs.ArrowLeft - glyphs.TextRight >= AppTheme.S(6) && glyphs.ArrowLeft - glyphs.TextRight <= AppTheme.S(6) + AppTheme.S(5)
                && glyphs.ArrowRight < stats[index].ActionForCheck.Width,
                "Action arrow stays close to its measured label and inside the button: " + index);
        }
        var first = stats[0];
        var initialBounds = (first.Bounds, first.ActionForCheck.Bounds, first.ArtworkForCheck.Bounds);
        first.AnimateHover(true);
        Expect(first.ArtworkForCheck.StateForCheck.Running, "Service image hover starts a finite shared-clock transition");
        await Task.Delay(110);
        var middle = first.ArtworkForCheck.StateForCheck;
        Expect(middle.Scale > 1 && middle.Scale < 1.1f && middle.Rotation > 0 && middle.OffsetX > 0,
            "Service image scale, rotation and offset interpolate together");
        first.AnimateHover(false);
        Expect(first.ArtworkForCheck.StateForCheck.Scale == middle.Scale, "Rapid hover reversal retains the current painted image transform");
        await Task.Delay(500);
        Expect(first.ArtworkForCheck.StateForCheck is { Scale: 1, Rotation: 0, OffsetX: 0, Running: false }, "Service image returns to its idle transform and stops");
        Expect(initialBounds == (first.Bounds, first.ActionForCheck.Bounds, first.ArtworkForCheck.Bounds),
            "Service hover preserves card, artwork and native action hitboxes");
        using (var fixture = new SchedulingStat("Phòng học", "door-open", SchedulingTheme.Lavender)
            { Size = new(AppTheme.S(244), AppTheme.S(180)) })
        {
            form.Controls.Add(fixture);
            fixture.SetValue(8, "7 sẵn sàng · 1 bảo trì", animate: false); fixture.SetRatio(.875f, false);
            fixture.SetAction("Xem phòng", () => { });
            fixture.AnimateHover(true); fixture.Visible = false;
            Expect(!fixture.MotionRunningForCheck, "Hiding a Service Card stops its finite image, arrow and metric jobs");
            fixture.Visible = true; AntdUI.Config.Animation = false;
            fixture.AnimateHover(true);
            Expect(!fixture.MotionRunningForCheck, "Reduced motion applies Service Card hover without animation jobs");
            fixture.AnimateHover(false); AntdUI.Config.Animation = true;
            using (var image = UiCapture.Client(fixture)) { }
            var images = fixture.ArtworkForCheck.OwnedBitmapsForCheck;
            var fonts = fixture.OwnedFontsForCheck;
            fixture.Dispose();
            Expect(images.Count > 0 && images.All(IsDisposed), "Disposing a Service Card releases the original image and every cached raster");
            Expect(fonts.All(IsDisposed), "Disposing a Service Card releases its native text and action fonts");
        }
        await CheckHoverEdges(form, page, folder, Expect);
        await Task.Delay(1200);
        Expect(PageMotionCoordinator.Current?.ActiveCount == 0, "Service Card interactions leave the shared clock idle");
    }

    private static async Task CheckHoverEdges(MainForm form, SchedulingPage page, string folder, Action<bool, string> expect)
    {
        foreach (var (width, height, name) in new[] { (1440, 900, "wide"), (1100, 720, "compact") })
        {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(height));
            await Task.Delay(200);
            var stats = page.StatsForCheck.Cast<SchedulingStat>().ToArray();
            var borders = stats.Select(stat => { using var image = UiCapture.Client(stat.ArtworkForCheck); return BorderPixels(image); }).ToArray();
            var cacheBuilds = stats.Select(stat => { using var image = UiCapture.Client(stat); return stat.ArtworkForCheck.CacheBuildsForCheck; }).ToArray();
            foreach (var stat in stats) stat.AnimateHover(true);
            await Task.Delay(450);
            for (int index = 0; index < stats.Length; index++)
            {
                var stat = stats[index]; var artwork = stat.ArtworkForCheck;
                using var image = UiCapture.Client(artwork);
                expect(stat.ClientRectangle.Contains(artwork.Bounds), $"Artwork viewport stays inside its card at {name}: {stat.AccessibleName}");
                expect(BorderPixels(image).SequenceEqual(borders[index]),
                    $"Zoom adds no artwork pixels to any viewport edge at {name}: {stat.AccessibleName}");
                expect(artwork.CacheBuildsForCheck == cacheBuilds[index], $"Hover reuses native-resolution artwork without rebuilding its cache at {name}: {stat.AccessibleName}");
            }
            using (var image = UiCapture.Client(page.KpiHostForCheck))
                image.Save(Path.Combine(folder, $"27-service-hover-{name}.png"), System.Drawing.Imaging.ImageFormat.Png);
            using (var image = UiCapture.Client(stats[3]))
                image.Save(Path.Combine(folder, $"28-enrollment-hover-{name}.png"), System.Drawing.Imaging.ImageFormat.Png);
            foreach (var stat in stats) stat.AnimateHover(false);
            await Task.Delay(650);
        }
        form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
    }

    private static int[] BorderPixels(Bitmap image)
    {
        var pixels = new List<int>();
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if (x < 2 || y < 2 || x >= image.Width - 2 || y >= image.Height - 2) pixels.Add(image.GetPixel(x, y).ToArgb());
        return pixels.ToArray();
    }

    private static bool IsDisposed(Bitmap bitmap)
    {
        try { _ = bitmap.GetPixel(0, 0); return false; }
        catch (Exception error) when (error is ArgumentException or ObjectDisposedException) { return true; }
    }
    private static bool IsDisposed(Font font)
    {
        try { _ = font.GetHeight(96f); return false; }
        catch (Exception error) when (error is ArgumentException or ObjectDisposedException) { return true; }
    }
}
