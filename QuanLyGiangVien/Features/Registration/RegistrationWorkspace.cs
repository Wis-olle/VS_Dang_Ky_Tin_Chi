// MainForm host adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/MainForm.cs.txt.
using System.Data;
using System.Diagnostics;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

public sealed class RegistrationPage(string connectionString, DatabaseIdentity identity, Action dataChanged)
    : RegistrationWorkspace(connectionString, identity, dataChanged, false);

public sealed class ReportsPage(string connectionString, DatabaseIdentity identity, Action dataChanged)
    : RegistrationWorkspace(connectionString, identity, dataChanged, true);

/// <summary>Embeds Huệ's original tabs in the common application form.</summary>
public abstract class RegistrationWorkspace : UserControl
{
    internal DangKyService Service { get; }
    internal AcademicRepository Academics { get; }
    internal CampusEventsRepository EventLog { get; }
    internal RegistrationWorkflowRepository Workflow { get; }
    internal string ConnectionString { get; }
    internal DatabaseIdentity Identity => identity;
    internal string MaHocKy => terms.SelectedValue as string ?? "";
    internal bool ChoDangKy => deadlineValue?.CanRegister(serverAge.Elapsed) == true;
    internal bool ChoHuy => deadlineValue?.CanCancel(serverAge.Elapsed) == true;
    public bool HasUnsavedChanges => tabs.TabPages.Cast<TabPage>().Any(t => ((ModulePage)t.Controls[0]).HasUnsavedChanges);
    internal bool IsBusy => busy;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<string, bool>? ConfirmAction { get; set; }
    internal bool Confirm(string message) => ConfirmAction?.Invoke(message) ?? MessageBox.Show(this, message, "Campus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    public bool CanLeave() => !busy && (!HasUnsavedChanges || Confirm("Có nội dung biểu mẫu chưa gửi hoặc chưa lưu. Rời trang và bỏ nội dung này?"));
    private readonly DatabaseIdentity identity;
    private readonly Action dataChanged;
    private readonly bool reports;
    private readonly ComboBox terms = new() { Name = "cboHocKy", DropDownStyle = ComboBoxStyle.DropDownList,
        Width = AppTheme.S(280), Margin = new Padding(AppTheme.S(4), AppTheme.S(8), AppTheme.S(12), AppTheme.S(4)) };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Padding = new Point(AppTheme.S(14), AppTheme.S(8)) };
    private readonly ComboBox section = new() { Name = "cboRegistrationModule", DropDownStyle = ComboBoxStyle.DropDownList,
        Width = AppTheme.S(245), DropDownWidth = AppTheme.S(320), Margin = new Padding(AppTheme.S(4), AppTheme.S(8), AppTheme.S(8), AppTheme.S(4)) };
    private readonly Panel top = new() { Dock = DockStyle.Top, Height = AppTheme.S(72), BackColor = AppTheme.Canvas };
    private readonly FlowLayoutPanel taskNavigation = new() { Name = "registrationTaskNavigation", Dock = DockStyle.Left,
        Width = AppTheme.S(184), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly Dictionary<TabPage, Button> taskButtons = [];
    private readonly Label heading = new() { Name = "lblRegistrationHeading", AutoEllipsis = true, ForeColor = AppTheme.Ink };
    private readonly Label subtitle = new() { AutoEllipsis = true, ForeColor = AppTheme.Muted };
    private readonly ToolTip tips = new();
    private readonly Label deadline = new() { Name = "lblRegistrationDeadline", AutoSize = false, Dock = DockStyle.Top,
        Height = AppTheme.S(30), Padding = new Padding(AppTheme.S(4), AppTheme.S(5), 0, 0), ForeColor = AppTheme.Muted, BackColor = AppTheme.Canvas };
    private readonly Label status = new() { Name = "lblRegistrationStatus", Dock = DockStyle.Bottom, Height = AppTheme.S(26),
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(AppTheme.S(15), 0, 0, 0) };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Stopwatch serverAge = new();
    private RegistrationDeadline? deadlineValue;
    private DataTable? termData;
    private bool initialized, busy, polling;
    private bool changingTerm;
    private string activeTerm = "";
    private TimeSpan nextPoll;

    protected RegistrationWorkspace(string connectionString, DatabaseIdentity identity, Action dataChanged, bool reports)
    {
        this.identity = identity; this.dataChanged = dataChanged; this.reports = reports;
        ConnectionString = connectionString;
        Service = new(new RegistrationRepository(connectionString));
        Academics = new(connectionString);
        EventLog = new(connectionString); Workflow = new(connectionString);
        AutoScaleMode = AutoScaleMode.None;
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas; ForeColor = AppTheme.Ink;
        Font = Ui.OwnFont(this, AppTheme.Font(10)); Padding = new Padding(AppTheme.S(12));
        heading.Font = Ui.OwnFont(heading, AppTheme.Font(18, true));
        subtitle.Font = Ui.OwnFont(subtitle, AppTheme.Font(9));
        var refresh = Ui.Button("Làm mới"); refresh.Name = "btnLamMoiDangKy";
        var dates = Ui.Button("Thời hạn"); dates.Name = "btnRegistrationDates";
        dates.Click += (_, _) => ShowDates();
        section.Visible = false; section.TabStop = false;
        top.Controls.AddRange([heading, subtitle, terms, refresh, dates, section]);
        ShowcaseStyle.Apply(terms);
        top.Resize += (_, _) => LayoutHeading(refresh, dates);
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(AppTheme.S(12), 0, 0, 0) };
        tabs.Name = "registrationPages";
        content.Controls.Add(ShowcaseTabs.HideHeader(tabs));
        content.Controls.Add(status); content.Controls.Add(deadline); content.Controls.Add(top);
        Controls.Add(content); Controls.Add(taskNavigation);
        LayoutHeading(refresh, dates);
        refresh.Click += async (_, _) => await ReloadAsync();
        terms.SelectedIndexChanged += async (_, _) => await ChangeTermAsync();
        section.SelectedIndexChanged += (_, _) => { if (section.SelectedIndex >= 0 && section.SelectedIndex != tabs.SelectedIndex) tabs.SelectedIndex = section.SelectedIndex; };
        tabs.SelectedIndexChanged += async (_, _) => {
            if (section.SelectedIndex != tabs.SelectedIndex) section.SelectedIndex = tabs.SelectedIndex;
            UpdateTaskNavigation();
            if (initialized && !busy) await ExecuteAsync(RefreshCurrentAsync);
        };
        timer.Tick += OnClockTick;
        VisibleChanged += (_, _) => { if (Visible && initialized) timer.Start(); else timer.Stop(); };
    }

