using System.Drawing.Imaging;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal static class SchedulingVisualUiCheck
{
    internal static async Task Run(MainForm form, SchedulingPage page, string folder, List<string> checks)
    {
        void Expect(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        var visuals = page.VisualsForCheck;
        Expect(visuals.LoadedKeysForCheck.Count == 7, "All seven subject and room illustrations are packaged and load locally");
        var courseVisuals = page.Store.Catalog.Courses.Select(visuals.GetCourse).ToArray();
        var roomVisuals = page.Store.Rooms.Select(visuals.GetRoom).ToArray();
        Expect(courseVisuals.Select(visual => visual.Key).Distinct().Count() == 4,
            "Course thumbnails distinguish all four faculties using their actual catalogue data");
        Expect(roomVisuals.Select(visual => visual.Key).Distinct().Count() == 3,
            "Room thumbnails distinguish theory, laboratory and hall types");
        Expect(courseVisuals.Concat(roomVisuals).All(visual => visual.Image is { Width: > 0, Height: > 0 }),
            "Every displayed subject and room type has a decoded image");
        var sample = courseVisuals[0];
        Expect(ReferenceEquals(visuals.Thumbnail(sample), visuals.Thumbnail(sample)),
            "Table rows reuse cached artwork thumbnails instead of allocating per repaint");
        page.SwitchTab(0);
        for (int index = 0; index < page.Store.Classes.Count; index++)
        {
            var item = page.Store.Classes[index];
            if (item.SemesterId != page.Store.Catalog.Semesters[0].Id) continue;
            page.SelectClassForCheck(index);
            var course = page.Store.Catalog.Courses.First(course => course.Id == item.CourseId);
            Expect(page.DetailVisualKeyForCheck == visuals.GetCourse(course).Key,
                "Inspector cover follows the selected course: " + item.CourseId);
        }
        page.SelectClassForCheck(0);
        page.SwitchTab(2);
        page.SetLecturerFilterForCheck(page.Store.Classes.First().LecturerId);
        Expect(page.CalendarThumbnailCountForCheck == page.CalendarCountForCheck,
            "Every scheduled occurrence has its subject illustration");
        Expect(page.CalendarThumbnailsFitForCheck && page.CalendarCardsReadableForCheck,
            "Weekly illustrations stay separate from complete title and metadata text");
        form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
        await Task.Delay(300);
        Expect(page.CalendarThumbnailsFitForCheck && page.CalendarCardsReadableForCheck,
            "Compact weekly illustrations preserve course, room and period metadata");
        using (var bitmap = UiCapture.Client(form)) bitmap.Save(Path.Combine(folder, "19-compact-week-visuals.png"), ImageFormat.Png);
        form.ClientSize = new(AppTheme.S(1440), AppTheme.S(900));
        page.SwitchTab(0); page.ScrollToTopForCheck();
        await Task.Delay(600);
        Expect(page.DetailBodyReadableForCheck, "The larger inspector cover preserves the schedule body's minimum height");
    }
}
