using AntdUI;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Catalogs;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.StudentTimetable;
using QuanLyGiangVien.Features.Students;
using QuanLyGiangVien.Features.Academics;
using QuanLyGiangVien.Features.Accounts;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Shell;

/// <summary>One top-level window for the modules available in the team's source.</summary>
internal sealed partial class CampusForm : Window
{
    private readonly string connectionString;
    private readonly SqlMinhRepository repository;
    private readonly SqlIdentityRepository identityRepository;
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<Font> ownedFonts = [];
    private readonly Dictionary<CampusModule, AntdUI.Button> moduleButtons = [];
    private readonly PageHeader caption = new();
    private readonly System.Windows.Forms.Panel navigation = new(), host = new();
    private readonly AntdUI.Panel connectCard = new();
    private readonly AntdUI.Label brand, identityText, roleText, connectionText, errorText;
    private readonly AntdUI.Button connect, disconnect;
    private readonly AntdUI.Button changePassword = new();
    private Control? currentPage;
    private Func<bool>? canLeave;
    private DatabaseIdentity? identity;
    private CampusModule? selectedModule;
    private bool busy;
    private readonly CampusModule? initialModule;
    private readonly bool studentOnly;
    private readonly bool passwordLogin;
    internal Control? CurrentPage => currentPage;
    internal Control? CurrentModulePage => currentPage is StudentPortalPage portal ? portal.ActivePage : currentPage;
    internal CampusModule? SelectedModule => selectedModule;
    internal DatabaseIdentity? ConnectedIdentity => identity;
    internal System.Windows.Forms.Panel PageHost => host;
    internal IReadOnlyCollection<CampusModule> NavigationModules => moduleButtons.Keys;

    internal CampusForm(string connectionString, CampusModule? initialModule = null, bool studentPortal = false, bool passwordLogin = false)
    {
        this.passwordLogin = passwordLogin;
        loginPortal = studentPortal ? LoginPortal.Student : LoginPortal.Training;
        this.connectionString = connectionString; this.initialModule = initialModule;
        repository = new(connectionString); identityRepository = new(connectionString);
        AutoHandDpi = false; AutoScaleMode = AutoScaleMode.None; AppTheme.SetScale(DeviceDpi);
        Text = "Campus · Đăng ký tín chỉ"; BackColor = AppTheme.Canvas;
        Font = MakeFont(10); StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new(AppTheme.S(1100), AppTheme.S(760)); ClientSize = new(AppTheme.S(1440), AppTheme.S(960));
        brand = TextLabel("campus.", 22, true);
        identityText = TextLabel("", 9, true); roleText = TextLabel("", 8, false, AppTheme.Muted);
        connectionText = TextLabel("", 9, false, AppTheme.Muted); errorText = TextLabel("", 9, false, AppTheme.Danger);
        connect = ActionButton("Kết nối hệ thống", true); disconnect = ActionButton("Ngắt kết nối");
        BuildChrome();
        BuildSessionRecovery();
        if (passwordLogin) BuildPasswordLogin(); else BuildConnection();
        LayoutShell();
        connect.Click += async (_, _) => { if (passwordLogin) await AuthenticateAsync(); else await ConnectAsync(); };
        disconnect.Click += async (_, _) => { if (passwordLogin) await LogoutAsync(); else Disconnect(); };
        SizeChanged += (_, _) => LayoutShell();
        ClientSizeChanged += (_, _) => LayoutShell();
        Activated += (_, _) => { if (passwordLogin && identity == null && !busy) RefreshQuickLogin(); };
        FormClosing += (_, args) => { if (busy || canLeave?.Invoke() == false) args.Cancel = true; };
        KeyPreview = true;
        KeyDown += async (_, args) => {
            if (args.KeyCode != Keys.F5 || identity == null || selectedModule == null) return;
            args.Handled = true; args.SuppressKeyPress = true;
            await OpenModuleAsync(selectedModule.Value, true);
        };
        UnifiedUiCheck.Attach(this, connectionString);
        StudentPortalUiCheck.Attach(this, connectionString);
        PasswordLoginUiCheck.Attach(this);
        PhaseOneUiCheck.Attach(this, connectionString);
        PhaseTwoUiCheck.Attach(this, connectionString);
        PhaseThreeUiCheck.Attach(this, connectionString);
        PhaseFourUiCheck.Attach(this, connectionString);
        AuditFixUiCheck.Attach(this, connectionString);
        ShowcaseUiCheck.Attach(this, connectionString);
        QuickLoginUiCheck.Attach(this, connectionString);
        if (studentPortal && !passwordLogin)
        {
            studentOnly = true;
            Shown += async (_, _) => await ConnectAsync();
        }
    }

