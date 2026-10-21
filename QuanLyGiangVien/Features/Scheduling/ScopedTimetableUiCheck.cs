using System.Collections.Immutable;
using System.Drawing.Imaging;
using System.Text.Json;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Opt-in native scope checks. Uses in-memory demo data only and never writes SQL.</summary>
internal static class ScopedTimetableUiCheck
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    internal static void Attach(MainForm owner, string? outputFolder = null)
    {
        var folder = outputFolder ?? Environment.GetEnvironmentVariable("SCOPED_TIMETABLE_UI_CHECK");
        if (string.IsNullOrWhiteSpace(folder)) return;
        owner.Shown += async (_, _) => {
            var checks = new List<string>();
            Directory.CreateDirectory(folder);
            try
            {
                if (!Environment.GetCommandLineArgs().Contains("--demo"))
                    throw new InvalidOperationException("Scoped timetable native verification requires explicit --demo.");
                owner.Location = new(SystemInformation.VirtualScreen.Right + 100, SystemInformation.VirtualScreen.Top);
                var page = owner.ShowScheduling();
                page.SwitchTab(2);
                await Task.Delay(160);
                Expect(page.VisibleCalendarSessionsForCheck.Count == 0,
                    "A timetable without a selected subject starts empty instead of showing the whole school", checks);
                await CheckScopes(owner, folder, checks);
                await CheckBoundedOverlap(owner, folder, checks);
                await CheckAsyncDirectory(owner, checks);
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new {
                    passed = true, method = "Offscreen native WinForms; in-memory fixture; no SQL writes or physical input",
                    dpi = owner.DeviceDpi, checks
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(folder, "error.txt"), error.ToString());
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
                Environment.ExitCode = 1;
            }
            finally { owner.Close(); }
        };
    }

    private static async Task CheckScopes(MainForm owner, string folder, List<string> checks)
    {
        var store = CreateFixture();
        var directory = CreateDirectory();
        using var host = Window(owner, new(1440, 900));
        using var page = new SchedulingPage(owner, store, directory) { Dock = DockStyle.Fill };
        host.Controls.Add(page); host.Show(owner);
        page.RestoreView(new(2, 0, "", null, null, Monday, null, null, Point.Empty));
        await Settle();
        Expect(page.VisibleCalendarSessionsForCheck.Count == 0,
            "An isolated page starts empty until one subject is explicitly selected", checks);
        await Shot(host, folder, "01-select-subject-wide");

        await page.SetScopeForCheckAsync(TimetableScopeKind.Lecturer, "T01");
        ExpectIds(page, ["PAR01"], "Lecturer timetable excludes every other lecturer", checks);
        await page.SetScopeForCheckAsync(TimetableScopeKind.Lecturer, "T11");
        ExpectIds(page, ["ACTUAL"], "Replacement lecturer sees the actual dated meeting", checks);
        Expect(page.VisibleCalendarSessionsForCheck[0].Date == Monday.AddDays(2)
            && page.VisibleCalendarSessionsForCheck[0].RoomId == "R11",
            "Replacement timetable preserves the actual date and room", checks);
        await Shot(host, folder, "02-lecturer-actual-wide");

        await page.SetScopeForCheckAsync(TimetableScopeKind.Student, "SV01");
        ExpectIds(page, ["ACTUAL", "PAR01"], "Student sees active registrations only; canceled registration stays excluded", checks);
        await Shot(host, folder, "03-student-wide");
        await page.SetScopeForCheckAsync(TimetableScopeKind.Student, "SV04");
        ExpectIds(page, [], "A student without active registrations has an empty timetable", checks);
        await page.SetScopeForCheckAsync(TimetableScopeKind.CourseClass, "PAR02");
        ExpectIds(page, ["PAR02"], "Course-class timetable contains only the selected course class", checks);
        await Shot(host, folder, "04-course-class-wide");

        await page.SetScopeForCheckAsync(TimetableScopeKind.StudentClass, "COHORT_A");
        ExpectIds(page, Enumerable.Range(1, 10).Select(i => $"PAR{i:00}").Append("ACTUAL"),
            "Student-class view is the deduplicated union of members' active registrations", checks);
        Expect(page.CalendarForCheck.RenderedBlockCountForCheck == 2,
            "Student-class union renders ten Monday classes as one block and the Wednesday lesson separately", checks);
        await Shot(host, folder, "05-student-class-wide");
        var saved = page.CaptureView();
        await page.SetScopeForCheckAsync(TimetableScopeKind.CourseClass, "PAR01");
        page.SwitchTab(0); page.RestoreView(saved);
        Expect(page.CaptureView().Scope == TimetableScopeKind.StudentClass
            && page.CaptureView().SubjectId == "COHORT_A" && page.CaptureView().Week == Monday
            && page.CaptureView().Tab == 2 && page.VisibleCalendarSessionsForCheck.Count == 11,
            "Capture/restore preserves scope, subject ID, week, tab and union results", checks);
        page.RefreshData(false);
        Expect(page.CaptureView().SubjectId == "COHORT_A" && page.VisibleCalendarSessionsForCheck.Count == 11,
            "Refreshing the selected subject does not reset to a whole-school timetable", checks);
        host.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
        await Settle();
        Expect(page.CalendarFiltersDoNotOverlapForCheck,
            "1100×720 selectors and week navigation have separate accessible bounds", checks);
        Expect(!page.HasHorizontalScrollForCheck, "Compact page keeps horizontal scrolling inside the timetable", checks);
        page.ScrollToBottomForCheck();
        await Shot(host, folder, "06-student-class-compact");

        await page.SetScopeForCheckAsync(TimetableScopeKind.StudentClass, "COHORT_B");
        ExpectIds(page, ["PAR02"], "Changing the student class replaces the union rather than accumulating sessions", checks);
        await page.SetScopeForCheckAsync(TimetableScopeKind.Room, "R03");
        ExpectIds(page, ["PAR03"], "Room scope remains available for resource control", checks);
        var missing = page.CaptureView() with { Scope = TimetableScopeKind.CourseClass, SubjectId = "REMOVED_CLASS" };
        page.RestoreView(missing); page.RefreshData(false);
        Expect(page.VisibleCalendarSessionsForCheck.Count == 0,
            "A missing saved subject yields no sessions and never falls back to all classes", checks);
        await page.SetScopeForCheckAsync(TimetableScopeKind.Lecturer, null);
        Expect(page.VisibleCalendarSessionsForCheck.Count == 0,
            "Clearing the selected subject removes all previously visible sessions", checks);
        host.Close();
    }

    private static async Task CheckBoundedOverlap(MainForm owner, string folder, List<string> checks)
    {
        var store = CreateFixture();
        var sessions = SchedulingQuery.Occurrences(store, Monday, Monday.AddDays(6))
            .Where(item => item.Date == Monday).ToArray();
        Expect(sessions.Length == 10, "Stress fixture contains ten independent concurrent classes", checks);
        using var host = Window(owner, new(1100, 720));
        using var visuals = new SchedulingVisualCatalog();
        using var calendar = new WeeklySchedule { Dock = DockStyle.Fill, Visuals = visuals };
        host.Controls.Add(calendar); host.Show(owner);
        calendar.SetData(store, sessions.Take(1).ToArray(), Monday);
        await Settle();
        var singleExtent = calendar.HorizontalExtentForCheck;
        calendar.SetData(store, sessions, Monday, personalSchedule: false, caption: "Lịch tổng hợp lớp sinh viên");
        await Settle();
        Expect(calendar.OccurrenceCount == 10 && calendar.PlacementsForCheck.Count == 10,
            "Grouping preserves all ten underlying occurrences", checks);
        Expect(calendar.RenderedBlockCountForCheck == 1,
            "Ten overlapping classes render as a single summary block instead of ten narrow lanes", checks);
        Expect(calendar.PlacementsForCheck.All(item => item.Conflicts.Length == 0),
            "Independent classes in a cohort union have no false student-conflict warning", checks);
        Expect(calendar.HorizontalExtentForCheck == singleExtent,
            "Adding ten concurrent classes does not expand the timetable's horizontal extent", checks);
        Expect(calendar.CardsReadableForCheck && calendar.HeadersTrackScrollForCheck,
            "Grouped timetable keeps readable native blocks and aligned axes", checks);
        await Shot(host, folder, "07-ten-concurrent-compact");
        string? selected = null;
        calendar.ClassSelected += item => selected = item.Id;
        calendar.OpenBlockForCheck(0);
        await Settle();
        var dialog = calendar.OpenedGroupDialogForCheck;
        Expect(dialog is { IsDisposed: false, Visible: true },
            "Opening the summary shows a native detail window", checks);
        try
        {
            var buttons = Descendants(dialog!).OfType<System.Windows.Forms.Button>()
                .Where(button => button.Tag is ScheduledOccurrence).ToArray();
            Expect(buttons.Length == 10 && buttons.Select(button => ((ScheduledOccurrence)button.Tag!).Class.Id).Distinct().Count() == 10,
                "The summary window exposes every concurrent class exactly once", checks);
            Expect(buttons.All(button => button.Width >= AppTheme.S(130)
                && button.Height >= AppTheme.S(38) && !string.IsNullOrWhiteSpace(button.AccessibleName)),
                "Summary-window class buttons retain readable content", checks);
            await Shot(dialog!, folder, "08-ten-concurrent-details");
            buttons.Single(button => ((ScheduledOccurrence)button.Tag!).Class.Id == "PAR10").PerformClick();
            Expect(selected == "PAR10", "Selecting the tenth class emits the actual class rather than a representative", checks);
        }
        finally { if (dialog is { IsDisposed: false }) dialog.Close(); }
        calendar.ScrollForCheck(AppTheme.S(240), AppTheme.S(160));
        await Settle();
        Expect(calendar.HeadersTrackScrollForCheck, "Grouped timetable axes remain aligned after scrolling", checks);
        calendar.SetData(store, sessions.Take(2).ToArray(), Monday, personalSchedule: true,
            caption: "Thời khóa biểu sinh viên có dữ liệu trùng");
        await Settle();
        Expect(calendar.PlacementsForCheck.All(item => item.Conflicts.Any(reason => reason.Contains("sinh viên", StringComparison.Ordinal))),
            "Overlapping active registrations warn on both classes in an individual student timetable", checks);
        await Shot(host, folder, "09-personal-overlap-warning");
        calendar.OpenBlockForCheck(0);
        await Settle();
        var staleDialog = calendar.OpenedGroupDialogForCheck;
        calendar.SetData(store, [], Monday);
        Expect(calendar.OccurrenceCount == 0 && calendar.RenderedBlockCountForCheck == 0,
            "Clearing grouped data leaves no stale blocks or underlying occurrences", checks);
        Expect(staleDialog is { IsDisposed: true } || staleDialog is { Visible: false },
            "Refreshing a timetable closes its stale overlap window", checks);
        host.Close();
    }

    private static async Task CheckAsyncDirectory(MainForm owner, List<string> checks)
    {
        var store = CreateFixture();
        var pending = new TaskCompletionSource<TimetableDirectory>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var host = Window(owner, new(1100, 720)))
        using (var page = new SchedulingPage(owner, store, directoryLoader: _ => pending.Task) { Dock = DockStyle.Fill })
        {
            host.Controls.Add(page); host.Show(owner);
            page.RestoreView(new(2, 0, "", null, null, Monday, null, null, Point.Empty,
                Scope: TimetableScopeKind.Student, SubjectId: "SV01"));
            Expect(page.VisibleCalendarSessionsForCheck.Count == 0 && page.CaptureView().SubjectId == "SV01",
                "Restoring a pending student selection stays empty and preserves its subject ID", checks);
            await page.SetScopeForCheckAsync(TimetableScopeKind.Lecturer, "T01");
            pending.SetResult(CreateDirectory());
            await Settle();
            ExpectIds(page, ["PAR01"], "A late student directory cannot replace the newly selected lecturer timetable", checks);
            Expect(page.CaptureView().Scope == TimetableScopeKind.Lecturer && page.TimetableReloadEnabledForCheck,
                "Directory completion preserves the current scope and enables refresh", checks);
            host.Close();
        }
        int loads = 0;
        using (var host = Window(owner, new(1100, 720)))
        using (var page = new SchedulingPage(owner, store, directoryLoader: _ => ++loads == 1
            ? Task.FromException<TimetableDirectory>(new QuanLyGiangVien.Domain.PersistenceException("Lỗi kiểm chứng danh mục."))
            : Task.FromResult(CreateDirectory())) { Dock = DockStyle.Fill })
        {
            host.Controls.Add(page); host.Show(owner);
            page.RestoreView(new(2, 0, "", null, null, Monday, null, null, Point.Empty));
            await page.SetScopeForCheckAsync(TimetableScopeKind.Student, "SV01");
            Expect(page.VisibleCalendarSessionsForCheck.Count == 0 && page.TimetableReloadEnabledForCheck
                && page.TimetableCaptionForCheck.Contains("Không tải được", StringComparison.Ordinal),
                "Failed directory loading stays empty, reports the error and permits retry", checks);
            page.ReloadTimetableForCheck();
            await Settle();
            ExpectIds(page, ["ACTUAL", "PAR01"], "Retry loads the preserved student selection without a whole-school fallback", checks);
            host.Close();
        }
        int reloads = 0;
        using (var host = Window(owner, new(1100, 720)))
        using (var page = new SchedulingPage(owner, store, CreateDirectory(), reloadTimetable: () => {
            reloads++; return Task.CompletedTask;
        }) { Dock = DockStyle.Fill })
        {
            host.Controls.Add(page); host.Show(owner);
            page.RestoreView(new(2, 0, "", null, null, Monday, null, null, Point.Empty));
            page.ReloadTimetableForCheck();
            await Settle();
            Expect(reloads == 1, "Refresh delegates to the complete snapshot reload instead of mixing a fresh roster with cached registrations", checks);
            host.Close();
        }
    }

    private static ScheduleStore CreateFixture()
    {
        var catalog = TeachingCatalog.ForTimetable(
            [new Course("IT103", "Lập trình hướng đối tượng", 3, "Công nghệ thông tin")],
            [new Semester("TEST_TERM", "Học kỳ kiểm chứng", new(2026, 10, 1), new(2026, 11, 30))],
            Enumerable.Range(1, 11).Select(i => new TeachingLecturer($"T{i:00}", $"Giảng viên {i}", "Đang giảng dạy")).ToArray());
        var rooms = Enumerable.Range(1, 11).Select(i => new Room($"R{i:00}", "Tòa kiểm chứng", 60,
            SchedulingLabels.Theory, SchedulingLabels.Ready));
        var classes = Enumerable.Range(1, 10).Select(i => Lesson($"PAR{i:00}", $"R{i:00}", $"T{i:00}", Monday)).Append(
            Lesson("ACTUAL", "R01", "T01", Monday.AddDays(1)) with {
                ActualMeetings = [new ActualClassMeeting(Monday.AddDays(2), "R11", "T11", 4, 6)]
            }).Append(Lesson("CANCELED", "R01", "T01", Monday.AddDays(3)) with { Status = SchedulingLabels.Canceled })
            .Append(Lesson("NEXT_WEEK", "R01", "T01", Monday.AddDays(7)));
        return new(catalog, rooms, classes);
    }

    private static TimetableDirectory CreateDirectory() => new(
        [new("SV01", "Sinh viên một", "COHORT_A", "Công nghệ thông tin A"),
         new("SV02", "Sinh viên hai", "COHORT_A", "Công nghệ thông tin A"),
         new("SV03", "Sinh viên ba", "COHORT_B", "Công nghệ thông tin B"),
         new("SV04", "Sinh viên chưa đăng ký", "COHORT_C", "Công nghệ thông tin C")],
        [new("SV01", "PAR01"), new("SV01", "PAR02", true), new("SV01", "ACTUAL"),
         new("SV01", "CANCELED"), new("SV01", "NEXT_WEEK"), new("SV03", "PAR02"),
         .. Enumerable.Range(1, 10).Select(i => new TimetableRegistration("SV02", $"PAR{i:00}"))]);

    private static CourseClass Lesson(string id, string room, string teacher, DateOnly date)
        => new(id, "IT103", "TEST_TERM", teacher, 40, 0, SchedulingLabels.Open,
            ImmutableArray.Create(new ClassMeeting(room, date.DayOfWeek, 1, 3, date, date)));

    private static Form Window(MainForm owner, Size size) => new() {
        Text = "Kiểm chứng thời khóa biểu theo đối tượng", StartPosition = FormStartPosition.Manual,
        Location = owner.Location, ClientSize = new(AppTheme.S(size.Width), AppTheme.S(size.Height)),
        BackColor = Color.White, ShowInTaskbar = false
    };

    private static void ExpectIds(SchedulingPage page, IEnumerable<string> expected, string label, List<string> checks)
        => Expect(page.VisibleCalendarSessionsForCheck.Select(item => item.Class.Id).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal)), label, checks);

    private static IEnumerable<Control> Descendants(Control parent)
        => parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static Task Settle() => Task.Delay(160);
    private static async Task Shot(Control control, string folder, string name)
    {
        await Settle();
        using var bitmap = UiCapture.Client(control);
        bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
    }

    private static void Expect(bool condition, string label, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add(label);
    }
}
