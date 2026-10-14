using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.StudentTimetable;

/// <summary>The two student tabs share one identity and retain their view state.</summary>
internal sealed class StudentPortalPage : UserControl
{
    private readonly SqlMinhRepository repository;
    private readonly DatabaseIdentity identity;
    private readonly RegistrationPage registration;
    private StudentTimetableForm? timetable;
    private bool busy;
    internal Control? ActivePage { get; private set; }
    internal CampusModule? ActiveModule { get; private set; }
    internal RegistrationPage Registration => registration;
    internal StudentTimetableForm? Timetable => timetable;

    internal StudentPortalPage(string connectionString, DatabaseIdentity identity, Action dataChanged)
    {
        if (identity.Role != "SINH_VIEN" || string.IsNullOrWhiteSpace(identity.StudentId))
            throw new PersistenceException("Cổng sinh viên cần tài khoản sinh viên được cấp quyền.");
        this.identity = identity; repository = new(connectionString);
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas;
        registration = new(connectionString, identity, dataChanged) { Visible = false };
        Controls.Add(registration);
        SizeChanged += (_, _) => FitPage();
    }

    internal bool CanLeave() => !busy && registration.CanLeave();

    internal async Task OpenAsync(CampusModule module, CancellationToken token)
    {
        if (!CanLeave()) return;
        if (module is not (CampusModule.Registration or CampusModule.Timetable))
            throw new ArgumentException("Tab không thuộc cổng sinh viên.");
        busy = true;
        try
        {
            // Resolve the shared semester through Huệ's catalog even when the
            // timetable is the first visible tab. Preserve later manual choices.
            if (ActiveModule == null && module == CampusModule.Timetable)
            {
                await registration.ReloadAsync();
                if (IsDisposed) return;
            }
            Control next;
            StudentTimetableView? prior = null;
            if (module == CampusModule.Registration)
            {
                var semester = ActiveModule == CampusModule.Timetable ? timetable?.Snapshot.Semester.Id : registration.MaHocKy;
                await registration.ReloadForSemesterAsync(semester);
                if (IsDisposed) return;
                next = registration;
            }
            else
            {
                var fresh = await repository.LoadAsync(token);
                if (IsDisposed) return;
                if (fresh.Identity.AccountId != identity.AccountId || fresh.Identity.StudentId != identity.StudentId
                    || fresh.Identity.Role != identity.Role)
                    throw new PersistenceException("Tài khoản đã thay đổi. Hãy ngắt kết nối và kết nối lại.");
                var term = ActiveModule is null or CampusModule.Registration ? registration.MaHocKy : timetable?.Snapshot.Semester.Id;
                if (string.IsNullOrWhiteSpace(term) || !fresh.Semesters.Any(value => value.Id == term))
                    throw new PersistenceException("Chưa xác định được học kỳ từ SQL. Hãy tải lại danh mục học kỳ trước khi xem lịch.");
                prior = timetable?.CaptureView();
                var replacement = new StudentTimetableForm(fresh, embedded: true);
                replacement.RestoreView(term, prior);
                Controls.Add(replacement);
                timetable?.Dispose(); timetable = replacement; next = replacement;
            }
            SurfaceTransition.CancelAll();
            if (ActivePage != next && ActivePage is { IsDisposed: false }) ActivePage.Hide();
            ActivePage = next; ActiveModule = module;
            next.Dock = DockStyle.Fill; next.Show(); next.BringToFront(); FitPage();
            if (next is StudentTimetableForm calendar && prior?.SemesterId == calendar.Snapshot.Semester.Id)
                calendar.ScrollPage(prior.Scroll);
        }
        finally { busy = false; }
    }

    private void FitPage()
    {
        if (ActivePage == null) return;
        if (ActivePage is AntdUI.Window window) window.Size = ClientSize;
        ActivePage.Bounds = ClientRectangle;
    }
}
