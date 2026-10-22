using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Shell;

internal sealed partial class CampusForm
{
    private LoginPortal loginPortal;
    private readonly TextBox username = new() { Name = "txtLoginUsername", BorderStyle = BorderStyle.FixedSingle, MaxLength = 128 };
    private readonly TextBox password = new() { Name = "txtLoginPassword", BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = true, MaxLength = 256 };
    private AntdUI.Button? studentLogin, trainingLogin;
    private AntdUI.Label? loginHeading;

    private void BuildPasswordLogin()
    {
        int s(int value) => AppTheme.S(value);
        connectCard.Back = Color.White; connectCard.BackColor = Color.Transparent;
        connectCard.Radius = 20; connectCard.BorderWidth = 1; connectCard.BorderColor = AppTheme.Line;
        loginHeading = TextLabel("", 22, true);
        loginHeading.SetBounds(s(32), s(24), s(576), s(48));
        var description = TextLabel(Environment.GetEnvironmentVariable("CAMPUS_SHOWCASE_DEMO") == "1"
            ? "Dữ liệu test đầy đủ · Chọn cổng hoặc dùng tài khoản mẫu."
            : "Chọn cổng hoặc dùng tài khoản mẫu bên dưới.", 10, false, AppTheme.Muted);
        description.SetBounds(s(32), s(80), s(576), s(32));
        studentLogin = ActionButton("Sinh viên"); trainingLogin = ActionButton("Phòng đào tạo");
        studentLogin.AccessibleName = "Cổng Sinh viên"; trainingLogin.AccessibleName = "Cổng Phòng đào tạo";
        studentLogin.SetBounds(s(32), s(128), s(278), s(44));
        trainingLogin.SetBounds(s(326), s(128), s(278), s(44));
        studentLogin.Click += (_, _) => SelectLoginPortal(LoginPortal.Student);
        trainingLogin.Click += (_, _) => SelectLoginPortal(LoginPortal.Training);
        var userLabel = TextLabel("Tên đăng nhập", 10, true);
        var passwordLabel = TextLabel("Mật khẩu", 10, true);
        userLabel.SetBounds(s(32), s(194), s(576), s(28));
        passwordLabel.SetBounds(s(32), s(280), s(576), s(28));
        username.Font = password.Font = MakeFont(11);
        username.SetBounds(s(32), s(230), s(576), s(38));
        password.SetBounds(s(32), s(316), s(576), s(38));
        username.AccessibleName = "Tên đăng nhập"; password.AccessibleName = "Mật khẩu";
        username.TabIndex = 0; password.TabIndex = 1; connect.TabIndex = 2;
        connect.Name = "btnPasswordLogin"; connect.Text = "Đăng nhập";
        connect.SetBounds(s(32), s(378), s(576), s(46));
        errorText.TextMultiLine = true;
        errorText.SetBounds(s(32), s(438), s(576), s(66));
        password.KeyDown += async (_, args) => {
            if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; await AuthenticateAsync(); }
        };
        connectCard.Controls.AddRange([loginHeading, description, studentLogin, trainingLogin, userLabel, username, passwordLabel, password, connect, errorText]);
        studentLogin.TabIndex = 5; trainingLogin.TabIndex = 6;
        BuildQuickLogin();
        disconnect.Text = "Đăng xuất";
        SelectLoginPortal(loginPortal);
    }

    internal void SelectLoginPortal(LoginPortal portal)
    {
        if (busy || identity != null) return;
        loginPortal = portal; password.Clear(); errorText.Text = "";
        if (studentLogin == null || trainingLogin == null || loginHeading == null) return;
        ApplySelected(studentLogin, portal == LoginPortal.Student); ApplySelected(trainingLogin, portal == LoginPortal.Training);
        loginHeading.Text = portal == LoginPortal.Student ? "Đăng nhập Sinh viên" : "Đăng nhập Phòng đào tạo";
        Text = portal == LoginPortal.Student ? "Campus · Đăng nhập Sinh viên" : "Campus · Đăng nhập Phòng đào tạo";
        if (Environment.GetEnvironmentVariable("CAMPUS_SHOWCASE_DEMO") == "1") Text += " · Dữ liệu mô phỏng";
    }

    internal async Task AuthenticateAsync(CampusModule? preferredModule = null)
    {
        if (!passwordLogin || busy || identity != null) return;
        SetBusy(true); SetLoginEnabled(false); errorText.Text = "";
        bool failed = false;
        var auth = new PasswordLogin(connectionString);
        try
        {
            await auth.SignInAsync(username.Text, password.Text, loginPortal, lifetime.Token);
            password.Clear();
            var user = await identityRepository.LoadAsync(lifetime.Token);
            if (IsDisposed) return;
            if (!PasswordLogin.Allows(loginPortal, user.Role)) throw new PersistenceException("Tài khoản không thuộc cổng đã chọn.");
            ConfigureIdentity(user);
            var first = (preferredModule ?? initialModule) is { } requested && CampusNavigation.CanOpen(user.Role, requested)
                ? requested : user.Role == "SINH_VIEN" ? CampusModule.Registration : CampusModule.Catalogs;
            await LoadModuleAsync(first, user);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is PersistenceException or ArgumentException or InvalidOperationException)
        {
            await auth.SignOutAsync();
            if (!IsDisposed) { DisconnectCore(); errorText.Text = SafeMessage(error); password.Clear(); failed = true; }
        }
        finally { if (!IsDisposed) { SetBusy(false); SetLoginEnabled(true); if (failed) password.Focus(); } }
    }

    internal async Task LogoutAsync()
    {
        if (busy || canLeave?.Invoke() == false) return;
        SetBusy(true);
        try { await new PasswordLogin(connectionString).SignOutAsync(); }
        finally
        {
            if (!IsDisposed) { DisconnectCore(); password.Clear(); username.Clear(); SetBusy(false); RefreshQuickLogin(); LayoutShell(); SelectLoginPortal(loginPortal); username.Focus(); }
        }
    }
    private void SetLoginEnabled(bool enabled)
    {
        username.Enabled = password.Enabled = enabled;
        if (studentLogin != null) studentLogin.Enabled = enabled;
        if (trainingLogin != null) trainingLogin.Enabled = enabled;
        if (quickStudentLogin != null) quickStudentLogin.Enabled = enabled && quickLoginAvailable;
        if (quickTrainingLogin != null) quickTrainingLogin.Enabled = enabled && quickLoginAvailable;
    }
}
