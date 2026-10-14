using System.Drawing.Imaging;
using System.Reflection;

namespace QuanLyGiangVien.Ui;

internal static class FacultySpreadUiCheck
{
    internal static async Task Run(MainForm form, string folder, Action<bool, string> expect)
    {
        var faculty = Descendants(form).OfType<DashboardFaculty>().Single();
        var tiles = Descendants(faculty).OfType<AntdUI.Panel>()
            .Where(panel => panel.Controls.OfType<AnimatedDashboardIcon>().Any()).ToArray();
        var clock = PageMotionCoordinator.Current!;
        bool animation = AntdUI.Config.Animation;
        try
        {
            clock.SetReducedMotion(true);
            expect(tiles.Length == 4, "Faculty spread has four independent native tiles");
            AuthCardMotion.SetHovered(faculty, true);
            expect(AuthCardMotion.State(faculty).Fill == 0 && AuthCardMotion.State(faculty).Scale == 1,
                "Faculty parent retains entrance only without a competing hover fill or zoom");
            using var idle = UiCapture.Client(form);
            var allBounds = Descendants(faculty).Append(faculty).ToDictionary(c => c, c => c.Bounds);
            foreach (var tile in tiles)
            {
                foreach (var icon in tile.Controls.OfType<AnimatedDashboardIcon>()) icon.Stop();
                var at = form.PointToClient(tile.PointToScreen(Point.Empty));
                AuthCardMotion.SetFrameForCheck(tile, .2f);
                using (var partial = UiCapture.Client(form))
                    expect(partial.GetPixel(at.X + tile.Width - 8, at.Y + tile.Height - 8).ToArgb() != Color.White.ToArgb()
                        && partial.GetPixel(at.X + 8, at.Y + tile.Height / 2).ToArgb() == Color.White.ToArgb(),
                        $"Faculty {tile.AccessibleName} has a diagonal color spread rather than a uniform fade");
                AuthCardMotion.SetFrameForCheck(tile, 1, 1);
                using (var filled = UiCapture.Client(form))
                {
                    var expectedFill = ColorTranslator.FromHtml(tile.Controls.OfType<AntdUI.Label>().First().Text switch
                    {
                        "CNTT" => "#F1FAFB",
                        "Kinh tế" => "#FDE4E0",
                        "Ngoại ngữ" => "#FAF6FE",
                        "Kỹ thuật" => "#F4FCF7",
                        _ => throw new InvalidOperationException("Unknown faculty palette fixture")
                    });
                    expect(filled.GetPixel(at.X + 8, at.Y + tile.Height / 2).ToArgb() == expectedFill.ToArgb(),
                        $"Settled faculty {tile.AccessibleName} uses its own pastel fill while icon palettes remain unchanged");
                    var changed = new Rectangle(at, tile.Size); changed.Inflate(AppTheme.S(4), AppTheme.S(4));
                    var whole = new Rectangle(form.PointToClient(faculty.PointToScreen(Point.Empty)), faculty.Size);
                    expect(OutsideMatches(idle, filled, whole, changed),
                        "Faculty tile overlay preserves the title and every neighboring tile");
                }
                var before = AuthCardMotion.State(tile);
                var label = tile.Controls.OfType<AntdUI.Label>().First();
                typeof(Control).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(label, [EventArgs.Empty]);
                expect(AuthCardMotion.State(tile).Fill == before.Fill && !AuthCardMotion.State(tile).Running,
                    "Moving across faculty icon count and caption does not restart a settled spread");
                using (var capture = UiCapture.Client(form)) { }
                int cached = AuthCardMotion.State(tile).CacheBuilds;
                using (var capture = UiCapture.Client(form)) { }
                expect(AuthCardMotion.State(tile).CacheBuilds == cached, "Settled faculty spread reuses its text cache");
                AuthCardMotion.SetHovered(tile, false);
                expect(AuthCardMotion.State(tile).Fill == 0 && AuthCardMotion.State(tile).Scale == 1,
                    "Reduced-motion faculty exit immediately restores the white tile");
            }
            expect(allBounds.All(pair => pair.Key.Bounds == pair.Value), "Faculty spread and zoom keep all native hitboxes fixed");
            foreach (var tile in tiles) AuthCardMotion.SetHovered(tile, true);
            using (var capture = UiCapture.Client(form))
            {
                var area = new Rectangle(form.PointToClient(faculty.PointToScreen(Point.Empty)), faculty.Size);
                area.Inflate(AppTheme.S(4), AppTheme.S(4)); area.Intersect(new(Point.Empty, capture.Size));
                using var crop = capture.Clone(area, PixelFormat.Format32bppArgb);
                crop.Save(Path.Combine(folder, "31-faculty-tiles-spread.png"), ImageFormat.Png);
            }
            foreach (var tile in tiles) AuthCardMotion.SetHovered(tile, false);
            expect(AuthCardMotion.ActiveCount == 0, "Faculty exits release every tile overlay and finite job");
            clock.SetReducedMotion(false);
            AuthCardMotion.Reveal(faculty);
            AuthCardMotion.SetHovered(tiles[0], true);
            expect(AuthCardMotion.State(tiles[0]).Fill == 0 && !AuthCardMotion.State(tiles[0]).Running
                && AuthCardMotion.ActiveCount == 1,
                "Faculty tile hover waits for the parent entrance instead of popping in above it");
            AuthCardMotion.SetHovered(tiles[0], false);
            await Task.Delay(1400);
            expect(AuthCardMotion.State(tiles[0]).Fill == 0 && AuthCardMotion.ActiveCount == 0,
                "Leaving during the parent entrance cancels a pending tile hover");
            AuthCardMotion.Reveal(faculty);
            AuthCardMotion.SetHovered(tiles[0], true);
            await Task.Delay(1400);
            expect(AuthCardMotion.State(faculty).Entrance == 1 && AuthCardMotion.State(tiles[0]).Running
                && AuthCardMotion.State(tiles[0]).Fill > 0,
                "A tile still hovered after entrance begins its finite color spread");
            clock.SetReducedMotion(true);
            AuthCardMotion.SetHovered(tiles[0], false);
            clock.SetReducedMotion(false);
            AuthCardMotion.Reveal(faculty);
            AuthCardMotion.SetHovered(tiles[0], true);
            faculty.Hide();
            expect(AuthCardMotion.ActiveCount == 0 && !AuthCardMotion.State(tiles[0]).Running,
                "Hiding faculty cancels parent entrance and pending child hover resources");
            faculty.Show();
        }
        finally { clock.SetReducedMotion(!animation); }
    }
    private static bool OutsideMatches(Bitmap first, Bitmap second, Rectangle whole, Rectangle changed)
    {
        for (int y = whole.Top; y < whole.Bottom; y++)
        for (int x = whole.Left; x < whole.Right; x++)
            if (!changed.Contains(x, y) && first.GetPixel(x, y) != second.GetPixel(x, y)) return false;
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
