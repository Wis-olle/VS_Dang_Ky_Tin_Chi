using System.Collections.Immutable;
using System.Drawing.Imaging;
using System.Text.Json;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using QuanLyGiangVien.Dialogs;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Opt-in checks on the app's own native controls; no desktop input or capture.</summary>
internal static class SchedulingUiCheck
{
    internal static void Attach(MainForm form)
    {
        string? folder = Environment.GetEnvironmentVariable("SCHEDULING_UI_CHECK");
        if (string.IsNullOrWhiteSpace(folder)) return;
        Application.ThreadException += (_, args) => {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "error.txt"), args.Exception.ToString());
            File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = false, nativePaintFailure = true }));
            Environment.Exit(1);
        };
        form.Shown += async (_, _) => {
            var checks = new List<string>();
            try
            {
                Directory.CreateDirectory(folder);
                string oldError = Path.Combine(folder, "error.txt");
                if (File.Exists(oldError)) File.Delete(oldError);
                form.Location = new(SystemInformation.VirtualScreen.Right + 60, SystemInformation.VirtualScreen.Top);
                form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
                await Task.Delay(900);
                await Run(form, folder, checks);
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, scale = AppTheme.K, checks }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(folder, "error.txt"), ex.ToString());
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
                Environment.ExitCode = 1;
            }
            finally { form.Close(); }
        };
    }

    private static async Task Run(MainForm form, string folder, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        static bool IsDisposed(Font font)
        {
            try { _ = font.GetHeight(96f); return false; }
            catch (Exception exception) when (exception is ArgumentException or ObjectDisposedException) { return true; }
        }
        static bool IsBitmapDisposed(Bitmap bitmap)
        {
            try { _ = bitmap.GetPixel(0, 0); return false; }
            catch (Exception exception) when (exception is ArgumentException or ObjectDisposedException) { return true; }
        }
        async Task Shot(Control control, string name) { await Task.Delay(260); using var bitmap = UiCapture.Client(control); bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png); }
        var page = form.ShowScheduling();
        await Task.Delay(900);
        await PhaseOneSchedulingUiCheck.Run(form, page.Store, folder, checks);
        SchedulingServiceUiCheck.CheckWholeCardClicks(form, page, checks);
        SchedulingServiceUiCheck.CheckAssets(page, checks);
        SchedulingMetricsUiCheck.CheckData(page, checks);
        Expect(page.TabIndexForCheck == 0 && page.ResultCount > 0, "Classes tab opens with current semester data");
        Expect(page.HasDistinctWorkspaceForCheck, "Scheduling uses its own workspace without the lecturer hero");
        Expect(page.WorkspaceStartsEarlyForCheck, "Workspace follows the Service Card overview with a clear gap");
        Expect(page.HeaderActionsAccessibleForCheck, "Semester and primary action stay accessible in the fixed header");
        Expect(page.PaletteMatchesLecturerForCheck, "Scheduling retains the lecturer canvas and primary palette");
        Expect(page.StatsForCheck.Cast<SchedulingStat>().All(stat => stat.NumberTextFitsForCheck), "KPI numbers fit their native text height without clipping");
        Expect(page.SidebarRemovedForCheck, "Scheduling page expands its content after removing the sidebar");
        Expect(!page.HasHorizontalScrollForCheck, "1440 layout fits without horizontal page scroll");
        Expect(page.HeadersSingleLineForCheck, "Single-line headers avoid GDI LineLimit clipping at high DPI");
        Expect(page.DetailBodyReadableForCheck, "Initial class detail reserves room for its schedule before scrolling");
        Expect(page.PreviewHasMetadataForCheck, "Weekly previews retain their date, periods and room");
        Expect(page.AgendaHasNoArtworkForCheck, "Weekly agenda omits the decorative header image");
        using (var preview = new SchedulingPreview { Text = "Lập trình hướng đối tượng\nT2 · Tiết 1–3 · A201" })
        using (var paintCheck = new WeeklySchedulePaintCheck())
        {
            form.Controls.Add(preview);
            Expect(paintCheck.ControlCornersMatchBackground(preview, folder, "29-agenda-corner-repaint"),
                "Agenda preview owner repaint clears every rounded corner");
            preview.Width += AppTheme.S(30);
            Expect(paintCheck.ControlCornersMatchBackground(preview, folder, "29-agenda-corner-resize"),
                "Resized agenda preview retains clean rounded corners");
            preview.Enabled = false;
            Expect(paintCheck.ControlCornersMatchBackground(preview, folder, "29-agenda-corner-disabled"),
                "Disabled agenda preview retains clean rounded corners");
        }
        foreach (var icon in new[] { "book-open", "door-open", "calendar-days", "users-round" })
            Expect(File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling", icon + ".svg")), "Local licensed icon: " + icon);
        await Shot(form, "01-classes");
        File.WriteAllText(Path.Combine(folder, "layout-full.json"), JsonSerializer.Serialize(page.LayoutDiagnosticsForCheck));
        page.SetSearchForCheck("no-such-class-984732");
        Expect(page.ResultCount == 0, "Unmatched class query shows empty state");
        await Shot(form, "04-empty");
        page.SetSearchForCheck("");
        page.SwitchTab(1);
        Expect(page.ResultCount == page.Store.Rooms.Count, "Rooms tab contains all demo rooms");
        await Shot(form, "02-rooms");
        page.SetSearchForCheck(page.Store.Rooms[0].Id);
        Expect(page.ResultCount == 1, "Room search filters native table");
        page.SwitchTab(2);
        Expect(page.ResultCount == 0, "Weekly view requires an explicit timetable subject");
        page.SetLecturerFilterForCheck(page.Store.Classes.First().LecturerId);
        Expect(page.ResultCount > 0 && page.CalendarCountForCheck == page.ResultCount, "Week contains actual scheduled occurrences");
        await Shot(form, "03-week");
        Expect(page.CalendarCardsReadableForCheck, "Weekly cards retain course, class, room and periods after text wraps");
        Expect(page.TabsAcceptNavigationKeysForCheck, "Workspace tabs accept arrow, Home and End keys as input");
        page.NavigateTabForCheck(Keys.Home);
        Expect(page.TabIndexForCheck == 0, "Home key selects the first workspace tab");
        page.NavigateTabForCheck(Keys.Right);
        Expect(page.TabIndexForCheck == 1, "Right arrow switches workspace tab through native key handling");
        page.NavigateTabForCheck(Keys.End);
        Expect(page.TabIndexForCheck == 2, "End key selects the final workspace tab");
        page.SetRoomFilterForCheck(page.Store.Rooms[0].Id);
        var filtered = SchedulingQuery.Occurrences(page.Store, page.WeekForCheck, page.WeekForCheck.AddDays(6), page.Store.Rooms[0].Id);
        Expect(page.CalendarCountForCheck == filtered.Count, "Room filter matches exact weekly dates");
        page.SetRoomFilterForCheck("");
        page.SetLecturerFilterForCheck(page.Store.Classes.First().LecturerId);
        var week = page.WeekForCheck; page.MoveWeek(7);
        Expect(page.WeekForCheck == week.AddDays(7), "Next week navigation advances seven days");
        page.MoveWeek(-7);
        page.SetSemesterForCheck(1);
        Expect(page.ResultCount == SchedulingQuery.Occurrences(page.Store, page.WeekForCheck, page.WeekForCheck.AddDays(6)).Count(o => o.Class.SemesterId == page.Store.Catalog.Semesters[1].Id), "Semester filters timetable");
        page.SetSemesterForCheck(0); page.SwitchTab(0);
        using (var editor = new RoomEditor(page.Store))
        {
            editor.Show(form);
            editor.FillForCheck(new Room("X901", "Tòa X", 0, SchedulingLabels.Theory, SchedulingLabels.Ready));
            Expect(!editor.TrySave(), "Room editor rejects zero capacity");
            editor.FillForCheck(new Room("X901", "Tòa X", 60, SchedulingLabels.Theory, SchedulingLabels.Ready));
            Expect(editor.TrySave() && page.Store.Rooms.Any(r => r.Id == "X901"), "Room editor commits valid room");
            await Shot(editor, "05-room-editor");
            var editorFonts = editor.OwnedFontResourcesForCheck;
            editor.Close();
            Expect(editorFonts.All(IsDisposed), "Closing a scheduling editor releases every owned font instance");
        }
        var term = page.Store.Catalog.Semesters[0];
        var course = page.Store.Catalog.Courses[0];
        var lecturer = page.Store.Catalog.Lecturers.First(l => l.Status == "Đang giảng dạy");
        var room = page.Store.Rooms.First(r => r.Status == SchedulingLabels.Ready);
        var newClassId = page.Store.NextClassId();
        using (var editor = new CourseClassEditor(page.Store))
        {
            editor.Show(form);
            var newClass = new CourseClass(newClassId, course.Id, term.Id, lecturer.Id, 30, 0, SchedulingLabels.Open, ImmutableArray<ClassMeeting>.Empty);
            editor.FillForCheck(newClass with { Capacity = 0 });
            Expect(!editor.TrySave(), "Class editor rejects zero capacity");
            editor.FillForCheck(newClass);
            Expect(editor.TrySave() && page.Store.Classes.Any(c => c.Id == newClassId), "Class editor saves unscheduled class without registering students");
            await Shot(editor, "06-class-editor");
            editor.Close();
        }
        using (var editor = new MeetingEditor(page.Store, term, 30))
        {
            editor.Show(form);
            editor.FillForCheck(new ClassMeeting(room.Id, DayOfWeek.Monday, 3, 1, term.StartDate, term.EndDate));
            Expect(!editor.TrySave(), "Meeting editor rejects reversed periods");
            editor.FillForCheck(new ClassMeeting("X901", DayOfWeek.Saturday, 1, 3, term.StartDate, term.EndDate));
            Expect(editor.TrySave(), "Meeting editor accepts valid dates and periods");
            await Shot(editor, "07-meeting-editor");
            editor.Close();
        }
        page.RefreshRoomChoices();
        Expect(page.RoomChoiceExistsForCheck("X901"), "Saved room is available in timetable filter");
        Expect(page.Store.TryDeleteClass(newClassId, out _), "Empty demo class can be deleted");
        Expect(page.Store.TryDeleteRoom("X901", out _), "Unused demo room can be deleted");
        page.RefreshRoomChoices();
        Expect(!page.RoomChoiceExistsForCheck("X901"), "Deleted room is removed from timetable filter");
        page.SelectSavedSemester(term.Id == page.Store.Catalog.Semesters[0].Id ? page.Store.Catalog.Semesters[1].Id : term.Id);
        Expect(page.WeekForCheck == SchedulingQuery.WeekStart(page.Store.Catalog.Semesters[1].StartDate), "Saving a class in another semester resets calendar week");
        page.SetSemesterForCheck(0);
        var fullLecturer = form.Store.Items.First(item => item.Id == lecturer.Id);
        using (var editor = new LecturerEditor(form.Store, fullLecturer,
            (value, isNew) => SchedulingLecturerGuard.ValidationError(page.Store, value, isNew)))
        {
            editor.Show(form); editor.FillForTest(fullLecturer with { Status = "Tạm nghỉ" });
            Expect(!editor.TrySave() && form.Store.Items.First(l => l.Id == lecturer.Id).Status == "Đang giảng dạy",
                "Lecturer editor prevents suspending a teacher assigned to a class");
            editor.Close();
        }
        var exhaustedRooms = new ScheduleStore(page.Store.Catalog, [room with { Id = "P9999" }], []);
        using (var editor = new RoomEditor(exhaustedRooms))
        {
            Expect(editor.ValidationSummary.Contains("Đã hết mã tự động"), "Room ID exhaustion offers manual input");
            editor.FillForCheck(room with { Id = "P0001" });
            Expect(editor.TrySave(), "Room ID exhaustion still allows an unused manual ID");
        }
        var exhaustedClass = page.Store.Classes[0] with { Id = "LHP9999", Enrolled = 0, Meetings = ImmutableArray<ClassMeeting>.Empty };
        var exhaustedClasses = new ScheduleStore(page.Store.Catalog, page.Store.Rooms, [exhaustedClass]);
        using (var editor = new CourseClassEditor(exhaustedClasses))
        {
            Expect(editor.ValidationSummary.Contains("Đã hết mã tự động"), "Class ID exhaustion offers manual input");
            editor.FillForCheck(exhaustedClass with { Id = "LHP0001" });
            Expect(editor.TrySave(), "Class ID exhaustion still allows an unused manual ID");
        }
        page.RefreshData(false);
        form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
        await Shot(form, "08-compact");
        File.WriteAllText(Path.Combine(folder, "layout-compact.json"), JsonSerializer.Serialize(page.LayoutDiagnosticsForCheck));
        Expect(!page.HasHorizontalScrollForCheck, "Compact page uses vertical scrolling without page overflow");
        page.SwitchTab(2);
        await Shot(form, "13-compact-week");
        Expect(page.CalendarCardsReadableForCheck, "Compact weekly cards keep all room and period metadata after wrapping");
        page.SwitchTab(0);
        page.ScrollToBottomForCheck();
        await Shot(form, "11-compact-detail");
        Expect(page.CompactDetailInViewForCheck, "Compact page scroll reaches detail and action buttons");
        page.ScrollToTopForCheck();
        form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
        AntdUI.Config.Animation = false;
        PageMotionCoordinator.Current?.RefreshPreference();
        page.RefreshData();
        Expect(PageMotionCoordinator.Current?.ActiveCount == 0, "Reduced motion completes finite jobs");
        await Shot(form, "09-reduced-motion");
        AntdUI.Config.Animation = true;
        page.ReturnToLecturersForCheck();
        await Task.Delay(900);
        Expect(form.Store.Items.Count == 12 && form.ResultCount == 12, "Returning to lecturer page preserves lecturer data");
        await Shot(form, "10-lecturer-return");
        var expectedStore = page.Store;
        var previousPageFonts = page.OwnedFontResourcesForCheck;
        var previousVisualBitmaps = page.VisualsForCheck.OwnedBitmapsForCheck;
        form.RecreateSchedulingForCheck();
        Expect(previousPageFonts.All(IsDisposed), "View recreation releases every page and detail font instance");
        Expect(previousVisualBitmaps.All(IsBitmapDisposed), "View recreation disposes every shared resource image and cached thumbnail");
        page = form.ShowScheduling();
        Expect(ReferenceEquals(page.Store, expectedStore), "Hidden view recreation preserves shared scheduling store");
        page.SwitchTab(2); page.MoveWeek(7); page.SetRoomFilterForCheck(room.Id);
        var expectedView = page.CaptureView();
        form.RecreateSchedulingForCheck(); page = form.ShowScheduling();
        Expect(page.CaptureView() == expectedView, "View recreation preserves tab, semester, week, filter and selection");
        page.SwitchTab(0);
        await Task.Delay(1000);
        Expect(PageMotionCoordinator.Current?.ActiveCount == 0 && PageMotionCoordinator.Current?.TimerIsRunning == false, "Scheduling page rests with no ambient timer jobs");
        page.ScrollToTopForCheck();
        await Shot(page.AgendaForCheck, "30-agenda-clean");
        page.Reveal();
        await UiCapture.Entrance(form, folder);
        await Task.Delay(800);
        Expect(PageMotionCoordinator.Current?.ActiveCount == 0, "Entrance and count-up finish after native frame capture");
        using (var fixture = new WeeklySchedule { Size = new(AppTheme.S(900), AppTheme.S(240)) })
        {
            var occurrence = SchedulingQuery.Occurrences(page.Store, page.WeekForCheck, page.WeekForCheck.AddDays(6)).First();
            form.Controls.Add(fixture);
            fixture.SetData(page.Store, Enumerable.Repeat(occurrence, 8).ToArray(), occurrence.Date);
            using var paintCheck = new WeeklySchedulePaintCheck();
            Expect(paintCheck.CornersMatchBackground(fixture, folder, "14-calendar-corner-repaint"),
                "Weekly card repaint fills all four rounded corners with its background");
            CourseClass? clickedClass = null;
            fixture.ClassSelected += item => clickedClass = item;
            fixture.ActivateFirstSessionForCheck();
            Expect(clickedClass?.Id == occurrence.Class.Id, "Rounded weekly card activates the correct class through native Button click");
            fixture.ScrollFirstDayForCheck();
            Expect(fixture.FirstDayAlignedForCheck, "Dense day fixture remains aligned while scrolled");
            fixture.Width += AppTheme.S(60);
            Expect(fixture.FirstDayAlignedForCheck, "Resizing a scrolled day preserves its card alignment");
            Expect(fixture.CardsReadableForCheck, "Dense rounded weekly cards retain all text after resize");
            Expect(paintCheck.CornersMatchBackground(fixture, folder, "15-calendar-corner-resize"),
                "Resized weekly cards repaint their entire background");
            fixture.Enabled = false;
            Expect(paintCheck.CornersMatchBackground(fixture, folder, "16-calendar-corner-disabled"),
                "Disabled weekly cards retain a clean corner background");
        }
        await SchedulingAssetsUiCheck.Run(form, page, folder, checks);
        await SchedulingVisualUiCheck.Run(form, page, folder, checks);
        await SchedulingMetricsUiCheck.Run(form, page, folder, checks);
        await SchedulingServiceUiCheck.Run(form, page, folder, checks);
    }
}