    private void LayoutHeading(Button refresh, Button dates)
    {
        int s(int n) => AppTheme.S(n);
        int right = top.ClientSize.Width;
        dates.AutoSize = refresh.AutoSize = false;
        dates.SetBounds(Math.Max(0, right - s(88)), s(14), s(88), s(38));
        refresh.SetBounds(Math.Max(0, right - s(192)), s(14), s(96), s(38));
        var termSurface = terms.Parent is SoftInputSurface surface ? (Control)surface : terms;
        termSurface.SetBounds(Math.Max(0, right - s(420)), s(15), s(216), s(36));
        int titleWidth = Math.Max(s(160), right - s(436));
        heading.SetBounds(s(4), s(4), titleWidth, s(34));
        subtitle.SetBounds(s(4), s(42), titleWidth, s(24));
    }

    private static string TaskTitle(string title) => title switch
    {
        "Tra cứu lớp mở" => "Tìm lớp học", "Giỏ nháp & đổi lớp" => "Giỏ đăng ký",
        "Gợi ý thời khóa biểu" => "Gợi ý lịch học", "Hàng chờ nhận chỗ" => "Hàng chờ nhận chỗ",
        "Học vụ của tôi" => "Hồ sơ học vụ", "CTĐT & kế hoạch học" => "Kế hoạch học tập",
        "Đơn ngoại lệ" => "Đơn đề nghị", "Đợt, quota & hàng chờ" => "Đợt & phân bổ chỗ",
        "Nhu cầu & sức khỏe" => "Tổng quan vận hành", _ => title
    };

