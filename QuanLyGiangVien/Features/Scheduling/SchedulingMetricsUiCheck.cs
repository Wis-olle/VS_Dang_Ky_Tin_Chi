using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal static class SchedulingMetricsUiCheck
{
    internal static void CheckData(SchedulingPage page, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        page.SetSemesterForCheck(0);
        var summary = SchedulingQuery.Summary(page.Store, page.Store.Catalog.Semesters[0].Id);
        int active = page.Store.Classes.Count(item => item.SemesterId == page.Store.Catalog.Semesters[0].Id && item.Status != SchedulingLabels.Canceled);
        float Fraction(int part, int total) => total > 0 ? (float)part / total : 0;
        var expected = new[] { Fraction(summary.OpenClasses, summary.TotalClasses), Fraction(summary.AvailableRooms, summary.TotalRooms),
            Fraction(active - summary.UnscheduledClasses, active), Fraction(summary.Enrolled, summary.Capacity) };
        Expect(page.StatsForCheck.Cast<SchedulingStat>().Select(stat => stat.RatioForCheck).Zip(expected)
            .All(pair => Math.Abs(pair.First - pair.Second) < .00001f), "All four KPI visuals reflect actual open, ready, scheduled and enrolled ratios");
        Expect(page.StatsForCheck.Cast<SchedulingStat>().Zip(expected).All(pair =>
            pair.First.AccessibleDescription?.Contains($"{Math.Round(pair.Second * 100)}%") == true), "KPI accessibility describes the same percentage shown by every ratio visual");
        var sessions = SchedulingQuery.Occurrences(page.Store, page.WeekForCheck, page.WeekForCheck.AddDays(6))
            .Where(item => item.Class.SemesterId == page.Store.Catalog.Semesters[0].Id).ToArray();
        Expect(page.AnalyticsForCheck.CountsForCheck.SequenceEqual(Enumerable.Range(0, 7)
            .Select(day => sessions.Count(item => item.Date == page.WeekForCheck.AddDays(day)))), "Weekly chart contains the seven actual daily session totals");
    }

    internal static async Task Run(MainForm form, SchedulingPage page, string folder, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        page.SetSemesterForCheck(0); page.SwitchTab(0); page.ScrollToTopForCheck();
        page.RefreshData(false);
        form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
        await Task.Delay(250);
        Expect(page.StatsForCheck.Cast<SchedulingStat>().All(stat => stat.ContentFitsForCheck), "Wide KPI cards display full headings, numbers and action links");
        SaveMetrics(page, folder, "20-metrics-full");
        SaveKpi(page, folder, "24-kpi-full");
        var sessions = SchedulingQuery.Occurrences(page.Store, page.WeekForCheck, page.WeekForCheck.AddDays(6))
            .Where(item => item.Class.SemesterId == page.Store.Catalog.Semesters[0].Id).ToArray();
        using (var chart = new ScheduleAnalytics { Size = new(AppTheme.S(304), AppTheme.S(108)) })
        {
            form.Controls.Add(chart);
            chart.SetData(sessions, page.WeekForCheck, false);
            using (var bitmap = UiCapture.Client(chart)) bitmap.Save(Path.Combine(folder, "22-week-chart.png"), ImageFormat.Png);
            var bars = chart.BarBoundsForCheck;
            Expect(chart.CountsForCheck.SequenceEqual(new[] { 2, 2, 2, 1, 1, 0, 0 }), "Demo week keeps its real 2/2/2/1/1/0/0 distribution");
            Expect(bars.Max(rect => rect.Height) >= AppTheme.S(32), "Two-session days use readable chart height instead of the old minimum-four scale");
            Expect(chart.ZeroHasNoFillForCheck && bars[5].Height == 0 && bars[6].Height == 0, "Zero-session days have a track and no colored data fill");
            Expect(Math.Abs(bars[0].Height - bars[3].Height * 2) < .1f, "Bar heights preserve the actual two-to-one daily ratio");
            chart.SetData(sessions, page.WeekForCheck, true);
            Expect(!chart.MotionRunning, "Refreshing identical weekly data does not restart chart motion");
            chart.NavigateForCheck(Keys.Home);
            Expect(chart.ActiveDayForCheck == 0, "Chart Home key focuses Monday");
            chart.NavigateForCheck(Keys.Right);
            Expect(chart.ActiveDayForCheck == 1, "Chart Right key focuses the next day");
            chart.NavigateForCheck(Keys.End);
            Expect(chart.ActiveDayForCheck == 6, "Chart End key focuses Sunday including a zero-session day");
            chart.SetActiveDayForCheck(-1);
            Expect(chart.ActiveDayForCheck == -1, "Leaving the chart clears its active day");
            chart.SetData([], page.WeekForCheck, false);
            var start = chart.BarBoundsForCheck.ToArray();
            chart.SetData(sessions, page.WeekForCheck, true);
            Expect(chart.MotionRunning, "Changing chart data starts a finite shared-clock transition");
            await Task.Delay(100);
            var middle = chart.BarBoundsForCheck.ToArray();
            Expect(middle[0].Height > start[0].Height && middle[0].Height < bars[0].Height, "Chart bars interpolate from their previous painted heights");
            chart.SetData([], page.WeekForCheck, true);
            Expect(Math.Abs(chart.BarBoundsForCheck[0].Height - middle[0].Height) < .01f, "Rapid week reversal starts from the current painted chart value");
            await Task.Delay(600);
            Expect(!chart.MotionRunning && chart.BarBoundsForCheck.All(rect => rect.Height == 0), "Empty week settles with seven zero fills and no running motion");
            chart.SetData(sessions, page.WeekForCheck, true); chart.Visible = false;
            Expect(!chart.MotionRunning, "Hiding the chart stops its finite transition");
            chart.SetData([], page.WeekForCheck, true);
            Expect(!chart.MotionRunning && chart.BarBoundsForCheck.All(rect => rect.Height == 0), "Refreshing an already hidden chart applies data without creating a motion job");
            chart.Visible = true;
            AntdUI.Config.Animation = false;
            chart.SetData([], page.WeekForCheck, true);
            Expect(!chart.MotionRunning && chart.BarBoundsForCheck.All(rect => rect.Height == 0), "Reduced motion immediately applies empty weekly data");
            AntdUI.Config.Animation = true;
            var chartFonts = chart.OwnedFontsForCheck;
            chart.Dispose();
            Expect(chartFonts.All(IsDisposed), "Disposing the chart releases all three cached GDI fonts");
        }
        using (var stat = new SchedulingStat("Tỷ lệ đăng ký", "users-round", SchedulingTheme.Rose)
            { Size = new(AppTheme.S(244), AppTheme.S(180)) })
        {
            form.Controls.Add(stat);
            stat.SetValue(25, "25 / 100 chỗ", true, false); stat.SetRatio(.25f, false);
            var artworkBounds = stat.ArtworkForCheck.Bounds;
            stat.SetRatio(.75f);
            Expect(stat.RatioStateForCheck.Running, "KPI ratio starts a finite transition between real values");
            await Task.Delay(100);
            var middle = stat.RatioStateForCheck.Displayed;
            Expect(middle > .25f && middle < .75f, "KPI ratio interpolates smoothly between its previous and target values");
            stat.SetRatio(.25f);
            Expect(stat.RatioStateForCheck.Displayed == middle, "Reversing KPI ratio preserves its current painted value");
            await Task.Delay(750);
            Expect(!stat.RatioStateForCheck.Running && stat.RatioStateForCheck.Displayed == .25f, "KPI ratio reaches its target and stops");
            Expect(stat.ArtworkForCheck.Bounds == artworkBounds && stat.NumberTextFitsForCheck, "Ratio animation preserves artwork bounds and native number fit");
            stat.SetRatio(.75f); stat.Visible = false;
            Expect(!stat.RatioStateForCheck.Running && stat.RatioStateForCheck.Displayed == .75f, "Hiding a KPI settles its ratio and removes the clock job");
            stat.Visible = true;
            AntdUI.Config.Animation = false;
            stat.SetRatio(float.NaN);
            Expect(stat.RatioStateForCheck == (0f, 0f, false), "Invalid ratio is sanitized and reduced motion paints its final value immediately");
            AntdUI.Config.Animation = true;
            _ = stat.NumberTextFitsForCheck;
            var fonts = stat.OwnedFontsForCheck;
            stat.Dispose();
            Expect(fonts.All(IsDisposed), "Disposing a KPI releases every cached native text and action font");
        }
        foreach (var number in new[] { 0, 99, 1234, 9999 })
        {
            using var stat = new SchedulingStat("Tổng lớp học phần", "book-open", SchedulingTheme.Cyan)
                { Size = new(AppTheme.S(244), AppTheme.S(180)) };
            form.Controls.Add(stat);
            stat.SetValue(number, "6 lớp đang mở", animate: false); stat.SetRatio(.75f, false);
            Expect(stat.ContentFitsForCheck, "Compact KPI keeps full text with count " + number);
        }
        form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
        await Task.Delay(250);
        Expect(page.StatsForCheck.Cast<SchedulingStat>().All(stat => stat.NumberTextFitsForCheck), "Compact KPI tiles keep complete native numbers and separate text rows");
        Expect(page.StatsForCheck.Cast<SchedulingStat>().All(stat => stat.ContentFitsForCheck), "Compact KPI cards retain full headings, numbers and links without overlapping their artwork");
        SaveMetrics(page, folder, "21-metrics-compact");
        SaveKpi(page, folder, "25-kpi-compact");
        form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
        await CaptureMotion(page, sessions, folder);
        await Task.Delay(1300);
        Expect(PageMotionCoordinator.Current?.ActiveCount == 0, "Metrics interactions finish without ambient animation jobs");
    }

    private static bool IsDisposed(Font font)
    {
        try { _ = font.GetHeight(96f); return false; }
        catch (Exception exception) when (exception is ArgumentException or ObjectDisposedException) { return true; }
    }
    private static void SaveMetrics(SchedulingPage page, string folder, string name)
    {
        using var bitmap = UiCapture.Client(page.MetricsHostForCheck);
        using var crop = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, Math.Min(bitmap.Height, page.KpiHostForCheck.Height)), PixelFormat.Format32bppArgb);
        crop.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
    }

    private static void SaveKpi(SchedulingPage page, string folder, string name)
    {
        using var bitmap = UiCapture.Client(page.KpiHostForCheck);
        bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
    }

    private static async Task CaptureMotion(SchedulingPage page, IReadOnlyList<ScheduledOccurrence> sessions, string folder)
    {
        string frames = Path.Combine(folder, "metrics-motion-frames");
        string kpiFrames = Path.Combine(folder, "kpi-motion-frames");
        Directory.CreateDirectory(frames);
        Directory.CreateDirectory(kpiFrames);
        page.AnalyticsForCheck.SetData([], page.WeekForCheck, false);
        foreach (var stat in page.StatsForCheck.Cast<SchedulingStat>()) stat.RevealValue();
        page.AnalyticsForCheck.SetData(sessions, page.WeekForCheck, true);
        var clock = Stopwatch.StartNew();
        var timestamps = new List<long>();
        for (int index = 0; index < 28; index++)
        {
            if (index == 4) page.StatsForCheck.Cast<SchedulingStat>().First().AnimateHover(true);
            if (index == 9) page.AnalyticsForCheck.SetActiveDayForCheck(1);
            if (index == 18) { page.StatsForCheck.Cast<SchedulingStat>().First().AnimateHover(false); page.AnalyticsForCheck.SetActiveDayForCheck(-1); }
            timestamps.Add(clock.ElapsedMilliseconds);
            SaveMetrics(page, frames, $"frame-{index:00}");
            SaveKpi(page, kpiFrames, $"frame-{index:00}");
            await Task.Delay(45);
        }
        File.WriteAllText(Path.Combine(frames, "timings.json"), JsonSerializer.Serialize(timestamps));
        File.WriteAllText(Path.Combine(kpiFrames, "timings.json"), JsonSerializer.Serialize(timestamps));
        File.WriteAllText(Path.Combine(folder, "metrics-state.json"), JsonSerializer.Serialize(
            page.StatsForCheck.Cast<SchedulingStat>().Select(stat => stat.VisualStateForCheck()), new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed partial class SchedulingPage
{
    internal ScheduleAnalytics AnalyticsForCheck => analytics;
    internal Control MetricsHostForCheck => content;
    internal Control KpiHostForCheck => statsCard;
}
