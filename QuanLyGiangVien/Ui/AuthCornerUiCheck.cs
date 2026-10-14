using System.Drawing.Imaging;

namespace QuanLyGiangVien.Ui;

/// <summary>Native regression for the rounded edge where the first stat meets its viewport.</summary>
internal static class AuthCornerUiCheck
{
    internal static void Run(MainForm form, string folder, Action<bool, string> expect)
    {
        var stats = Descendants(form).OfType<DashboardStats>().Single();
        var cards = stats.Controls.OfType<DashboardStatCard>().OrderBy(card => card.Left).ToArray();
        var first = cards[0];
        var body = stats.Parent as ScrollableControl
            ?? throw new InvalidOperationException("Statistics must remain in a scrollable dashboard body.");
        var scrollMinimum = body.AutoScrollMinSize;
        var scrollPosition = body.AutoScrollPosition;
        var clock = PageMotionCoordinator.Current!;
        bool animation = AntdUI.Config.Animation;
        var bounds = Descendants(stats).Append(stats).ToDictionary(control => control, control => control.Bounds);
        try
        {
            clock.SetReducedMotion(true);
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, 0);
            using var idle = UiCapture.Client(form);
            var origin = form.PointToClient(first.PointToScreen(Point.Empty));
            AuthCardMotion.SetFrameForCheck(first, 1, 1);
            using var hovered = UiCapture.Client(form);
            const float scale = 1.05f;
            var zoomBounds = new RectangleF(origin.X + (first.Width - first.Width * scale) / 2,
                origin.Y + (first.Height - first.Height * scale) / 2, first.Width * scale, first.Height * scale);
            int edgeX = (int)Math.Ceiling(zoomBounds.Left) + 1;
            var fill = ColorTranslator.FromHtml("#FDE4E0");
            expect(edgeX < origin.X
                && hovered.GetPixel(edgeX, origin.Y + first.Height / 2).ToArgb() == fill.ToArgb(),
                "Fully visible first statistic exposes its zoomed left edge in empty canvas beyond the viewport body");
            expect(hovered.GetPixel(edgeX, (int)Math.Ceiling(zoomBounds.Top) + 1).ToArgb() == AppTheme.Canvas.ToArgb()
                && hovered.GetPixel(edgeX, (int)Math.Floor(zoomBounds.Bottom) - 2).ToArgb() == AppTheme.Canvas.ToArgb(),
                "First statistic keeps both centered zoom corners visible in the surrounding empty canvas");
            expect(hovered.GetPixel(origin.X + AppTheme.S(8), origin.Y + first.Height / 2).ToArgb() == fill.ToArgb(),
                "Viewport corner protection retains the settled peach fill inside the first statistic");
            expect(bounds.All(pair => pair.Key.Bounds == pair.Value),
                "Viewport-safe Auth zoom leaves native cards parent and descendant hitboxes fixed");
            expect(cards.Skip(1).All(card => RegionMatches(idle, hovered,
                new Rectangle(form.PointToClient(card.PointToScreen(Point.Empty)), card.Size))),
                "First statistic corner correction preserves neighboring statistic pixels");
            var cropArea = Rectangle.Ceiling(zoomBounds);
            cropArea.Inflate(AppTheme.S(4), AppTheme.S(4));
            cropArea.Intersect(new Rectangle(Point.Empty, hovered.Size));
            using var crop = hovered.Clone(cropArea, PixelFormat.Format32bppArgb);
            crop.Save(Path.Combine(folder, "33-auth-first-stat-corners.png"), ImageFormat.Png);
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, 0);
            body.AutoScrollMinSize = new Size(body.ClientSize.Width + AppTheme.S(200), scrollMinimum.Height);
            body.AutoScrollPosition = new Point(AppTheme.S(80), -scrollPosition.Y);
            var viewportOrigin = form.PointToClient(body.PointToScreen(Point.Empty));
            var scrolledOrigin = form.PointToClient(first.PointToScreen(Point.Empty));
            expect(body.AutoScrollPosition.X < 0 && scrolledOrigin.X < viewportOrigin.X
                && scrolledOrigin.X + first.Width > viewportOrigin.X,
                "Corner guard fixture places the first statistic partially behind a real horizontal scroll viewport");
            AuthCardMotion.SetFrameForCheck(first, 1, 1);
            using var scrolled = UiCapture.Client(form);
            expect(scrolled.GetPixel(viewportOrigin.X - 3, scrolledOrigin.Y + first.Height / 2).ToArgb()
                    == AppTheme.Canvas.ToArgb(),
                "A partially scrolled statistic remains clipped instead of leaking fill outside the viewport");
        }
        finally
        {
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, 0);
            body.AutoScrollMinSize = scrollMinimum;
            body.AutoScrollPosition = new Point(-scrollPosition.X, -scrollPosition.Y);
            clock.SetReducedMotion(!animation);
        }
    }

    private static bool RegionMatches(Bitmap first, Bitmap second, Rectangle area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
        for (int x = area.Left; x < area.Right; x++)
            if (first.GetPixel(x, y) != second.GetPixel(x, y)) return false;
        return true;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
