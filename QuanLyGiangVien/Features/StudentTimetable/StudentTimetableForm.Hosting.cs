namespace QuanLyGiangVien.Features.StudentTimetable;

internal sealed record StudentTimetableView(string SemesterId, DateOnly Date, int Scroll);

internal sealed partial class StudentTimetableForm
{
    internal StudentTimetableView CaptureView() => new(semesterId, selectedDate,
        (int)Math.Round(-scroll.AutoScrollPosition.Y / Ui.AppTheme.K));

    internal void RestoreView(string? preferredSemester, StudentTimetableView? prior)
    {
        var index = store.Catalog.Semesters.ToList().FindIndex(item => item.Id == preferredSemester);
        if (index < 0) return;
        building = true;
        try { semester.SelectedIndex = index; semesterId = store.Catalog.Semesters[index].Id; }
        finally { building = false; }
        selectedDate = prior?.SemesterId == semesterId ? prior.Date : DateOnly.FromDateTime(DateTime.Today);
        RefreshWeek(false);
    }
}
