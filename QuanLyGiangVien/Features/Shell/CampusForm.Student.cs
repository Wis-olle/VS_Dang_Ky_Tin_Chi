using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.StudentTimetable;

namespace QuanLyGiangVien.Features.Shell;

internal sealed partial class CampusForm
{
    private async Task OpenStudentTabAsync(CampusModule module, DatabaseIdentity user)
    {
        if (currentPage is not StudentPortalPage)
        {
            ClearPage();
            var portal = new StudentPortalPage(connectionString, user, DataChanged);
            currentPage = portal; canLeave = portal.CanLeave;
            currentPage.Enabled = !busy;
            host.Controls.Add(currentPage); currentPage.Show(); currentPage.BringToFront();
            connectCard.Visible = false;
            FitCurrentPage();
        }
        var workspace = (StudentPortalPage)currentPage;
        await workspace.OpenAsync(module, lifetime.Token);
        if (IsDisposed) return;
        selectedModule = workspace.ActiveModule;
        foreach (var button in moduleButtons) ApplySelected(button.Value, button.Key == selectedModule);
    }
}
