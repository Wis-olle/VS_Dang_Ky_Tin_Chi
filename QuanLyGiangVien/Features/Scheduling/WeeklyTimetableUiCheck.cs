namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>The former aggregate/lane harness now follows the subject-scoped specification.</summary>
internal static class WeeklyTimetableUiCheck
{
    internal static void Attach(MainForm form)
    {
        var folder = Environment.GetEnvironmentVariable("WEEKLY_TIMETABLE_UI_CHECK");
        if (string.IsNullOrWhiteSpace(folder) || !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("SCOPED_TIMETABLE_UI_CHECK"))) return;
        ScopedTimetableUiCheck.Attach(form, folder);
    }
}