    internal async Task ConnectAsync()
    {
        if (busy) return;
        SetBusy(true); errorText.Text = "";
        try
        {
            var user = await identityRepository.LoadAsync(lifetime.Token);
            if (IsDisposed) return;
            if (studentOnly && user.Role != "SINH_VIEN")
                throw new PersistenceException("Profile cổng sinh viên cần tài khoản sinh viên đã được ánh xạ trong SQL.");
            ConfigureIdentity(user);
            var first = initialModule is { } requested && CampusNavigation.CanOpen(user.Role, requested)
                ? requested : user.Role == "SINH_VIEN" ? CampusModule.Registration : CampusModule.Catalogs;
            await LoadModuleAsync(first, user);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is PersistenceException or ArgumentException or InvalidOperationException)
        {
            if (!IsDisposed) { DisconnectCore(); errorText.Text = SafeMessage(error); }
        }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    internal async Task OpenModuleAsync(CampusModule module, bool reload = false)
    {
        if (busy || identity == null || !CampusNavigation.CanOpen(identity.Role, module)) return;
        if (!reload && selectedModule == module) return;
        if (canLeave?.Invoke() == false) return;
        SetBusy(true);
        try
        {
            var user = await identityRepository.LoadAsync(lifetime.Token);
            if (IsDisposed) return;
            if (user != identity) { ClearPage(); ConfigureIdentity(user); }
            if (!CampusNavigation.CanOpen(user.Role, module)) module = CampusNavigation.ForRole(user.Role).First();
            await LoadModuleAsync(module, user);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is PersistenceException or ArgumentException or InvalidOperationException)
        {
            if (!IsDisposed) AntdUI.Message.error(this, SafeMessage(error));
        }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private async Task LoadModuleAsync(CampusModule module, DatabaseIdentity user)
    {
        if (user.Role == "SINH_VIEN")
        {
            await OpenStudentTabAsync(module, user);
            return;
        }
        var snapshot = module is CampusModule.Lecturers or CampusModule.Scheduling
            ? await repository.LoadAsync(lifetime.Token) : null;
        if (IsDisposed) return;
        if (snapshot != null && (snapshot.Identity.AccountId != user.AccountId || snapshot.Identity.Role != user.Role))
            throw new PersistenceException("Tài khoản đã thay đổi trong khi tải dữ liệu. Hãy ngắt kết nối và kết nối lại.");
        ClearPage();
        Func<Task>? reload = null;
        switch (module)
        {
            case CampusModule.Lecturers:
            case CampusModule.Scheduling:
                currentPage = new MainForm(module == CampusModule.Scheduling, snapshot, embedded: true);
                break;
            case CampusModule.Catalogs:
                var catalogs = new CatalogsPage(connectionString, DataChanged);
                currentPage = catalogs; canLeave = catalogs.CanLeave; reload = catalogs.ReloadAsync;
                break;
            case CampusModule.Reports:
                var reports = new ReportsPage(connectionString, user, DataChanged);
                currentPage = reports; canLeave = reports.CanLeave; reload = reports.ReloadAsync;
                break;
            case CampusModule.Students:
                var students = new StudentsPage(connectionString, DataChanged);
                currentPage = students; canLeave = students.CanLeave; reload = students.ReloadAsync;
                break;
            case CampusModule.Academics:
                var academics = new AcademicPage(connectionString, DataChanged);
                currentPage = academics; canLeave = academics.CanLeave; reload = academics.ReloadAsync;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(module));
        }
        connectCard.Visible = false;
        selectedModule = module; currentPage.Dock = DockStyle.Fill;
        currentPage.Enabled = !busy;
        host.Controls.Add(currentPage); currentPage.BringToFront();
        if (currentPage is Form embeddedForm) embeddedForm.Show();
        else currentPage.Show();
        if (currentPage is CatalogsPage or StudentsPage or AcademicPage) ShowcaseStyle.Apply(currentPage);
        host.PerformLayout();
        FitCurrentPage();
        foreach (var button in moduleButtons) ApplySelected(button.Value, button.Key == module);
        if (reload != null) await reload();
    }

    private void ConfigureIdentity(DatabaseIdentity user)
    {
        if (CampusNavigation.ForRole(user.Role).IsEmpty) throw new PersistenceException("Vai trò này chưa có phân hệ được tích hợp.");
        identity = user; identityText.Text = user.DisplayName;
        Text = user.Role == "SINH_VIEN" ? "Campus · Cổng sinh viên" : "Campus · Đăng ký tín chỉ";
        roleText.Text = user.Role == "SINH_VIEN" ? user.StudentId : user.Role == "QUAN_TRI" ? "Quản trị" : "Phòng đào tạo";
        if (Environment.GetEnvironmentVariable("CAMPUS_SHOWCASE_DEMO") == "1"
            && new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_Showcase_", StringComparison.Ordinal))
        { roleText.Text += " · Dữ liệu mô phỏng"; Text += " · Bản trình diễn"; }
        foreach (var button in moduleButtons.Values) { ownedFonts.Remove(button.Font); button.Font.Dispose(); button.Dispose(); }
        moduleButtons.Clear();
        foreach (var module in CampusNavigation.ForRole(user.Role))
        {
            var button = ActionButton(CampusNavigation.Title(module));
            button.AccessibleName = "Mở " + CampusNavigation.Title(module);
            if (user.Role == "SINH_VIEN")
            {
                button.AccessibleRole = AccessibleRole.PageTab;
                if (module == CampusModule.Registration) button.Text = "Đăng ký";
                button.AccessibleName = button.Text;
            }
            button.Click += async (_, _) => await OpenModuleAsync(module);
            moduleButtons.Add(module, button); navigation.Controls.Add(button);
        }
        disconnect.Visible = identityText.Visible = roleText.Visible = true;
        changePassword.Visible = passwordLogin;
        LayoutShell();
    }

    internal void Disconnect()
    {
        if (busy || canLeave?.Invoke() == false) return;
        DisconnectCore();
    }
    private void DisconnectCore()
    {
        sessionBlocked = false; sessionNotice.Visible = false; host.Enabled = true;
        ClearPage(); identity = null; selectedModule = null;
        foreach (var button in moduleButtons.Values) { ownedFonts.Remove(button.Font); button.Font.Dispose(); button.Dispose(); }
        moduleButtons.Clear(); disconnect.Visible = identityText.Visible = roleText.Visible = changePassword.Visible = false;
        connectCard.Visible = true; errorText.Text = ""; LayoutShell();
    }
    private void ClearPage()
    {
        SurfaceTransition.CancelAll(); canLeave = null;
        currentPage?.Dispose(); currentPage = null; selectedModule = null;
    }
    private void DataChanged() => host.AccessibleDescription = "Dữ liệu vừa được cập nhật. Chuyển trang hoặc bấm F5 để nạp dữ liệu mới.";
    private void SetBusy(bool value)
    {
        busy = value; connect.Enabled = disconnect.Enabled = changePassword.Enabled = !value;
        if (currentPage != null) currentPage.Enabled = !value && !sessionBlocked;
        foreach (var button in moduleButtons.Values) button.Enabled = !value && !sessionBlocked;
        UseWaitCursor = value;
    }
    private static string SafeMessage(Exception error) => error is PersistenceException ? error.Message
        : "Chưa thể mở phân hệ. Kiểm tra dữ liệu và các script tích hợp, sau đó kết nối lại.";
    protected override void Dispose(bool disposing)
    {
        if (disposing) { AppSqlConnection.SessionExpired -= OnSessionExpired; AppSqlConnection.SessionRetryBlocked -= OnSessionExpired; lifetime.Cancel(); ClearPage(); lifetime.Dispose(); if (passwordLogin) _ = new PasswordLogin(connectionString).SignOutAsync(); }
        base.Dispose(disposing);
        if (disposing) { foreach (var font in ownedFonts) font.Dispose(); ownedFonts.Clear(); }
    }
}
