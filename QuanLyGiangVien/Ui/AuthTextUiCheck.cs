using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Compares settled zoom glyphs with the native renderer at the final size.</summary>
internal static class AuthTextUiCheck
{
    internal static void Run(MainForm form, string folder, Action<bool, string> expect)
    {
        var faculty = Descendants(form).OfType<DashboardFaculty>().Single();
        var tiles = Descendants(faculty).OfType<AntdUI.Panel>()
            .Where(panel => panel.Controls.OfType<AnimatedDashboardIcon>().Any()).ToArray();
        var cards = Descendants(form).OfType<DashboardStatCard>().Cast<AntdUI.Panel>().Concat(tiles).ToArray();
        var clock = PageMotionCoordinator.Current!;
        clock.SetReducedMotion(true);
        try
        {
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, 1, 1);
            using var actual = UiCapture.Client(form);
            using var reference = new Bitmap(actual.Width, actual.Height, PixelFormat.Format32bppPArgb);
            reference.SetResolution(actual.HorizontalResolution, actual.VerticalResolution);
            using var referenceGraphics = Graphics.FromImage(reference);
            referenceGraphics.Clear(Color.Transparent);
            using var nativePainter = new NativePainter();
            var metrics = new List<object>();
            int differences = 0, sourceInk = 0;
            foreach (var card in cards)
            {
                var tone = Tone(card);
                var origin = form.PointToClient(card.PointToScreen(Point.Empty));
                const float scale = 1.05f;
                var scaledOrigin = new PointF(origin.X + (card.Width - card.Width * scale) / 2,
                    origin.Y + (card.Height - card.Height * scale) / 2);
                foreach (var label in Descendants(card).OfType<AntdUI.Label>())
                {
                    var labelOrigin = card.PointToClient(label.PointToScreen(Point.Empty));
                    var destination = Point.Round(new PointF(scaledOrigin.X + labelOrigin.X * scale,
                        scaledOrigin.Y + labelOrigin.Y * scale));
                    var size = new Size((int)Math.Ceiling(label.Width * scale), (int)Math.Ceiling(label.Height * scale));
                    using var tile = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                    tile.SetResolution(actual.HorizontalResolution, actual.VerticalResolution);
                    using (var graphics = Graphics.FromImage(tile))
                    {
                        graphics.Clear(tone);
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        graphics.ScaleTransform(scale, scale);
                        nativePainter.PaintLabel(label, graphics);
                    }
                    referenceGraphics.DrawImageUnscaled(tile, destination);
                    var comparison = BestAlignment(tile, actual, destination);
                    int ink = InkPixels(tile);
                    differences += comparison.Differences; sourceInk += ink;
                    metrics.Add(new { label.Text, Destination = destination, ExpectedInk = ink,
                        Surface = card is DashboardStatCard ? "Statistic" : "Faculty tile",
                        Font = new { label.Font.Name, label.Font.Size, label.Font.SizeInPoints,
                            Unit = label.Font.Unit.ToString(), Style = label.Font.Style.ToString() },
                        LabelSize = label.Size, SurfaceSize = card.Size, LabelDpi = label.DeviceDpi,
                        comparison.Differences, comparison.OffsetX, comparison.OffsetY });
                }
            }
            var builds = cards.Select(card => AuthCardMotion.State(card).CacheBuilds).ToArray();
            for (int frame = 0; frame < 4; frame++) { using var capture = UiCapture.Client(form); }
            bool reused = cards.Select(card => AuthCardMotion.State(card).CacheBuilds).SequenceEqual(builds);
            actual.Save(Path.Combine(folder, "auth-text-settled-actual.png"), ImageFormat.Png);
            reference.Save(Path.Combine(folder, "auth-text-native-reference.png"), ImageFormat.Png);
            File.WriteAllText(Path.Combine(folder, "auth-text-settled-metrics.json"), JsonSerializer.Serialize(new
            {
                SettledScale = 1.05f, NativeGlyphDifferences = differences, ExpectedInk = sourceInk,
                Labels = metrics, CacheReused = reused,
                OwnerDpi = form.DeviceDpi, LayoutScale = AppTheme.K, OwnerSize = form.ClientSize,
                CaptureDpiX = actual.HorizontalResolution, CaptureDpiY = actual.VerticalResolution,
                Comparison = "Native AntdUI OnPaint at final 1.05 scale; integer alignment ±1; RGB tolerance 1."
            }, new JsonSerializerOptions { WriteIndented = true }));
            expect(tiles.Length == 4 && metrics.Count == 14 && sourceInk > 100,
                "Settled Auth glyph comparison covers six statistics and eight faculty tile labels");
            expect(differences == 0,
                "Fully filled Auth cards preserve native glyph-edge colors at the final 1.05 zoom");
            expect(reused, "Repeated settled Auth capture reuses cached final-size text rasters");
        }
        finally
        {
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, 0);
            clock.SetReducedMotion(false);
        }
    }

    private static Color Tone(AntdUI.Panel card)
    {
        // Expected dashboard palette comes from the design fixture, not production raster state.
        var captions = card.Controls.OfType<AntdUI.Label>().Select(label => label.Text).ToArray();
        if (card is not DashboardStatCard)
            return ColorTranslator.FromHtml(captions[0] switch
            {
                "CNTT" => "#F1FAFB",
                "Kinh tế" => "#FDE4E0",
                "Ngoại ngữ" => "#FAF6FE",
                "Kỹ thuật" => "#F4FCF7",
                _ => throw new InvalidOperationException("Unknown faculty palette fixture")
            });
        var tone = captions.Contains("Tổng giảng viên") ? AppTheme.Peach :
            captions.Contains("Khoa có giảng viên") ? AppTheme.Lilac : AppTheme.Cyan;
        return Color.FromArgb(255, (255 + tone.R) / 2, (255 + tone.G) / 2, (255 + tone.B) / 2);
    }

    private static (int Differences, int OffsetX, int OffsetY) BestAlignment(Bitmap reference, Bitmap actual, Point at)
    {
        var best = (Differences: int.MaxValue, OffsetX: 0, OffsetY: 0);
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int differences = 0;
            for (int y = 1; y < reference.Height - 1; y++)
            for (int x = 1; x < reference.Width - 1; x++)
            {
                var expected = reference.GetPixel(x, y);
                var painted = actual.GetPixel(at.X + x + dx, at.Y + y + dy);
                if (!Ink(expected) && !Ink(painted)) continue;
                if (Math.Abs(expected.R - painted.R) > 1 || Math.Abs(expected.G - painted.G) > 1 ||
                    Math.Abs(expected.B - painted.B) > 1 || expected.A != painted.A) differences++;
            }
            if (differences < best.Differences) best = (differences, dx, dy);
        }
        return best;
    }

    private static int InkPixels(Bitmap image)
    {
        int count = 0;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++) if (Ink(image.GetPixel(x, y))) count++;
        return count;
    }
    private static bool Ink(Color pixel) => pixel.A > 0 && pixel.R < 220 && pixel.G < 220 && pixel.B < 220;
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private sealed class NativePainter : Control
    {
        internal void PaintLabel(AntdUI.Label label, Graphics graphics) =>
            InvokePaint(label, new PaintEventArgs(graphics, label.ClientRectangle));
    }
}
