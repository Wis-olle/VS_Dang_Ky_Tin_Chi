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
    internal string MaHocKy => terms.SelectedValue as string ?? "";
    internal bool ChoDangKy => deadlineValue?.CanRegister(serverAge.Elapsed) == true;
    internal bool ChoHuy => deadlineValue?.CanCancel(serverAge.Elapsed) == true;
    public bool HasUnsavedChanges => false;
    public bool CanLeave() => !busy;
    private readonly DatabaseIdentity identity;
    private readonly Action dataChanged;
    private readonly bool reports;
    private readonly ComboBox terms = new() { Name = "cboHocKy", DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 280, Margin = new Padding(4, 8, 12, 4) };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Padding = new Point(20, 10) };
    private readonly FlowLayoutPanel top = Ui.Toolbar();
    private readonly Label deadline = new() { Name = "lblRegistrationDeadline", AutoSize = true, Dock = DockStyle.Top,
        Padding = new Padding(18, 12, 18, 12), BackColor = AppTheme.Lilac };
    private readonly Label status = new() { Name = "lblRegistrationStatus", Dock = DockStyle.Bottom, Height = 36,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Stopwatch serverAge = new();
    private RegistrationDeadline? deadlineValue;
    private DataTable? termData;
    private bool initialized, busy, polling;
    private TimeSpan nextPoll;

    protected RegistrationWorkspace(string connectionString, DatabaseIdentity identity, Action dataChanged, bool reports)
    {
        this.identity = identity; this.dataChanged = dataChanged; this.reports = reports;
        Service = new(new RegistrationRepository(connectionString));
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas; ForeColor = AppTheme.Ink;
        Font = Ui.OwnFont(this, AppTheme.Font(10)); Padding = new Padding(16);
        var title = new Label { Text = reports ? "Kết quả đăng ký & thống kê" : "Đăng ký học phần",
            Name = "lblRegistrationHeading", Dock = DockStyle.Top, Height = 62,
            Font = AppTheme.Font(20, true), Padding = new Padding(16, 12, 0, 0) };
        Ui.OwnFont(title, title.Font);
        var refresh = Ui.Button("Làm mới"); refresh.Name = "btnLamMoiDangKy";
        top.Controls.AddRange([Ui.Label("Học kỳ"), terms, refresh,
            Ui.Label(identity.DisplayName + " • " + (identity.StudentId ?? "Phòng đào tạo"))]);
        Controls.Add(tabs); Controls.Add(status); Controls.Add(deadline); Controls.Add(top); Controls.Add(title);
        refresh.Click += async (_, _) => await ReloadAsync();
        terms.SelectedIndexChanged += async (_, _) => {
            if (initialized && !busy) await ExecuteAsync(async () => {
                ClearPages(); await RefreshCurrentAsync();
            });
        };
        tabs.SelectedIndexChanged += async (_, _) => {
            if (initialized && !busy) await ExecuteAsync(RefreshCurrentAsync);
        };
        timer.Tick += OnClockTick;
        VisibleChanged += (_, _) => { if (Visible && initialized) timer.Start(); else timer.Stop(); };
    }

    public Task ReloadAsync() => ReloadForSemesterAsync(null);

    internal Task ReloadForSemesterAsync(string? preferredSemester) => ExecuteAsync(async () => {
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
        if (tabs.TabCount == 0)
        {
            if (!reports) AddPage("Tra cứu lớp mở", new TraCuuPage(this));
            AddPage("Kết quả đăng ký", new KetQuaPage(this));
            if (reports) AddPage("Thống kê theo lớp", new ThongKePage(this));
        }
        initialized = true;
        await RefreshCurrentAsync();
        if (Visible) timer.Start();
    });

    private void AddPage(string title, ModulePage page)
    { var tab = new TabPage(title) { BackColor = Color.White }; tab.Controls.Add(page); tabs.TabPages.Add(tab); }

    private void ClearPages()
    {
        foreach (TabPage page in tabs.TabPages) ((ModulePage)page.Controls[0]).ClearData();
    }

    internal async Task RefreshCurrentAsync()
    {
        if (tabs.SelectedTab?.Controls[0] is ModulePage current) current.ClearData();
        await RefreshDeadlineAsync();
        if (!IsDisposed && tabs.SelectedTab?.Controls[0] is ModulePage page && MaHocKy.Length > 0)
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
        deadline.Text = $"{(ChoDangKy ? "ĐANG NHẬN ĐĂNG KÝ" : "NGOÀI HẠN / ĐÃ ĐÓNG")} • " +
            $"{(ChoHuy ? "Còn hạn hủy" : "Không thể hủy")} • Giờ máy chủ: {value.Now(serverAge.Elapsed):dd/MM/yyyy HH:mm:ss} (Việt Nam)\r\n" +
            $"Đăng ký: {value.Start:dd/MM/yyyy HH:mm} → {value.End:dd/MM/yyyy HH:mm} | Hạn hủy: {value.CancelEnd:dd/MM/yyyy HH:mm}";
        ApplyDeadlines();
    }

    private void ApplyDeadlines()
    {
        foreach (TabPage page in tabs.TabPages) ((ModulePage)page.Controls[0]).ApplyDeadline();
    }

    private async void OnClockTick(object? sender, EventArgs e)
    {
        if (!Visible || !initialized || busy || IsDisposed) return;
        UpdateClock();
        if (polling || deadlineValue is null || serverAge.Elapsed < nextPoll) return;
        polling = true;
        try { await ExecuteAsync(RefreshDeadlineAsync); }
        finally { polling = false; }
    }

    internal async Task ExecuteAsync(Func<Task> action)
    {
        if (busy || IsDisposed) return;
        busy = true; top.Enabled = tabs.Enabled = false; UseWaitCursor = true; status.Text = "Đang xử lý…";
        try { await action(); if (!IsDisposed) status.Text = "Sẵn sàng"; }
        catch (Exception error)
        {
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
            if (!IsDisposed) { top.Enabled = tabs.Enabled = true; UseWaitCursor = false; ApplyDeadlines(); }
        }
    }

    internal void NotifyDataChanged() => dataChanged();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Tick -= OnClockTick; timer.Dispose();
            ClearPages(); terms.DataSource = null; termData?.Dispose(); termData = null;
            Ui.DisposeOwnedFonts(this);
        }
        base.Dispose(disposing);
    }
}