    private void UpdateTaskNavigation()
    {
        foreach (var (page, button) in taskButtons)
        {
            bool selected = page == tabs.SelectedTab;
            button.BackColor = selected ? AppTheme.AccentSoft : AppTheme.Canvas;
            button.ForeColor = selected ? AppTheme.PrimaryPressed : AppTheme.Muted;
            button.AccessibleDescription = selected ? "Đang mở" : "Mở chức năng";
        }
        if (tabs.SelectedTab is not {} current) return;
        heading.Text = TaskTitle(current.Text);
        subtitle.Text = current.Controls[0] switch
        {
            SchedulePlannerPage => "Chọn lịch học phù hợp với bạn",
            AcademicPlanningPage => "Theo dõi tiến độ và chuẩn bị học kỳ tiếp theo",
            RegistrationCartPage => "Kiểm tra lựa chọn trước khi gửi đăng ký",
            SeatsAndWaitlistPage => reports ? "Quản lý đợt, nhóm ưu tiên và lời mời" : "Theo dõi lời mời và nhận chỗ còn trống",
            OperationsDashboardPage => "Nắm nhu cầu học và những việc cần xử lý",
            _ => reports ? "Không gian quản lý đào tạo" : "Không gian học tập của bạn"
        };
    }

    private void ShowDates()
    {
        if (deadlineValue is not {} value) return;
        MessageBox.Show(this, $"{terms.Text}\n\nĐăng ký: {value.Start:dd/MM/yyyy HH:mm} → {value.End:dd/MM/yyyy HH:mm}\nHạn hủy: {value.CancelEnd:dd/MM/yyyy HH:mm}\nThời gian hệ thống: {value.Now(serverAge.Elapsed):dd/MM/yyyy HH:mm:ss} (Việt Nam)",
            "Thời hạn học kỳ", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public Task ReloadAsync() => ReloadForSemesterAsync(null);

    internal Task ReloadForSemesterAsync(string? preferredSemester) => ExecuteAsync(async () => {
        if (initialized && HasUnsavedChanges && !Confirm("Có nội dung chưa lưu. Tải lại và bỏ các bản nháp biểu mẫu?")) return;
        await Service.InitializeAsync(identity);
        if (IsDisposed) return;
        if (reports && !Service.User.LaDaoTao)
            throw new InvalidOperationException("Bạn không có quyền xem báo cáo phòng đào tạo.");
        if (!reports && !Service.User.LaSinhVien)
            throw new InvalidOperationException("Trang đăng ký học phần dành cho sinh viên.");
        var selected = preferredSemester ?? MaHocKy;
        var freshTerms = await Service.HocKyAsync();
        if (IsDisposed) { freshTerms.Dispose(); return; }
        initialized = false;
        terms.DataSource = null; termData?.Dispose(); termData = freshTerms;
        terms.DisplayMember = "ten_hk"; terms.ValueMember = "ma_hk"; terms.DataSource = termData;
        if (freshTerms.Rows.Cast<DataRow>().Any(row => Convert.ToString(row["ma_hk"]) == selected)) terms.SelectedValue = selected;
        activeTerm = MaHocKy;
        ResetPages();
        initialized = true;
        await RefreshCurrentAsync();
        if (Visible) timer.Start();
    });

    private async Task ChangeTermAsync()
    {
        if (!initialized || busy || changingTerm || MaHocKy == activeTerm) return;
        var previous = activeTerm;
        if (HasUnsavedChanges && !Confirm("Có nội dung chưa lưu của học kỳ hiện tại. Đổi học kỳ và bỏ các bản nháp biểu mẫu?"))
        {
            changingTerm = true;
            try { terms.SelectedValue = previous; }
            finally { changingTerm = false; }
            return;
        }
        await ExecuteAsync(async () =>
        {
            activeTerm = MaHocKy;
            ResetPages();
            await RefreshCurrentAsync();
        });
    }

    private void ResetPages()
    {
        var wasInitialized = initialized;
        var selected = tabs.SelectedIndex;
        initialized = false;
        foreach (var old in tabs.TabPages.Cast<TabPage>().ToArray()) { tabs.TabPages.Remove(old); old.Dispose(); }
        foreach (Control old in taskNavigation.Controls.Cast<Control>().ToArray()) { taskNavigation.Controls.Remove(old); Ui.DisposeOwnedFonts(old); old.Dispose(); }
        taskButtons.Clear();
        section.Items.Clear();
        var caption = Ui.Label(reports ? "QUẢN LÝ ĐÀO TẠO" : "KHÔNG GIAN HỌC TẬP");
        caption.Font = Ui.OwnFont(caption, AppTheme.Font(8, true)); caption.ForeColor = AppTheme.Muted;
        caption.Margin = new(0, AppTheme.S(8), 0, AppTheme.S(14)); taskNavigation.Controls.Add(caption);
        // Fresh controls bind every draft and captured row version to this semester.
        if (!reports) AddPage("Tra cứu lớp mở", new TraCuuPage(this));
        if (!reports) AddPage("Giỏ nháp & đổi lớp", new RegistrationCartPage(this));
        if (!reports) AddPage("Gợi ý thời khóa biểu", new SchedulePlannerPage(this));
        AddPage(reports ? "Đợt, quota & hàng chờ" : "Hàng chờ nhận chỗ", new SeatsAndWaitlistPage(this));
        AddPage("Kết quả đăng ký", new KetQuaPage(this));
        if (!reports) AddPage("Học vụ của tôi", new StudentAcademicPage(this));
        AddPage(reports ? "Cố vấn & kế hoạch" : "CTĐT & kế hoạch học", new AcademicPlanningPage(this));
        if (reports) AddPage("Thống kê theo lớp", new ThongKePage(this));
        AddPage(reports ? "Duyệt ngoại lệ" : "Đơn ngoại lệ", new ExceptionRequestsPage(this));
        if (reports) AddPage("Vận hành lớp & lịch", new ClassOperationsPage(this));
        if (reports) AddPage("Nhập dữ liệu nguồn", new DataImportPage(this));
        if (reports) AddPage("Nhu cầu & sức khỏe", new OperationsDashboardPage(this));
        AddPage("Thông báo", new NotificationsPage(this));
        if (reports) AddPage("Nhật ký & quyền", new OperationsAuditPage(this));
        tabs.SelectedIndex = Math.Clamp(selected, 0, tabs.TabCount - 1);
        section.SelectedIndex = tabs.SelectedIndex;
        UpdateTaskNavigation();
        initialized = wasInitialized;
    }

    private void AddPage(string title, ModulePage page)
    {
        var tab = new TabPage(title) { BackColor = Color.White }; tab.Controls.Add(page); section.Items.Add(title); tabs.TabPages.Add(tab);
        var button = Ui.Button(TaskTitle(title)); button.Name = "navRegistration_" + page.Name;
        button.AccessibleRole = AccessibleRole.PageTab; button.AccessibleName = TaskTitle(title);
        button.AutoSize = false; button.Size = new(AppTheme.S(172), AppTheme.S(40));
        button.Margin = new(0, AppTheme.S(2), 0, AppTheme.S(2)); button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new(AppTheme.S(10), 0, AppTheme.S(4), 0);
        button.Font = Ui.OwnFont(button, AppTheme.Font(9));
        button.Click += (_, _) => tabs.SelectedTab = tab;
        taskButtons.Add(tab, button); taskNavigation.Controls.Add(button);
    }

    internal async Task OpenCartAsync()
    {
        var tab = tabs.TabPages.Cast<TabPage>().Single(t => t.Controls[0] is RegistrationCartPage);
        tabs.SelectedTab = tab; await RefreshCurrentAsync();
    }

    private void ClearPages()
    {
        foreach (TabPage page in tabs.TabPages) ((ModulePage)page.Controls[0]).ClearData();
    }

    internal async Task RefreshCurrentAsync()
    {
        await RefreshDeadlineAsync();
        if (!IsDisposed && tabs.SelectedTab?.Controls[0] is ModulePage page && !page.HasUnsavedChanges && MaHocKy.Length > 0)
            await page.RefreshDataAsync();
    }

    private async Task RefreshDeadlineAsync()
    {
        deadlineValue = null; ApplyDeadlines();
        deadline.Text = "Đang kiểm tra thời hạn trên máy chủ…";
        if (MaHocKy.Length == 0) { deadline.Text = "Chưa có học kỳ. Liên hệ phòng đào tạo."; return; }
        serverAge.Restart();
        using var table = await Service.ThoiHanAsync(MaHocKy);
        if (IsDisposed) return;
        if (table.Rows.Count != 1) throw new InvalidOperationException("Không nhận được thời hạn của học kỳ.");
        var row = table.Rows[0];
        deadlineValue = new(Convert.ToDateTime(row["tg_bat_dau"]), Convert.ToDateTime(row["tg_ket_thuc"]),
            Convert.ToDateTime(row["han_huy_dk"]), Convert.ToDateTime(row["gio_may_chu"]), Convert.ToString(row["trang_thai"])!);
        nextPoll = serverAge.Elapsed + TimeSpan.FromSeconds(30);
        UpdateClock();
    }

    private void UpdateClock()
    {
        if (deadlineValue is not { } value) return;
        var text = $"{(ChoDangKy ? "Đang mở đăng ký" : "Ngoài đợt đăng ký")}  ·  Hạn đăng ký {value.End:dd/MM HH:mm}  ·  {(ChoHuy ? "Còn hạn hủy" : "Đã hết hạn hủy")}";
        if (deadline.Text != text) deadline.Text = text;
        deadline.ForeColor = ChoDangKy ? Color.FromArgb(40, 102, 68) : AppTheme.Muted;
        tips.SetToolTip(deadline, "Bấm Thời hạn để xem đầy đủ lịch đăng ký và thời gian hệ thống.");
        ApplyDeadlines();
    }

    private void ApplyDeadlines()
    {
        foreach (TabPage page in tabs.TabPages) ((ModulePage)page.Controls[0]).ApplyDeadline();
    }

    private async void OnClockTick(object? sender, EventArgs e)
    {
        if (!Visible || !Enabled || !initialized || busy || IsDisposed) return;
        UpdateClock();
        if (polling || deadlineValue is null || serverAge.Elapsed < nextPoll) return;
        polling = true;
        try { await ExecuteAsync(RefreshDeadlineAsync); }
        finally { polling = false; }
    }

    internal async Task ExecuteAsync(Func<Task> action)
    {
        if (busy || IsDisposed) return;
        busy = true; top.Enabled = tabs.Enabled = taskNavigation.Enabled = false; UseWaitCursor = true; status.Text = "Đang xử lý…";
        try { await action(); if (!IsDisposed) status.Text = "Sẵn sàng"; }
        catch (Exception error)
        {
            if (Environment.GetEnvironmentVariable("CAMPUS_PHASE3_UI_CHECK") is { Length: > 0 } verification)
                File.AppendAllText(Path.Combine(verification, "workspace-errors.txt"), error + Environment.NewLine);
            if (Environment.GetEnvironmentVariable("CAMPUS_PHASE4_UI_CHECK") is { Length: > 0 } phaseFourVerification)
                File.AppendAllText(Path.Combine(phaseFourVerification, "workspace-errors.txt"), error + Environment.NewLine);
            // SQL repository exposes only whitelisted messages. Never display/log raw connection credentials.
            if (!IsDisposed)
            {
                status.Text = error is PersistenceException or ArgumentException or InvalidOperationException
                    ? error.Message : "Yêu cầu chưa hoàn tất. Vui lòng tải lại dữ liệu và thử lại.";
                deadlineValue = null; deadline.Text = "Chưa xác nhận được thời hạn. Bấm Làm mới để thử lại."; ApplyDeadlines();
            }
        }
        finally
        {
            busy = false;
            if (!IsDisposed) { top.Enabled = tabs.Enabled = taskNavigation.Enabled = true; UseWaitCursor = false; ApplyDeadlines(); }
        }
    }

    internal void NotifyDataChanged() => dataChanged();

    internal PendingRegistrationStore PendingStore()
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString);
        return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Campus", "PendingRegistration"),
            $"{b.DataSource.ToUpperInvariant()}|{b.InitialCatalog.ToUpperInvariant()}|{identity.AccountId}|{MaHocKy}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Tick -= OnClockTick; timer.Dispose();
            tips.Dispose();
            ClearPages(); terms.DataSource = null; termData?.Dispose(); termData = null;
            Ui.DisposeOwnedFonts(this);
        }
        base.Dispose(disposing);
    }
}
