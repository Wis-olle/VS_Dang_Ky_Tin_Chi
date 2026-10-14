using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

internal static class AuthCardUiCheck
{
    internal static async Task Run(MainForm form, string folder, Action<bool, string> expect)
    {
        AuthCornerUiCheck.Run(form, folder, expect);
        AuthTextUiCheck.Run(form, folder, expect);
        var cards = Descendants(form).OfType<DashboardStatCard>().Cast<AntdUI.Panel>()
            .Append(Descendants(form).OfType<DashboardFaculty>().Single()).ToArray();
        var hoverSurfaces = cards.Take(3).Concat(Descendants(cards[3]).OfType<AntdUI.Panel>()
            .Where(panel => panel.Controls.OfType<AnimatedDashboardIcon>().Any())).ToArray();
        expect(cards.Length == 4, "Auth Form motion is bound to exactly three statistics and the faculty surface");
        var bounds = cards.Select(c => c.Bounds).ToArray();
        var childBounds = cards.Select(c => Descendants(c).Select(item => item.Bounds).ToArray()).ToArray();
        var stats = Descendants(form).OfType<DashboardStats>().Single();
        var clock = PageMotionCoordinator.Current!;
        foreach (var card in cards) AuthCardMotion.Reveal(card);
        expect(cards.All(c => AuthCardMotion.State(c).Entrance == 0), "All four cards start the 1250ms fade and slide at zero opacity");
        await Task.Delay(350);
        expect(cards.All(c => AuthCardMotion.State(c).Entrance is > 0 and < 1), "Auth entrance has intermediate opacity and float translation on the shared clock");
        await Task.Delay(1100);
        expect(cards.All(c => AuthCardMotion.State(c).Entrance == 1) && AuthCardMotion.ActiveCount == 0,
            "Completed entrances release all four overlays");
        expect(cards.Select(c => c.Bounds).SequenceEqual(bounds), "Entrance leaves all live surface bounds fixed");
        for (int i = 0; i < cards.Length; i++)
            expect(Descendants(cards[i]).Select(item => item.Bounds).SequenceEqual(childBounds[i]), $"Auth card {i + 1} leaves descendant hitboxes and geometry fixed");
        expect(stats.DisplayedCounts.SequenceEqual(stats.ActualCounts), "Statistics retain the real counts after Auth Form entrance");
        CaptureRow(form, cards, folder, "24-auth-cards-resting");
        clock.SetReducedMotion(true);
        using (var idleText = UiCapture.Client(form))
        {
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, .0001f);
            using var overlayText = UiCapture.Client(form);
            bool sameMetrics = true;
            var metrics = new List<object>();
            foreach (var metricLabel in cards.SelectMany(Descendants).OfType<AntdUI.Label>())
            {
                var area = new Rectangle(form.PointToClient(metricLabel.PointToScreen(Point.Empty)), metricLabel.Size);
                var idle = InkExtent(idleText, area);
                var active = InkExtent(overlayText, area);
                // Alpha compositing can move a thresholded antialias edge by one pixel;
                // a font/renderer change moves the glyph extent by several pixels.
                sameMetrics &= idle.IsEmpty || active.IsEmpty ? idle == active :
                    Math.Abs(idle.Left - active.Left) <= 1 && Math.Abs(idle.Top - active.Top) <= 1 &&
                    Math.Abs(idle.Right - active.Right) <= 1 && Math.Abs(idle.Bottom - active.Bottom) <= 1;
                metrics.Add(new { metricLabel.Text, idle, active });
            }
            File.WriteAllText(Path.Combine(folder, "auth-label-metrics.json"), JsonSerializer.Serialize(metrics));
            expect(sameMetrics, "Auth overlay preserves native glyph extents within one antialias pixel at hover onset");
            foreach (var card in cards) AuthCardMotion.SetFrameForCheck(card, 0);
        }
        clock.SetReducedMotion(false);
        var first = cards[0];
        AuthCardMotion.SetHovered(first, true);
        await Task.Delay(180);
        var growing = AuthCardMotion.State(first);
        expect(growing.Fill is > 0 and < 1 && growing.Scale is > 1 and < 1.05f && growing.Running,
            "Hover grows the foreground and circular fill together on the shared clock");
        Enter(first.Controls.OfType<AntdUI.Label>().First());
        var afterChild = AuthCardMotion.State(first);
        expect(afterChild.Scale == growing.Scale && afterChild.Fill == growing.Fill && afterChild.Running,
            "Entering a child during zoom preserves the in-flight scale and circle progress");
        bool increasing = true;
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(60);
            var next = AuthCardMotion.State(first);
            increasing &= next.Scale >= growing.Scale && next.Scale <= 1.05f && next.Fill >= growing.Fill;
            growing = next;
        }
        expect(increasing, "Native zoom-in progresses monotonically without bounce or overshoot");
        await Task.Delay(100);
        expect(AuthCardMotion.State(first).Fill == 1 && AuthCardMotion.State(first).Scale == 1.05f,
            "Faster hover completes circular fill and a steady 1.05 scale after the monotonic progression checks");
        expect(first.Bounds == bounds[0], "Hover keeps the original stat-card hitbox");
        var frameIcon = Descendants(first).OfType<AnimatedDashboardIcon>().Single();
        frameIcon.Play();
        int frameBefore = frameIcon.FrameIndex;
        await Task.Delay(90);
        expect(frameIcon.FrameIndex != frameBefore && frameIcon.IsPlaying,
            "Icon playback advances while the Auth hover overlay is settled");
        using (var capture = UiCapture.Client(form)) { }
        int caches = AuthCardMotion.State(first).CacheBuilds;
        for (int i = 0; i < 8; i++)
        {
            using var capture = UiCapture.Client(form);
        }
        expect(AuthCardMotion.State(first).CacheBuilds == caches, "Repeated Auth hover paints reuse cached label rasters");
        AuthCardMotion.SetHovered(first, false);
        await Task.Delay(120);
        float reversing = AuthCardMotion.State(first).Fill;
        float reversingScale = AuthCardMotion.State(first).Scale;
        AuthCardMotion.SetHovered(first, true);
        expect(Math.Abs(AuthCardMotion.State(first).Fill - reversing) < .0001f
            && Math.Abs(AuthCardMotion.State(first).Scale - reversingScale) < .0001f,
            "Fast hover reversal preserves the current circle position and fractional zoom");
        await Task.Delay(1100);
        var child = first.Controls.OfType<AntdUI.Label>().First();
        int jobs = clock.ActiveCount;
        Enter(child);
        expect(clock.ActiveCount == jobs && !AuthCardMotion.State(first).Running, "Moving into a child does not restart the settled card hover");
        AuthCardMotion.SetHovered(first, false);
        float previousScale = AuthCardMotion.State(first).Scale;
        bool decreasing = true;
        for (int i = 0; i < 8; i++)
        {
            await Task.Delay(60);
            float nextScale = AuthCardMotion.State(first).Scale;
            decreasing &= nextScale <= previousScale && nextScale >= 1;
            previousScale = nextScale;
        }
        expect(decreasing, "Native zoom-out shrinks monotonically without oscillation");
        await Task.Delay(650);
        expect(AuthCardMotion.ActiveCount == 0 && AuthCardMotion.State(first).Scale == 1,
            "Leaving a card restores white and releases its overlay and finite motions");
        clock.SetReducedMotion(true);
        using var resting = UiCapture.Client(form);
        AuthCardMotion.SetFrameForCheck(first, .2f);
        using (var partial = UiCapture.Client(form))
        {
            var at = form.PointToClient(first.PointToScreen(Point.Empty));
            expect(partial.GetPixel(at.X + first.Width - 12, at.Y + first.Height - 12).ToArgb() != Color.White.ToArgb()
                && partial.GetPixel(at.X + 12, at.Y + first.Height / 2).ToArgb() == Color.White.ToArgb(),
                "Native pixels show the circular fill starting at bottom right before reaching the left edge");
        }
        AuthCardMotion.SetFrameForCheck(first, 0);
        foreach (var card in hoverSurfaces) AuthCardMotion.SetHovered(card, true);
        expect(hoverSurfaces.All(c => !AuthCardMotion.State(c).Running && AuthCardMotion.State(c).Fill == 1),
            "Reduced motion immediately completes the card hover without a new timer");
        CaptureRow(form, cards, folder, "25-auth-cards-hover");
        using (var hovered = UiCapture.Client(form))
        {
            var at = form.PointToClient(cards[1].PointToScreen(Point.Empty));
            expect(hovered.GetPixel(at.X + cards[1].Width + 2, at.Y + cards[1].Height / 2).ToArgb()
                != resting.GetPixel(at.X + cards[1].Width + 2, at.Y + cards[1].Height / 2).ToArgb(),
                "Native pixels confirm that the whole foreground zooms beyond the fixed hitbox");
            var hero = Descendants(form).OfType<DashboardHero>().Single();
            var heroAt = form.PointToClient(hero.PointToScreen(Point.Empty));
            bool same = true;
            for (int x = heroAt.X + 4; x < heroAt.X + hero.Width - 4; x += 7)
                same &= hovered.GetPixel(x, heroAt.Y + hero.Height - 4).ToArgb() == resting.GetPixel(x, heroAt.Y + hero.Height - 4).ToArgb();
            expect(same, "Auth overlay padding preserves the neighboring hero's lower foreground and background");
            var body = cards[3].Parent as ScrollableControl ?? throw new InvalidOperationException("Faculty must remain in the dashboard body.");
            expect(!body.HorizontalScroll.Visible, "Auth hover does not add a horizontal dashboard scrollbar");
        }
        foreach (var card in hoverSurfaces) AuthCardMotion.SetHovered(card, false);
        expect(AuthCardMotion.ActiveCount == 0, "Reduced-motion hover exit disposes all overlays immediately");
        clock.SetReducedMotion(false);
        stats.AnimateFromZero();
        AuthCardMotion.Reveal(first);
        await Task.Delay(180);
        var displayed = ((DashboardStatCard)first).DisplayedCount;
        expect(displayed > 0 && displayed < ((DashboardStatCard)first).ActualCount,
            "Real count-up keeps updating during the long Auth entrance");
        var label = first.Controls.OfType<AntdUI.Label>().Single(c => c.Font.Size > 15);
        using (var capture = UiCapture.Client(form)) { }
        int countCache = AuthCardMotion.State(first).CacheBuilds;
        await Task.Delay(240);
        using (var capture = UiCapture.Client(form)) { }
        expect(AuthCardMotion.State(first).CacheBuilds > countCache && label.Text != displayed.ToString(),
            "Counter changes invalidate the entrance foreground instead of freezing a snapshot");
        await Task.Delay(1050);
        expect(stats.DisplayedCounts.SequenceEqual(stats.ActualCounts), "Long entrance settles at all final data counts");
        var faculty = (DashboardFaculty)cards[3];
        AuthCardMotion.SetHovered(hoverSurfaces[3], true); await Task.Delay(100);
        var changedData = form.Store.Items.Skip(1).ToArray();
        faculty.Refresh(changedData);
        var newLabel = Descendants(faculty).OfType<AntdUI.Label>().First(c => c.Text == "CNTT");
        await Task.Delay(1100);
        Enter(newLabel);
        expect(AuthCardMotion.State((AntdUI.Panel)newLabel.Parent!).Running && AuthCardMotion.State(faculty).Fill == 0,
            "Faculty rows created by refresh inherit independent tile hover while the parent stays white");
        faculty.Refresh(form.Store.Items);
        expect(!hoverSurfaces[3].IsHandleCreated || hoverSurfaces[3].IsDisposed,
            "Refreshing faculty data disposes the old animated tile and its overlay");
        await Task.Delay(1100);
        AuthCardMotion.SetHovered(first, true); await Task.Delay(100);
        var oldSize = form.ClientSize;
        form.ClientSize = new(oldSize.Width - 1, oldSize.Height);
        expect(AuthCardMotion.ActiveCount == 0 && !AuthCardMotion.State(first).Running, "Resize cancels stale Auth geometry and overlay resources");
        form.ClientSize = oldSize;
        AuthCardMotion.Reveal(first);
        form.Hide();
        expect(AuthCardMotion.ActiveCount == 0 && !AuthCardMotion.State(first).Running, "Hiding the owner cancels Auth motion even through AntdUI containers");
        form.Show(); await Task.Delay(100);
        // Showing the test form can legitimately hover a dashboard card under the real cursor.
        int overlaysBeforeTemporary = AuthCardMotion.ActiveCount;
        using (var temporary = new AntdUI.Panel { Back = Color.White, Bounds = new(20, 40, 180, 80), Radius = 16 })
        {
            form.Controls.Add(temporary); AuthCardMotion.Bind(temporary, AppTheme.Lilac); AuthCardMotion.SetHovered(temporary, true);
            expect(AuthCardMotion.ActiveCount == overlaysBeforeTemporary + 1, "Temporary Auth surface owns one native overlay");
            temporary.Dispose();
            expect(AuthCardMotion.ActiveCount == overlaysBeforeTemporary, "Disposing a surface releases Auth overlay, bitmaps, jobs and hooks");
        }
        await CaptureMotion(form, cards, folder);
        expect(form.ResultCount == 12 && stats.ActualCounts.SequenceEqual(new[] { 12, 4, 27 }), "Auth effects leave all dashboard data and filtering unchanged");
    }
    private static void Enter(Control child) => typeof(Control).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(child, [EventArgs.Empty]);
    private static Rectangle InkExtent(Bitmap image, Rectangle area)
    {
        int left = area.Right, top = area.Bottom, right = area.Left - 1, bottom = area.Top - 1;
        for (int y = area.Top; y < area.Bottom; y++)
        for (int x = area.Left; x < area.Right; x++)
        {
            var pixel = image.GetPixel(x, y);
            if (pixel.R >= 160 || pixel.G >= 160 || pixel.B >= 160) continue;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
    private static void CaptureRow(MainForm form, AntdUI.Panel[] cards, string folder, string name)
    {
        using var row = Row(form, cards); row.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
    }
    private static Bitmap Row(MainForm form, AntdUI.Panel[] cards)
    {
        using var full = UiCapture.Client(form);
        var rectangle = cards.Select(c => new Rectangle(form.PointToClient(c.PointToScreen(Point.Empty)), c.Size)).Aggregate(Rectangle.Union);
        rectangle.Inflate(AppTheme.S(12), AppTheme.S(30));
        rectangle.Intersect(new(Point.Empty, full.Size));
        return full.Clone(rectangle, PixelFormat.Format32bppArgb);
    }
    private static async Task CaptureMotion(MainForm form, AntdUI.Panel[] cards, string folder)
    {
        var hoverSurfaces = cards.Take(3).Concat(Descendants(cards[3]).OfType<AntdUI.Panel>()
            .Where(panel => panel.Controls.OfType<AnimatedDashboardIcon>().Any())).ToArray();
        string frames = Path.Combine(folder, "auth-card-motion-frames"); Directory.CreateDirectory(frames);
        var timings = new List<long>(); var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 60; i++)
        {
            if (i == 0) foreach (var card in cards) AuthCardMotion.Reveal(card);
            if (i == 22) foreach (var card in hoverSurfaces) AuthCardMotion.SetHovered(card, true);
            if (i == 42) foreach (var card in hoverSurfaces) AuthCardMotion.SetHovered(card, false);
            timings.Add(watch.ElapsedMilliseconds);
            using var frame = Row(form, cards); frame.Save(Path.Combine(frames, $"frame-{i:00}.png"), ImageFormat.Png);
            await Task.Delay(45);
        }
        File.WriteAllText(Path.Combine(frames, "timings.json"), JsonSerializer.Serialize(timings));
        foreach (var card in hoverSurfaces) AuthCardMotion.SetHovered(card, false);
        await Task.Delay(1100);
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
