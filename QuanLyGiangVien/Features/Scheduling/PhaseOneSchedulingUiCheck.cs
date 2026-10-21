using System.Drawing.Imaging;
using System.Reflection;
using QuanLyGiangVien.Dialogs;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Opt-in native editor regression checks, using isolated in-memory stores.</summary>
internal static class PhaseOneSchedulingUiCheck
{
    internal static async Task Run(MainForm owner, ScheduleStore source, string folder, List<string> checks)
    {
        void Expect(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
        static T Field<T>(object target, string name) where T : class =>
            (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
                ?? throw new InvalidOperationException("Missing editor field " + name));
        static async Task Shot(Control control, string folder, string name)
        {
            await Task.Delay(260);
            using var image = UiCapture.Client(control);
            image.Save(Path.Combine(folder, name), ImageFormat.Png);
        }
        const string version = "0000000000000001";
        var room = source.Rooms.First(r => r.Status == SchedulingLabels.Ready) with { Capacity = 501, Version = version };
        var rooms = new ScheduleStore(source.Catalog, [room], []);
        using (var editor = new RoomEditor(rooms, room))
        {
            editor.Show(owner);
            editor.FillForCheck(room with { Capacity = 1000 });
            Expect(editor.TrySave(), "Phase 1: room editor accepts SQL-supported capacity 1000");
            Expect(editor.Result is { Capacity: 1000, Version: version }, "Phase 1: room editor preserves original optimistic version");
            await Shot(editor, folder, "phase1-room-1000.png");
            editor.Close();
        }
        var term = source.Catalog.Semesters[0];
        var teacher = source.Catalog.Lecturers.First(g => g.Status == "Đang giảng dạy");
        var historic = new CourseClass("HISTORY_UI", source.Catalog.Courses[0].Id, term.Id, teacher.Id, 30, 0,
            SchedulingLabels.Open, [new(room.Id, DayOfWeek.Monday, 1, 3, term.StartDate, term.EndDate)], version, true);
        var historyStore = new ScheduleStore(source.Catalog, [room with { Capacity = 1000 }], [historic]);
        using (var editor = new CourseClassEditor(historyStore, historic))
        {
            editor.Show(owner);
            foreach (var field in new[] { "course", "semester", "lecturer", "editMeeting", "removeMeeting" })
                Expect(!Field<Control>(editor, field).Enabled, "Phase 1: canceled history locks editor field " + field);
            var add = Descendants(editor).Single(c => c is AntdUI.Button && c.Text == "Thêm buổi");
            Expect(!add.Enabled, "Phase 1: canceled history disables adding meetings");
            editor.FillForCheck(historic with { Capacity = 1000 });
            Expect(editor.TrySave(), "Phase 1: historical class editor allows capacity 1000 when room is sufficient");
            Expect(editor.Result is { Capacity: 1000, Version: version, HasRegistrationHistory: true },
                "Phase 1: historical class editor preserves version and history flag");
            await Shot(editor, folder, "phase1-history-locked-editor.png");
            editor.Close();
        }
        var lecturer = owner.Store.Items[0] with { ActiveClasses = 0, Version = version };
        var lecturers = new LecturerStore([lecturer]);
        using (var editor = new LecturerEditor(lecturers, lecturer))
        {
            editor.Show(owner);
            editor.FillForTest(lecturer with { Name = "Nguyễn Kiểm Thử" });
            Expect(editor.TrySave(), "Phase 1: lecturer editor saves a valid profile with an optimistic version");
            Expect(editor.Result is { Version: version }, "Phase 1: lecturer editor preserves original optimistic version");
            editor.Close();
        }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
