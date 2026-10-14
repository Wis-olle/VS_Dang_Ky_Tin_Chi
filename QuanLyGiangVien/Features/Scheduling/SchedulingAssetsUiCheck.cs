using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal static class SchedulingAssetsUiCheck
{
    internal static async Task Run(MainForm form, SchedulingPage page, string folder, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        Expect(page.ArtworkReadyForCheck, "Locally packaged empty-state illustration loads without network access");
        for (int index = 0; index < page.ArtworkForCheck.Count; index++)
        {
            using var bitmap = UiCapture.Client(page.ArtworkForCheck[index]);
            bitmap.Save(Path.Combine(folder, $"17-artwork-{index}.png"), ImageFormat.Png);
            bool hasInk = Enumerable.Range(0, bitmap.Width).Any(x => Enumerable.Range(0, bitmap.Height)
                .Any(y => bitmap.GetPixel(x, y) is var pixel && pixel.A > 0 && Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) < 240));
            Expect(hasInk, "Illustration raster contains visible painted artwork: " + index);
        }
        Expect(page.StatsForCheck.Cast<SchedulingStat>().All(stat => {
            using var state = JsonDocument.Parse(JsonSerializer.Serialize(stat.VisualStateForCheck()));
            return !stat.Controls.OfType<SchedulingIcon>().Any() && state.RootElement.GetProperty("Note").GetProperty("IsEmpty").GetBoolean()
                && state.RootElement.GetProperty("Caption").GetProperty("Left").GetInt32() == state.RootElement.GetProperty("Number").GetProperty("Left").GetInt32();
        }), "All four statistic cards omit icons and status lines, with headings aligned to their numbers");
        using (var tabs = new SchedulingTabs { Size = new(AppTheme.S(380), AppTheme.S(42)) })
        {
            form.Controls.Add(tabs);
            var buttonBounds = tabs.Controls.Cast<Control>().Select(button => button.Bounds).ToArray();
            var initial = tabs.UnderlineRectForCheck();
            tabs.SelectIndex = 2;
            Expect(tabs.UnderlineMotionRunningForCheck(), "Tab underline starts a finite native animation");
            await Task.Delay(70);
            var middle = tabs.UnderlineRectForCheck();
            Expect(middle.X > initial.X && middle.X < tabs.UnderlineTargetForCheck().X,
                "Tab underline interpolates its position rather than jumping between labels");
            tabs.SelectIndex = 0;
            Expect(tabs.UnderlineRectForCheck() == middle, "Rapid tab reversal starts from the currently painted indicator");
            await Task.Delay(300);
            Expect(!tabs.UnderlineMotionRunningForCheck() && tabs.UnderlineRectForCheck() == tabs.UnderlineTargetForCheck(),
                "Tab underline reaches its final measured label width and stops");
            Expect(buttonBounds.SequenceEqual(tabs.Controls.Cast<Control>().Select(button => button.Bounds)),
                "Sliding underline keeps all native tab hitboxes fixed");
            Expect(tabs.RovingTabStopForCheck(), "Only the selected workspace tab is a native Tab stop");
            AntdUI.Config.Animation = false;
            tabs.SelectIndex = 1;
            Expect(!tabs.UnderlineMotionRunningForCheck() && tabs.UnderlineRectForCheck() == tabs.UnderlineTargetForCheck(),
                "Reduced motion settles tab selection immediately");
            AntdUI.Config.Animation = true;
            tabs.SelectIndex = 2; tabs.Visible = false;
            Expect(!tabs.UnderlineMotionRunningForCheck(), "Hiding the tab strip cancels its indicator animation");
        }
        using (var detail = new SchedulingDetail { Size = new(AppTheme.S(320), AppTheme.S(466)) })
        {
            form.Controls.Add(detail);
            var first = page.Store.Classes.First(c => c.Capacity > 0);
            var other = page.Store.Classes.First(c => c.Capacity > 0 && (float)c.Enrolled / c.Capacity != (float)first.Enrolled / first.Capacity);
            detail.ShowClass(first, page.Store, false);
            var initial = detail.ProgressForCheck;
            detail.ShowClass(other, page.Store, true);
            Expect(detail.ProgressForCheck.Running, "Seat fill animates when selecting a class with a different occupancy");
            await Task.Delay(90);
            var middle = detail.ProgressForCheck;
            Expect(middle.Displayed > Math.Min(initial.Target, middle.Target) && middle.Displayed < Math.Max(initial.Target, middle.Target),
                "Seat fill interpolates between real occupancy values");
            detail.ShowClass(first, page.Store, true);
            Expect(detail.ProgressForCheck.Displayed == middle.Displayed, "Reversing seat fill preserves its current painted value");
            await Task.Delay(400);
            Expect(!detail.ProgressForCheck.Running && detail.ProgressForCheck.Displayed == detail.ProgressForCheck.Target,
                "Seat fill reaches its target and stops the shared motion job");
            detail.ShowClass(other, page.Store, true); detail.Visible = false;
            Expect(!detail.ProgressForCheck.Running, "Hiding the inspector stops its seat fill animation");
        }
        await CaptureInteractions(form, page, folder);
        await Task.Delay(1000);
        Expect(PageMotionCoordinator.Current?.ActiveCount == 0, "Asset interactions leave no ambient motion jobs running");
    }

    private static async Task CaptureInteractions(MainForm form, SchedulingPage page, string folder)
    {
        string frames = Path.Combine(folder, "interaction-motion-frames");
        Directory.CreateDirectory(frames);
        page.SwitchTab(0); page.ScrollToTopForCheck();
        var clock = Stopwatch.StartNew();
        var timestamps = new List<long>();
        for (int index = 0; index < 28; index++)
        {
            if (index == 2) page.SwitchTab(1);
            if (index == 9) page.SwitchTab(2);
            if (index == 16) page.SwitchTab(0);
            timestamps.Add(clock.ElapsedMilliseconds);
            using var bitmap = UiCapture.Client(form);
            bitmap.Save(Path.Combine(frames, $"frame-{index:00}.png"), ImageFormat.Png);
            await Task.Delay(45);
        }
        File.WriteAllText(Path.Combine(frames, "timings.json"), JsonSerializer.Serialize(timestamps));
    }
}
