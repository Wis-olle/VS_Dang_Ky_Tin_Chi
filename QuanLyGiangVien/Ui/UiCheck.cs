using System.Drawing.Imaging;
using System.Text.Json;
using QuanLyGiangVien.Dialogs;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in in-process UI integration check. Captures actual WinForms rendering;
/// never captures the user's desktop or interacts with unrelated apps.</summary>
public static class UiCheck
{
    public static void Attach(MainForm form)
    {
        string? folder = Environment.GetEnvironmentVariable("LECTURER_UI_CHECK");
        if (string.IsNullOrWhiteSpace(folder)) return;
        form.Shown += async (_, _) =>
        {
            var checks = new List<string>();
            try
            {
                Directory.CreateDirectory(folder);
                var previousError = Path.Combine(folder, "error.txt");
                if (File.Exists(previousError)) File.Delete(previousError);
                string? heroPhase = Environment.GetEnvironmentVariable("LECTURER_HERO_BENCHMARK");
                if (heroPhase is "before" or "after")
                {
                    await Task.Delay(1800);
                    await HeroMotionBenchmark.Run(form, folder, heroPhase);
                    return;
                }
                string? phase = Environment.GetEnvironmentVariable("LECTURER_PAINT_BENCHMARK");
                if (phase is "before" or "after")
                {
                    await Task.Delay(1600);
                    MotionPaintBenchmark.Run(form, folder, phase);
                    return;
                }
                await Run(form, folder, checks);
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(folder, "error.txt"), exception.ToString());
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
                Environment.ExitCode = 1;
            }
            finally { form.Close(); }
        };
    }
    private static async Task Run(MainForm form, string folder, List<string> checks)
    {
        void Expect(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("UI check failed: " + name);
            checks.Add(name);
        }
        async Task Shot(Control control, string name)
        {
            await Task.Delay(350);
            using var bitmap = UiCapture.Client(control);
            bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
        }
        if (Environment.GetEnvironmentVariable("LECTURER_DASHBOARD_CLICK_CHECK_ONLY") == "1")
        {
            form.Location = new(SystemInformation.VirtualScreen.Right + 40, SystemInformation.VirtualScreen.Top);
            await Task.Delay(1700);
            await DashboardClickUiCheck.Run(form, folder, Expect);
            await FacultySpreadUiCheck.Run(form, folder, Expect);
            await AuthCardUiCheck.Run(form, folder, Expect);
            return;
        }
        if (Environment.GetEnvironmentVariable("LECTURER_HERO_CHECK_ONLY") == "1")
        {
            await Task.Delay(900);
            await CheckSmoothRendering(form, Expect);
            CheckHeroEfficiency(form, Expect);
            await CheckPageMotion(form, Expect);
            await Shot(Descendants(form).OfType<DashboardHero>().Single(), "23-hero-faster");
            return;
        }
        if (Environment.GetEnvironmentVariable("LECTURER_LIST_HOVER_CHECK_ONLY") == "1"
            || Environment.GetEnvironmentVariable("LECTURER_LIST_PREVIEW_CHECK_ONLY") == "1")
        {
            form.Location = new(SystemInformation.VirtualScreen.Right + 40, SystemInformation.VirtualScreen.Top);
            await Task.Delay(1700);
            await ListHoverUiCheck.Run(form, folder, Expect);
            return;
        }
        if (Environment.GetEnvironmentVariable("LECTURER_AUTH_CARDS_CHECK_ONLY") == "1")
        {
            // Keep this disposable test window away from the user's physical pointer.
            // Rendering and event-hook checks still operate on native live controls.
            form.Location = new(SystemInformation.VirtualScreen.Right + 40, SystemInformation.VirtualScreen.Top);
            await Task.Delay(1700);
            await FacultySpreadUiCheck.Run(form, folder, Expect);
            await AuthCardUiCheck.Run(form, folder, Expect);
            await CheckPageMotion(form, Expect);
            return;
        }
        if (Environment.GetEnvironmentVariable("LECTURER_SELECT_CHECK_ONLY") == "1")
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new(SystemInformation.VirtualScreen.Right + 60, 0);
            await Task.Delay(900);
            await SelectUiCheck.Run(form, folder, Expect);
            return;
        }
        await UiCapture.Entrance(form, folder);
        await Task.Delay(600);
        using (var metricsGraphics = form.CreateGraphics())
        {
            var labels = new List<object>();
            void Collect(Control root)
            {
                if (root is AntdUI.Label) labels.Add(new { root.Text, FontSize = root.Font.Size, FontHeight = root.Font.Height,
                    Width = root.Width, Height = root.Height, FontName = root.Font.FontFamily.Name });
                foreach (Control child in root.Controls) Collect(child);
            }
            Collect(form);
            var scrolling = Descendants(form).OfType<ScrollableControl>().Select(control => new {
                Type = control.GetType().Name, control.Bounds, control.ClientSize, control.DisplayRectangle,
                control.AutoScrollPosition, control.AutoScrollMinSize, control.AutoScrollMargin, control.Padding,
                Horizontal = control.HorizontalScroll.Visible,
                Vertical = control.VerticalScroll.Visible, Children = control.Controls.Cast<Control>().Select(child => new {
                    Type = child.GetType().Name, child.Text, child.Bounds, child.Visible
                }).ToArray()
            }).ToArray();
            File.WriteAllText(Path.Combine(folder, "metrics.json"), JsonSerializer.Serialize(new { form.DeviceDpi, GraphicsDpi = metricsGraphics.DpiX, AppTheme.K, labels, scrolling }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Expect(form.ResultCount == 12, "Initial lecturer cards: 12 records");
        await AuthCardUiCheck.Run(form, folder, Expect);
        await CheckQuietOverview(form, folder, Expect);
        var listTitle = Descendants(form).OfType<AntdUI.Label>().Single(label => label.Text == "Danh sách giảng viên");
        Expect(!listTitle.Parent!.Controls.OfType<AntdUI.Tag>().Any(tag => tag.Visible),
            "Lecturer list title has no numeric count badge");
        CheckSimplifiedChrome(form, Expect);
        CheckIconOnlyView(form, folder, Expect);
        await SelectUiCheck.Run(form, folder, Expect);
        await CheckPageMotion(form, Expect);
        await CheckSmoothRendering(form, Expect);
        CheckHeroEfficiency(form, Expect);
        await CheckFacultyHover(form, folder, Expect);
        await CheckOverviewMotion(form, Expect);
        Expect(Math.Abs(AppTheme.K - form.DeviceDpi / 96f) < .01f, "Startup layout scale matches window DPI");
        Expect(Descendants(form).OfType<AntdUI.Label>().Where(label => label.Text is "campus." or "Danh sách giảng viên")
            .All(label => label.Font.Height <= label.Height), "Primary heading fonts fit their controls");
        await Shot(form, "01-cards");
        await CheckAnimatedIcons(form, Expect);
        Expect(!form.HasHorizontalScrollForCheck, "Dashboard and lecturer grid fit without horizontal scrolling");
        form.ShowProfileForCheck(1);
        await Shot(form, "02-profile");
        var detailPanel = Descendants(form).OfType<Panels.LecturerProfile>().Single();
        var detailScroll = detailPanel.Controls.OfType<System.Windows.Forms.Panel>().Single();
        detailScroll.AutoScrollPosition = new(0, AppTheme.S(310));
        await Shot(form, "11-profile-schedule");
        form.ShowProfileForCheck(0);
        form.SetTableForCheck(true);
        await Shot(form, "03-table");
        Expect(form.ResultCount == 12, "Table preserves all records");
        var laptopSize = MainForm.ClampWindowForCheck(new(1440, 900), new(1366, 728));
        Expect(laptopSize.Width <= 1366 && laptopSize.Height < 728, "Initial window fits laptop working area");
        form.OpenProfileForCheck(0);
        await Task.Delay(350);
        Expect(form.HasOpenProfileDrawer, "Table selection opens profile drawer");
        form.CloseProfileForCheck();
        await Task.Delay(350);
        Expect(!form.HasOpenProfileDrawer, "Profile drawer closes and disposes");
        form.SetFiltersForCheck("", selectedStatus: "Tạm nghỉ");
        Expect(form.ResultCount == 2, "Status filter updates visible lecturers");
        form.SetFiltersForCheck("", "Công nghệ thông tin");
        Expect(form.ResultCount == 4, "Faculty filter updates table");
        form.SetTableForCheck(false);
        await Shot(form, "04-filter");
        form.SetFiltersForCheck("dang duc huy");
        Expect(form.ResultCount == 1, "Search ignores Vietnamese diacritics and đ");
        form.SetFiltersForCheck("zzzz-no-match");
        Expect(form.ResultCount == 0, "No-results empty state");
        await Shot(form, "05-empty");
        form.SetFiltersForCheck("");
        var candidate = new Lecturer("GV0013", "Lê Minh Tuấn", new(1990, 5, 8), "Nam", "tuan.lm@campus.edu.vn",
            "0901112233", "Công nghệ thông tin", "Thạc sĩ", "Đang giảng dạy", "portrait-48", 0);
        using (var editor = new LecturerEditor(form.Store))
        {
            editor.Show(form);
            Expect(editor.Opacity < 1, "Editor enters with a native opacity transition");
            await Shot(editor, "06-add");
            Expect(editor.Opacity == 1, "Editor entrance settles without changing input bounds");
            Expect(editor.Controls.Cast<Control>().SelectMany(control => control.Controls.Cast<Control>()).OfType<AntdUI.Label>()
                .Where(label => label.Text == "Thêm giảng viên").All(label => label.Font.Height <= label.Height), "Editor title font fits after native window creation");
            editor.FillForTest(candidate with { Name = "", Email = "invalid" });
            Expect(!editor.TrySave(), "Editor blocks invalid fields and preserves store");
            await Shot(editor, "07-errors");
            editor.FillForTest(candidate);
            Expect(editor.TrySave(), "Editor creates valid lecturer");
            Expect(form.Store.Items.Count == 13, "Create updates immutable repository snapshot");
            editor.Close();
        }
        using (var editor = new LecturerEditor(form.Store, candidate))
        {
            editor.Show(form);
            editor.FillForTest(candidate with { Name = "Lê Minh Tuấn Anh" });
            await Shot(editor, "08-edit");
            Expect(editor.TrySave(), "Editor updates existing lecturer");
            Expect(form.Store.Items.Single(x => x.Id == candidate.Id).Name == "Lê Minh Tuấn Anh", "Updated profile persisted in session");
            editor.Close();
        }
        Expect(!form.Store.TryDelete("GV0001", out _), "Assigned lecturer deletion is protected");
        Expect(form.Store.TryDelete(candidate.Id, out _), "Unassigned lecturer can be removed");
        form.SetFiltersForCheck("");
        form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
        await Shot(form, "09-compact-cards");
        Expect(form.DashboardViewportFitsForCheck, "Compact dashboard stays within the window viewport");
        Expect(form.DashboardScrollsForCheck, "Compact dashboard scrolls to keep cards readable");
        form.ScrollDashboardForCheck();
        await Shot(form, "13-compact-scrolled");
        form.SetTableForCheck(true);
        await Shot(form, "10-compact-table");
        Expect(form.ResultCount == 12, "Compact layout preserves records");
        Expect(!form.HasHorizontalScrollForCheck, "Compact dashboard fits without horizontal scrolling");
        form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
        form.SetTableForCheck(false);
        await Task.Delay(150);
        Expect(form.DashboardAlignedForCheck, "Dashboard retains correct origin after scroll and resize");
    }
    private static async Task CheckAnimatedIcons(MainForm form, Action<bool, string> expect)
    {
        var icons = Descendants(form).OfType<AnimatedDashboardIcon>().ToArray();
        expect(icons.Length == 7 && icons.All(icon => icon.HasAnimation), "Seven dashboard icons load local animation sprites");
        expect(icons.All(icon => !icon.IsPlaying && !icon.TimerIsRunning), "Animated icons stay still with no idle timers");
        bool animation = AntdUI.Config.Animation;
        try
        {
            AntdUI.Config.Animation = true;
            foreach (var icon in icons) icon.Play();
            expect(icons.All(icon => icon.IsPlaying && icon.TimerIsRunning), "Hover playback starts one timer per icon");
            await Task.Delay(150);
            expect(icons.All(icon => icon.FrameIndex > 0), "Native timers advance the source animation frames");
            AntdUI.Config.Animation = false;
            await Task.Delay(80);
            expect(icons.All(icon => !icon.TimerIsRunning && !icon.IsPlaying && icon.FrameIndex == icon.IdleFrame),
                "Reduced motion stops active icon playback and restores full glyphs");
            foreach (var icon in icons) icon.Play();
            expect(icons.All(icon => !icon.TimerIsRunning && !icon.IsPlaying), "Reduced motion blocks new icon playback");
            AntdUI.Config.Animation = true;
            var fastest = icons.MinBy(icon => icon.DurationMilliseconds)!;
            fastest.Play();
            await Task.Delay(fastest.DurationMilliseconds + 100);
            expect(!fastest.IsPlaying && !fastest.TimerIsRunning && fastest.FrameIndex == fastest.IdleFrame,
                "One-shot icon playback finishes and releases its timer");
            fastest.Play();
            fastest.Visible = false;
            expect(!fastest.IsPlaying && !fastest.TimerIsRunning, "Hidden animated icon immediately stops playback");
            fastest.Visible = true;
            using var temporary = new AnimatedDashboardIcon("users", "Test icon", AppTheme.Peach, "TeamOutlined");
            temporary.SetBounds(0, 0, AppTheme.S(36), AppTheme.S(36));
            form.Controls.Add(temporary);
            temporary.Play();
            expect(temporary.IsPlaying, "Temporary icon starts while visible");
            temporary.Dispose();
            expect(!temporary.IsPlaying && !temporary.TimerIsRunning, "Disposing an animated icon stops its timer");
        }
        finally
        {
            AntdUI.Config.Animation = animation;
            PageMotionCoordinator.Current?.RefreshPreference();
            foreach (var icon in icons) icon.Stop();
        }
    }
    private static async Task CheckPageMotion(MainForm form, Action<bool, string> expect)
    {
        var coordinator = PageMotionCoordinator.Current;
        expect(coordinator != null, "Whole page uses a shared native animation clock");
        var clock = coordinator!;
        var stats = Descendants(form).OfType<DashboardStats>().Single();
        var overview = Descendants(form).OfType<DashboardOverview>().Single();
        var hero = Descendants(form).OfType<DashboardHero>().Single();
        await Task.Delay(900);
        expect(stats.DisplayedCounts.SequenceEqual(stats.ActualCounts) && !stats.HasRunningMotion,
            "Entrance counters settle at the exact real statistics");
        expect(Math.Abs(overview.DisplayedFraction - overview.ActualFraction) < .001f && !overview.IsAnimating,
            "Animated teaching ring settles at its real proportion");
        expect(clock.AmbientTicks > 5 && clock.TimerIsRunning, "Hero ambient animation advances on the shared clock");
        var profile = Descendants(form).OfType<Panels.LecturerProfile>().Single();
        var before = profile.Bounds;
        SurfaceTransition.Reveal(profile, 120);
        expect(SurfaceTransition.ActiveCount == 1 && profile.Bounds == before,
            "Native fade keeps the live profile hitbox and layout fixed");
        await Task.Delay(180);
        expect(SurfaceTransition.ActiveCount == 0, "Completed fades release their snapshot overlays");
        using (var temporaryParent = new System.Windows.Forms.Panel { BackColor = Color.White, Bounds = new(0, 0, 90, 90) })
        {
            var temporaryChild = new System.Windows.Forms.Panel { BackColor = Color.White, Bounds = new(0, 0, 80, 80) };
            temporaryParent.Controls.Add(temporaryChild);
            form.Controls.Add(temporaryParent);
            temporaryParent.BringToFront();
            SurfaceTransition.Reveal(temporaryChild, 300);
            expect(SurfaceTransition.ActiveCount == 1, "Temporary surface creates a transition snapshot");
            temporaryParent.Dispose();
            expect(SurfaceTransition.ActiveCount == 0, "Disposing a parent releases snapshot registrations and bitmaps");
        }
        form.ShowProfileForCheck(1);
        form.ShowProfileForCheck(2);
        expect(Descendants(profile).OfType<AntdUI.Label>().Any(label => label.Text == form.Store.Items[2].Name),
            "Rapid profile selection keeps the newest lecturer content");
        await Task.Delay(280);
        expect(SurfaceTransition.ActiveCount == 0, "Replacing profile transitions releases superseded snapshots");
        var profileScroll = profile.Controls.OfType<System.Windows.Forms.Panel>().Single();
        profileScroll.AutoScrollPosition = new(0, 40);
        var scrollPosition = profileScroll.AutoScrollPosition;
        var childBounds = profileScroll.Controls.Cast<Control>().Select(child => child.Bounds).ToArray();
        form.ShowProfileForCheck(2);
        expect(profileScroll.AutoScrollPosition == scrollPosition && childBounds.SequenceEqual(profileScroll.Controls.Cast<Control>().Select(child => child.Bounds)),
            "Reselecting the same scrolled profile preserves child positions");
        form.ShowProfileForCheck(0);
        await Task.Delay(260);
        stats.AnimateFromZero(); overview.AnimateFromZero();
        SurfaceTransition.Reveal(profile, 300);
        clock.SetReducedMotion(true);
        expect(!clock.TimerIsRunning && clock.ActiveCount == 0 && SurfaceTransition.ActiveCount == 0,
            "Reduced motion immediately finishes jobs and stops the page clock");
        expect(stats.DisplayedCounts.SequenceEqual(stats.ActualCounts) && hero.ArtworkOffsetPixels == 0,
            "Reduced motion restores final numbers and canonical hero artwork position");
        SurfaceTransition.Reveal(profile);
        expect(SurfaceTransition.ActiveCount == 0, "Reduced motion creates no fade snapshots");
        clock.SetReducedMotion(false);
        using var job = new Motion();
        float progress = 0;
        job.Run(100, p => progress = p, 200);
        await Task.Delay(60);
        expect(progress == 0 && job.IsRunning, "Staggered jobs respect their delay");
        job.Cancel();
        expect(progress == 1 && !job.IsRunning, "Cancelling a finite job can settle its final value");
        job.Run(200, p => progress = p);
        form.Hide();
        int ticks = clock.AmbientTicks;
        float frozen = progress;
        await Task.Delay(100);
        expect(!clock.TimerIsRunning && ticks == clock.AmbientTicks && frozen == progress,
            "Hidden window pauses both ambient and finite motion");
        form.Show();
        await Task.Delay(280);
        expect(progress == 1 && !job.IsRunning && clock.TimerIsRunning,
            "Showing the window resumes and completes pending motion");
        job.Run(200, p => progress = p);
        job.Dispose();
        expect(!job.IsRunning && clock.ActiveCount == 0, "Disposing a motion job removes it from the shared clock");
        SurfaceTransition.Reveal(profile, 300);
        form.ClientSize = new(form.ClientSize.Width - 1, form.ClientSize.Height);
        expect(SurfaceTransition.ActiveCount == 0, "Resizing cancels stale snapshot transitions");
        var button = Descendants(form).OfType<MotionButton>().First(item => item.Type == AntdUI.TTypeMini.Primary && item.Visible);
        button.PlayShine();
        expect(button.IsShining, "Primary button starts its native shimmer on interaction");
        await Task.Delay(680);
        expect(!button.IsShining, "Primary button shimmer completes after one sweep");
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void CheckIconOnlyView(MainForm form, string folder, Action<bool, string> expect)
    {
        var toggle = Descendants(form).OfType<AntdUI.Segmented>().Single();
        using var bitmap = UiCapture.Client(toggle);
        bool captionFree = Enumerable.Range(AppTheme.S(8), toggle.Width - AppTheme.S(16)).All(x =>
            Enumerable.Range(toggle.Height - AppTheme.S(8), AppTheme.S(4)).All(y => {
                Color pixel = bitmap.GetPixel(x, y);
                return Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) >= 190;
            }));
        expect(captionFree, "View toggle has no visible caption row below its icons");
        bitmap.Save(Path.Combine(folder, "19-view-toggle-icons.png"), ImageFormat.Png);
    }
    private static async Task CheckQuietOverview(MainForm form, string folder, Action<bool, string> expect)
    {
        var overview = Descendants(form).OfType<DashboardOverview>().Single();
        var ring = Descendants(overview).OfType<LecturerStatusRing>().Single();
        using var first = UiCapture.Client(ring);
        await Task.Delay(200);
        using var second = UiCapture.Client(ring);
        await Task.Delay(200);
        using var third = UiCapture.Client(ring);
        bool same = Enumerable.Range(0, first.Width).All(x => Enumerable.Range(0, first.Height).All(y =>
            first.GetPixel(x, y) == second.GetPixel(x, y) && first.GetPixel(x, y) == third.GetPixel(x, y)));
        expect(same, "Settled teaching percentage stays visually still without a travelling marker");
        using var capture = UiCapture.Client(overview);
        capture.Save(Path.Combine(folder, "20-teaching-overview.png"), ImageFormat.Png);
    }
    private static void CheckSimplifiedChrome(MainForm form, Action<bool, string> expect)
    {
        var visible = Descendants(form).Where(control => control.Visible).ToArray();
        expect(!visible.Any(control =>
            control is AntdUI.PageHeader && !string.IsNullOrWhiteSpace(control.Text) ||
            control.Text?.StartsWith("HỌC KỲ", StringComparison.Ordinal) == true ||
            control.Text?.Contains("kết quả  ·", StringComparison.Ordinal) == true ||
            control.Text?.StartsWith("Dữ liệu mẫu", StringComparison.Ordinal) == true ||
            control.Text?.StartsWith("Avatar:", StringComparison.Ordinal) == true ||
            control is AntdUI.Button button && button.IconSvg == "InfoCircleOutlined"),
            "Requested title, semester, result summary, footer credits and info button are absent");
        var header = visible.OfType<AntdUI.PageHeader>().Single();
        using var bitmap = UiCapture.Client(header);
        for (int index = 0; index < 3; index++)
        {
            int left = header.Width - AppTheme.S(50) * (index + 1);
            bool hasGlyph = Enumerable.Range(left, AppTheme.S(50)).Any(x =>
                Enumerable.Range(AppTheme.S(7), header.Height - AppTheme.S(14)).Any(y => {
                    Color pixel = bitmap.GetPixel(x, y);
                    return Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) < 190;
                }));
            expect(hasGlyph, $"Window caption button {index + 1} remains visible with the title removed");
        }
    }
    private static async Task CheckSmoothRendering(MainForm form, Action<bool, string> expect)
    {
        var hero = Descendants(form).OfType<DashboardHero>().Single();
        var slot = Descendants(hero).OfType<Control>().First(item => item.AccessibleName?.StartsWith("Minh họa người", StringComparison.Ordinal) == true);
        var bounds = slot.Bounds;
        hero.ApplyAmbient(.125, true);
        expect(slot.Bounds == bounds, "Hero float does not move its native artwork control bounds");
        expect(hero.ArtworkOffset > 0 && hero.ArtworkOffset < 1, "Hero float retains fractional pixel movement between timer frames");
        hero.ApplyAmbient(1.25, true);
        expect(Math.Abs(hero.ArtworkOffset - AppTheme.S(3)) < .0001f,
            "Hero float reaches its unchanged three-pixel peak after 1.25 seconds");
        hero.ApplyAmbient(5, true);
        expect(Math.Abs(hero.ArtworkOffset) < .0001f,
            "Hero float completes its faster five-second cycle at the canonical position");
        var positions = new HashSet<int>();
        using var image = new Bitmap(hero.Width, hero.Height);
        hero.DrawToBitmap(image, hero.ClientRectangle);
        int artBuilds = hero.ArtworkCacheBuilds;
        for (int frame = 0; frame < 24; frame++)
        {
            hero.ApplyAmbient(frame / 60d, true);
            hero.DrawToBitmap(image, hero.ClientRectangle);
            positions.Add(slot.Top);
        }
        expect(positions.Count == 1, "Ambient repaint retains a fixed artwork slot across frames");
        expect(hero.ArtworkCacheBuilds == artBuilds, "Ambient animation reuses the scaled artwork raster");
        var card = Descendants(form).OfType<LecturerCard>().First(item => item.Visible);
        var cardBounds = card.Bounds;
        using var cardImage = new Bitmap(card.Width, card.Height);
        card.DrawToBitmap(cardImage, card.ClientRectangle);
        int contentBuilds = card.ContentCacheBuilds;
        var liftPositions = new HashSet<float>();
        for (int frame = 0; frame < 24; frame++)
        {
            card.SetHoverProgressForCheck(frame / 23f); card.DrawToBitmap(cardImage, card.ClientRectangle);
            liftPositions.Add(card.RenderTop);
        }
        expect(card.Bounds == cardBounds, "Hover rendering preserves the lecturer card hitbox");
        expect(liftPositions.Count == 24, "Hover renders all fractional lift positions without integer stepping");
        expect(card.ContentCacheBuilds == contentBuilds, "Hover frames reuse cached text, avatar and metadata");
        using (var dpiImage = new Bitmap(card.Width, card.Height))
        {
            dpiImage.SetResolution(144, 144);
            using var dpiGraphics = Graphics.FromImage(dpiImage);
            card.PaintForCheck(dpiGraphics);
            expect(Math.Abs(card.CachedContentDpiX - dpiGraphics.DpiX) < .01f,
                "Lecturer text raster uses the target paint DPI");
        }
        card.Width += 1;
        using (var resized = new Bitmap(card.Width, card.Height)) card.DrawToBitmap(resized, card.ClientRectangle);
        expect(card.ContentCacheBuilds > contentBuilds, "Resizing rebuilds the lecturer raster cache");
        card.Bounds = cardBounds;
        card.SetHoverProgressForCheck(0);
        card.AnimateHoverForCheck(true);
        await Task.Delay(65);
        expect(card.HoverProgress > 0 && card.HoverProgress < 1, "Native hover timer interpolates the cached card");
        card.AnimateHoverForCheck(false);
        await Task.Delay(220);
        expect(card.HoverProgress == 0, "Rapid hover reversal settles at the resting card position");
        var clock = PageMotionCoordinator.Current!;
        card.AnimateHoverForCheck(true);
        clock.SetReducedMotion(true);
        expect(card.HoverProgress == 1 && hero.ArtworkOffset == 0, "Reduced motion completes hover and resets fractional hero movement");
        card.AnimateHoverForCheck(false);
        clock.SetReducedMotion(false);
    }
    private static void CheckHeroEfficiency(MainForm form, Action<bool, string> expect)
    {
        var hero = Descendants(form).OfType<DashboardHero>().Single();
        hero.ApplyAmbient(0, false);
        int parentInvalidations = 0;
        var offsets = new HashSet<float>();
        void CountParent(object? sender, InvalidateEventArgs args) => parentInvalidations++;
        hero.Invalidated += CountParent;
        try
        {
            for (int frame = 0; frame < 120; frame++)
            {
                hero.ApplyAmbient(frame / 20d, true);
                offsets.Add(hero.ArtworkOffset);
            }
            expect(offsets.Count > 40 && offsets.Any(offset => offset > 0) && offsets.Any(offset => offset < 0),
                "Hero illustration continues smooth floating through the efficiency workload");
            expect(parentInvalidations == 0, "Floating illustration does not invalidate hero text and background");
        }
        finally { hero.Invalidated -= CountParent; hero.ApplyAmbient(0, false); }
        using var viewport = new System.Windows.Forms.Panel { ClientSize = new Size(320, 210) };
        using var localHero = new DashboardHero { Bounds = new Rectangle(0, -300, 300, 210) };
        viewport.Controls.Add(localHero);
        form.Controls.Add(viewport);
        viewport.CreateControl();
        localHero.CreateControl();
        localHero.ApplyAmbient(1, true);
        expect(localHero.ArtworkOffset == 0, "Hero outside its parent viewport does not animate");
        localHero.Location = Point.Empty;
        localHero.ApplyAmbient(1, true);
        expect(localHero.ArtworkOffset != 0, "Hero entering its parent viewport resumes floating");
        float previousOffset = localHero.ArtworkOffset;
        localHero.Visible = false;
        localHero.ApplyAmbient(2, true);
        expect(localHero.ArtworkOffset == previousOffset, "Hidden hero freezes its illustration position");
        localHero.ApplyAmbient(0, false);
        expect(localHero.ArtworkOffset == 0, "Reduced motion resets even a hidden hero to its canonical position");
    }

    private static Task CheckFacultyHover(MainForm form, string folder, Action<bool, string> expect)
        => FacultySpreadUiCheck.Run(form, folder, expect);

    private static async Task CheckOverviewMotion(MainForm form, Action<bool, string> expect)
    {
        var overview = Descendants(form).OfType<DashboardOverview>().Single();
        var clock = PageMotionCoordinator.Current!;
        float fraction = overview.ActualFraction;
        expect(overview.DisplayedFraction == fraction, "Teaching ring displays the real teaching percentage");
        overview.SetRingHoverForCheck(true);
        await Task.Delay(210);
        expect(overview.RingHoverEmphasis == 1, "Hover smoothly emphasizes the teaching ring");
        overview.SetRingHoverForCheck(false);
        await Task.Delay(210);
        expect(overview.RingHoverEmphasis == 0, "Teaching ring hover settles without changing the data arc");
        overview.SetRingHoverForCheck(true);
        await Task.Delay(210);
        clock.SetReducedMotion(true);
        expect(overview.RingHoverEmphasis == 0,
            "Reduced motion resets teaching ring hover emphasis");
        overview.Refresh([]);
        expect(overview.ActualFraction == 0 && overview.DisplayedFraction == 0, "Empty teaching data displays an exact zero ring");
        overview.Refresh(form.Store.Items);
        clock.SetReducedMotion(false);
        overview.SetRingHoverForCheck(true);
        await Task.Delay(210);
        overview.Visible = false;
        expect(overview.RingHoverEmphasis == 0, "Hidden teaching overview resets hover emphasis");
        overview.Visible = true;
        expect(overview.RingHoverEmphasis == 0, "Teaching ring remains at rest when shown again");
        overview.Refresh(form.Store.Items.Where(item => item.Status == "Tạm nghỉ").ToArray());
        await Task.Delay(80);
        overview.Refresh(form.Store.Items);
        await Task.Delay(720);
        expect(!overview.IsAnimating && Math.Abs(overview.DisplayedFraction - fraction) < .001f,
            "Teaching data changes during a sweep settle at the latest proportion");
    }
}

