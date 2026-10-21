using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class OperationsDashboardPage : ModulePage
{
    private readonly OperationsRepository repository;
    private readonly DataGridView grid = Ui.Grid();
    private readonly ComboBox view = PhaseThreeUi.Choices(new("Demand", "Nhu cầu & chỗ"), new("DemandDetails", "Nhu cầu chi tiết"), new("PlanRisks", "Cảnh báo kế hoạch"), new("Health", "Sức khỏe dữ liệu"),
        new("Worker", "Lịch sử xử lý hàng chờ"), new("Imports", "Lịch sử nhập dữ liệu"), new("Outbox", "Thông báo chưa chuyển"));
    private readonly Label notice = Ui.Label("Giỏ và kế hoạch không giữ chỗ. Lượt chờ, lời mời tính theo lớp; một sinh viên có thể xuất hiện ở nhiều lớp.");
    private readonly Label waitingValue = new(), offeredValue = new(), availableValue = new(), riskValue = new();
    private readonly Font headerFont;
    private DataSet? data;
    public OperationsDashboardPage(RegistrationWorkspace host) : base(host)
    {
        Name = "OperationsDashboardPage"; repository = new(host.ConnectionString); grid.Name = "dgvOperationsOverview";
        headerFont = Ui.OwnFont(grid, AppTheme.Font(9, true));
        var tools = Ui.Toolbar(); var refresh = Ui.Button("Tải tổng quan"); var work = Ui.Button("Xử lý hàng chờ"); work.Name = "btnOperationsRun";
        view.Width = AppTheme.S(230); tools.Controls.AddRange([view, refresh, work]);
        notice.AutoSize = false; notice.Dock = DockStyle.Top; notice.Height = AppTheme.S(40); notice.Padding = new(AppTheme.S(8), AppTheme.S(4), AppTheme.S(8), 0);
        notice.ForeColor = AppTheme.Muted; notice.BackColor = AppTheme.SubtleLilac;
        var metrics = new TableLayoutPanel { Name = "pnlOperationsMetrics", Dock = DockStyle.Top, Height = AppTheme.S(82), ColumnCount = 4, RowCount = 1, Padding = new(AppTheme.S(4), 0, AppTheme.S(4), AppTheme.S(4)) };
        metrics.RowStyles.Add(new(SizeType.Percent, 100));
        AddMetric(metrics, waitingValue, "Lượt chờ theo lớp", AppTheme.Lilac, 0);
        AddMetric(metrics, offeredValue, "Lời mời theo lớp", AppTheme.SubtlePeach, 1);
        AddMetric(metrics, availableValue, "Chỗ trống · lớp mở", AppTheme.Mint, 2);
        AddMetric(metrics, riskValue, "Cảnh báo kế hoạch", AppTheme.Cyan, 3);
        Controls.Add(grid); Controls.Add(notice); Controls.Add(metrics); Controls.Add(tools);
        view.SelectedIndexChanged += (_, _) => Bind(); refresh.Click += async (_, _) => await Host.ExecuteAsync(RefreshDataAsync);
        work.Click += async (_, _) => await Host.ExecuteAsync(async () => { using var result = await repository.RunOnceAsync(Host.MaHocKy); await RefreshDataAsync(); });
        grid.DataBindingComplete += (_, _) => FormatDemand();
        UpdateMetrics();
    }

    private static void AddMetric(TableLayoutPanel parent, Label value, string caption, Color fill, int column)
    {
        parent.ColumnStyles.Add(new(SizeType.Percent, 25));
        var card = new SoftSurface { Dock = DockStyle.Fill, BackColor = fill, Padding = new(AppTheme.S(12), AppTheme.S(5), AppTheme.S(8), AppTheme.S(4)), Margin = new(AppTheme.S(4), 0, AppTheme.S(4), 0) };
        value.Name = "lblOperationsMetric" + column; value.Dock = DockStyle.Top; value.Height = AppTheme.S(36);
        value.Font = Ui.OwnFont(value, AppTheme.Font(20, true)); value.ForeColor = AppTheme.Ink;
        var label = new Label { Text = caption, Dock = DockStyle.Bottom, Height = AppTheme.S(22), ForeColor = AppTheme.Muted, AutoEllipsis = true };
        label.Font = Ui.OwnFont(label, AppTheme.Font(9));
        card.Controls.Add(value); card.Controls.Add(label); parent.Controls.Add(card, column, 0);
    }
    public override async Task RefreshDataAsync()
    {
        var fresh = await repository.SnapshotAsync(Host.MaHocKy);
        if (IsDisposed) { fresh.Dispose(); return; } ClearData(); data = fresh; UpdateMetrics(); Bind();
    }

    private void UpdateMetrics()
    {
        var demand = data?.Tables["Demand"];
        long Sum(string column, bool openOnly = false) => demand?.AsEnumerable()
            .Where(row => !openOnly || Convert.ToString(row["lifecycle"]) == "REGISTRATION")
            .Sum(row => row.IsNull(column) ? 0L : Convert.ToInt64(row[column])) ?? 0;
        waitingValue.Text = demand is null ? "—" : Sum("waiting").ToString("N0");
        offeredValue.Text = demand is null ? "—" : Sum("offered").ToString("N0");
        availableValue.Text = demand is null ? "—" : Sum("available", true).ToString("N0");
        riskValue.Text = data is null ? "—" : (data.Tables["PlanRisks"]?.Rows.Count ?? 0).ToString("N0");
        riskValue.AccessibleDescription = "Số dòng cảnh báo theo học phần; không phải số sinh viên hay kết luận tốt nghiệp.";
    }

    private void Bind()
    {
        var selected = PhaseThreeUi.Choice(view);
        Ui.Bind(grid, data?.Tables[selected == "DemandDetails" ? "Demand" : selected]); FormatDemand();
    }

    private void FormatDemand()
    {
        var selected = PhaseThreeUi.Choice(view);
        if (grid.DataSource is null) return;
        if (selected == "DemandDetails") { foreach (DataGridViewColumn column in grid.Columns) column.Visible = true; return; }
        if (selected != "Demand") return;
        grid.ColumnHeadersDefaultCellStyle.Font = headerFont;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(AppTheme.S(6), AppTheme.S(4), AppTheme.S(6), AppTheme.S(4));
        string[] visible = ["ma_lhp", "ten_hp", "enrolled", "waiting", "offered", "available", "planned_students"];
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.Visible = visible.Contains(column.Name);
            column.MinimumWidth = AppTheme.S(column.Name == "ten_hp" ? 180 : 75);
            column.FillWeight = column.Name == "ten_hp" ? 190 : 75;
            column.DefaultCellStyle.WrapMode = column.Name == "ten_hp" ? DataGridViewTriState.True : DataGridViewTriState.False;
        }
        if (grid.Columns.Contains("ma_lhp")) grid.Columns["ma_lhp"]!.HeaderText = "Mã lớp";
        if (grid.Columns.Contains("enrolled")) grid.Columns["enrolled"]!.HeaderText = "Đã đăng ký";
        if (grid.Columns.Contains("planned_students")) grid.Columns["planned_students"]!.HeaderText = "SV dự kiến";
        if (grid.Columns.Contains("waiting")) grid.Columns["waiting"]!.HeaderText = "Lượt chờ";
        if (grid.Columns.Contains("offered")) grid.Columns["offered"]!.HeaderText = "Lời mời";
        if (grid.Columns.Contains("available")) grid.Columns["available"]!.HeaderText = "Chỗ trống";
        grid.AutoResizeColumnHeadersHeight();
    }

    public override void ClearData() { Ui.Bind(grid, null); data?.Dispose(); data = null; UpdateMetrics(); }
}
