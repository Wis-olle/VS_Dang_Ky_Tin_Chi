using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Shell;

internal sealed partial class CampusForm
{
    private AntdUI.Button? quickStudentLogin, quickTrainingLogin;
    private AntdUI.Label? quickLoginTitle;
    private bool quickLoginAvailable;

    private QuickLoginCredentials? ReadQuickLogin() => QuickLoginCredentials.Load(connectionString,
        Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE"));

    private void BuildQuickLogin()
    {
        int s(int n) => AppTheme.S(n);
        quickLoginTitle = TextLabel("Đăng nhập nhanh bằng tài khoản mẫu", 9, true, AppTheme.Muted);
        quickLoginTitle.SetBounds(s(32), s(440), s(576), s(28));
        quickStudentLogin = ActionButton("Đăng nhập mẫu Sinh viên");
        quickTrainingLogin = ActionButton("Đăng nhập mẫu PĐT");
        quickStudentLogin.Name = "btnQuickStudentLogin"; quickTrainingLogin.Name = "btnQuickTrainingLogin";
        quickStudentLogin.AccessibleName = "Đăng nhập nhanh bằng tài khoản mẫu Sinh viên";
        quickTrainingLogin.AccessibleName = "Đăng nhập nhanh bằng tài khoản mẫu Phòng đào tạo";
        quickStudentLogin.TabIndex = 3; quickTrainingLogin.TabIndex = 4;
        quickStudentLogin.DefaultBack = AppTheme.Lilac; quickTrainingLogin.DefaultBack = AppTheme.AccentSoft;
        quickStudentLogin.SetBounds(s(32), s(480), s(278), s(44));
        quickTrainingLogin.SetBounds(s(326), s(480), s(278), s(44));
        quickStudentLogin.Click += async (_, _) => await QuickLoginAsync(LoginPortal.Student);
        quickTrainingLogin.Click += async (_, _) => await QuickLoginAsync(LoginPortal.Training);
        connectCard.Controls.AddRange([quickLoginTitle, quickStudentLogin, quickTrainingLogin]);
        RefreshQuickLogin();
    }

    private void RefreshQuickLogin()
    {
        quickLoginAvailable = QuickLoginCredentials.Load(connectionString,
            Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE"), out var status) != null;
        if (quickLoginTitle != null)
        {
            quickLoginTitle.Visible = true;
            quickLoginTitle.Text = status switch {
                QuickLoginStatus.Ready => "Đăng nhập nhanh bằng tài khoản mẫu",
                QuickLoginStatus.MissingConfiguration => "Chưa có tệp cấu hình tài khoản mẫu trên máy này",
                QuickLoginStatus.UnsupportedTarget => "Tài khoản mẫu cần SQL Server cục bộ, instance mặc định",
                QuickLoginStatus.DifferentDatabase => "Tài khoản mẫu chưa được cấu hình cho database đang mở",
                QuickLoginStatus.CannotDecrypt => "Không đọc được tài khoản mẫu bằng người dùng Windows hiện tại",
                QuickLoginStatus.CannotRead => "Chưa có quyền đọc tệp cấu hình tài khoản mẫu",
                _ => "Cấu hình tài khoản mẫu chưa hợp lệ; cần cấu hình lại"
            };
        }
        if (quickStudentLogin != null) { quickStudentLogin.Visible = true; quickStudentLogin.Enabled = quickLoginAvailable && !busy; }
        if (quickTrainingLogin != null) { quickTrainingLogin.Visible = true; quickTrainingLogin.Enabled = quickLoginAvailable && !busy; }
        errorText.SetBounds(AppTheme.S(32), AppTheme.S(544), AppTheme.S(576), AppTheme.S(66));
    }

    internal async Task QuickLoginAsync(LoginPortal portal)
    {
        if (!passwordLogin || busy || identity != null) return;
        var account = ReadQuickLogin()?.For(portal);
        if (account == null)
        {
            RefreshQuickLogin(); LayoutShell();
            errorText.Text = "Chưa có thông tin đăng nhập mẫu phù hợp trên máy này. Bạn vẫn có thể nhập tài khoản được cấp.";
            return;
        }
        SelectLoginPortal(portal);
        username.Text = account.Username; password.Text = account.Password;
        await AuthenticateAsync(portal == LoginPortal.Training ? QuanLyGiangVien.Domain.Shell.CampusModule.Lecturers : null);
    }
}
