using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class RegistrationCartPage : ModulePage
{
    private readonly DataGridView additions = Ui.Grid(), current = Ui.Grid(), combinations = Ui.Grid();
    private readonly TextBox details = new() { Name = "txtCartDetails", Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = AppTheme.S(104), BackColor = AppTheme.Lilac, BorderStyle = BorderStyle.None };
    private readonly Button send = Ui.Button("Gửi thay đổi", true), check = Ui.Button("Kiểm tra"),
        remove = Ui.Button("Bỏ khỏi giỏ"), lookup = Ui.Button("Tra cứu kết quả"), retry = Ui.Button("Gửi lại cùng yêu cầu");
    private readonly Label summary = Ui.Label("Giỏ nháp không giữ chỗ.");
    private readonly TabControl plans = new() { Dock = DockStyle.Fill };
    private DataTable? cartData, activeData, combinationData;
    private ImmutableArray<string> codes = [];
    private byte[]? version;
    private PendingRegistration? pending;
    private PendingRegistrationStore? pendingStore;

    public RegistrationCartPage(RegistrationWorkspace host) : base(host)
    {
        Name = "RegistrationCartPage"; additions.Name = "dgvCart"; current.Name = "dgvSwapOld";
        combinations.Name = "dgvCombinations"; additions.MultiSelect = current.MultiSelect = true;
        AddTab("Lớp trong giỏ", additions); AddTab("Chọn lớp cũ để hủy / đổi", current); AddTab("Tổ hợp LT–TH hợp lệ", combinations);
        var actions = Ui.Toolbar(); actions.Dock = DockStyle.Bottom; actions.AutoSize = false; actions.Height = AppTheme.S(52);
        send.Name = "btnCartSubmit"; check.Name = "btnCartPrecheck"; lookup.Name = "btnRequestLookup";
        actions.Controls.AddRange([remove, check, send, lookup, retry]);
        Controls.Add(plans); Controls.Add(details); Controls.Add(actions);
        SizeChanged += (_, _) => details.Height = AppTheme.S(Height < AppTheme.S(440) ? 48 : 104);
        foreach (var grid in new[] { additions, current })
        {
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(AppTheme.S(3), AppTheme.S(2), AppTheme.S(3), AppTheme.S(2));
            grid.DefaultCellStyle.Padding = new Padding(AppTheme.S(3));
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = AppTheme.S(52);
        }
        remove.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            var selected = additions.SelectedRows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells["ma_lhp"].Value)).ToHashSet();
            await SaveAsync(codes.Where(c => !selected.Contains(c))); await RefreshDataAsync();
        });
        check.Click += async (_, _) => await Host.ExecuteAsync(CheckAsync);
        send.Click += async (_, _) => await Host.ExecuteAsync(SubmitNewAsync);
        retry.Click += async (_, _) => await Host.ExecuteAsync(SendPendingAsync);
        lookup.Click += async (_, _) => await Host.ExecuteAsync(LookupPendingAsync);
        combinations.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || pending != null) return;
            await Host.ExecuteAsync(async () =>
            {
                if (Ui.Current(combinations) is not { } row) return;
                var linked = JsonSerializer.Deserialize<string[]>(Convert.ToString(row["class_json"])!)!;
                await SaveAsync(codes.Concat(linked).Distinct(StringComparer.OrdinalIgnoreCase)); await RefreshDataAsync();
            });
        };
        current.SelectionChanged += (_, _) => UpdateActions();
    }
    private void AddTab(string title, Control control)
    { var tab = new TabPage(title); tab.Controls.Add(control); plans.TabPages.Add(tab); }
    private PendingRegistrationStore Store()
        => Host.PendingStore();
    public override async Task RefreshDataAsync()
    {
        pendingStore = Store(); pending = pendingStore.Load();
        using var saved = await Host.Workflow.CartAsync(Host.MaHocKy);
        var row = saved.Rows[0]; version = row.IsNull("version") ? null : (byte[])row["version"];
        codes = (JsonSerializer.Deserialize<string[]>(Convert.ToString(row["class_json"])!) ?? []).ToImmutableArray();
        using var offered = await Service.TraCuuAsync(Host.MaHocKy, "", false);
        var active = await Service.KetQuaAsync(Host.MaHocKy, null, false);
        var components = await Host.Workflow.ComponentsAsync(Host.MaHocKy);
        if (IsDisposed) { active.Dispose(); components.Dispose(); return; }
        Ui.Bind(additions, null); cartData?.Dispose(); cartData = offered.Clone();
        cartData.Columns.Add("cart_note", typeof(string));
        foreach (var code in codes)
        {
            var source = offered.Rows.Cast<DataRow>().FirstOrDefault(r => Convert.ToString(r["ma_lhp"]) == code);
            if (source != null)
            {
                cartData.ImportRow(source); var entry = cartData.Rows[^1];
                entry["cart_note"] = active.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["ma_lhp"]) == code) ? "Lớp đã đăng ký"
                    : Convert.ToInt32(source["so_cho_con"]) <= 0 ? "Đã hết chỗ"
                    : Convert.ToString(source["lich_hoc"])?.Contains("Chưa xếp lịch") == true ? "Chưa có lịch học" : "Trong giỏ; cần kiểm tra trước khi gửi";
            }
            else { var missing = cartData.NewRow(); missing["ma_lhp"] = code; missing["ten_hp"] = "Không còn khả dụng"; missing["cart_note"] = "Lớp đã đóng hoặc không còn nhận đăng ký"; cartData.Rows.Add(missing); }
        }
        Ui.Bind(additions, cartData); Ui.Bind(current, null); activeData?.Dispose(); activeData = active; Ui.Bind(current, activeData); current.ClearSelection();
        VisibleColumns(additions, ["ma_lhp", "ma_hp", "ten_hp", "so_tin_chi", "lich_hoc", "so_cho_con", "cart_note"]);
        VisibleColumns(current, ["ma_dk", "ma_lhp", "ten_hp", "so_tin_chi", "lich_hoc", "trang_thai"]);
        Ui.Bind(combinations, null); combinationData?.Dispose(); combinationData = components; Ui.Bind(combinations, components);
        if (combinations.Columns.Contains("version")) combinations.Columns["version"]!.Visible = false;
        details.Text = pending == null
            ? "Giỏ đã lưu theo sinh viên / học kỳ; không giữ chỗ. Chọn lớp cũ ở tab thứ hai nếu muốn hủy hoặc đổi. Cả thay đổi được ghi cùng giao dịch.\r\nNhấp đúp một tổ hợp để thêm đủ thành phần. Lịch dự kiến nằm trong cột Lịch học. Bấm Kiểm tra để xem tổng tín chỉ và điều kiện."
            : $"Kết quả chưa xác định • Mã yêu cầu: {pending.RequestId}\r\nTra cứu kết quả hoặc gửi lại cùng yêu cầu. Nội dung yêu cầu được giữ nguyên qua lần đăng nhập sau.";
        summary.Text = $"{codes.Length} thành phần trong giỏ • Không giữ chỗ"; UpdateActions();
        AccessibleDescription = summary.Text;
    }
    private async Task SaveAsync(IEnumerable<string> selected)
    {
        if (pending != null) throw new InvalidOperationException("Cần đối soát yêu cầu đang chờ trước khi sửa giỏ.");
        using var result = await Host.Workflow.SaveCartAsync(Host.MaHocKy, selected, version);
    }
    private static void VisibleColumns(DataGridView grid, string[] names)
    {
        foreach (DataGridViewColumn column in grid.Columns) column.Visible = names.Contains(column.Name);
    }
    private long[] RemovalIds() => current.SelectedRows.Cast<DataGridViewRow>()
        .Where(r => Convert.ToString(r.Cells["trang_thai"].Value) == "DANG_KY")
        .Select(r => Convert.ToInt64(r.Cells["ma_dk"].Value)).Order().ToArray();
    private async Task CheckAsync()
    {
        using var checks = await Host.Workflow.PrecheckAsync(Host.MaHocKy, codes, RemovalIds());
        if (!IsDisposed) details.Text = string.Join(Environment.NewLine, checks.Rows.Cast<DataRow>()
            .Select(r => ((bool)r["blocking"] ? "Cần xử lý: " : "• ") + r["message"]));
    }
    private async Task SubmitNewAsync()
    {
        if (pending != null) return;
        var removals = RemovalIds(); if (codes.IsEmpty && removals.Length == 0) return;
        await CheckAsync();
        var proposed = "Thêm: " + (codes.IsEmpty ? "không" : string.Join(", ", codes)) + "\r\nBỏ mã đăng ký: " +
            (removals.Length == 0 ? "không" : string.Join(", ", removals));
        if (MessageBox.Show(this, proposed + "\r\nChỉ ghi khi cả kế hoạch hợp lệ. Xác nhận gửi?", "Xác nhận đăng ký / đổi lớp",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var request = new PendingRegistration(Guid.NewGuid(), Host.MaHocKy, codes, removals.ToImmutableArray(), version);
        pendingStore ??= Store(); pendingStore.Save(request); pending = request; UpdateActions(); await SendPendingAsync();
    }
    private async Task SendPendingAsync()
    {
        if (pending == null) return;
        var request = pending;
        // Retry must never send an id which was not durably stored, including
        // the case where another application instance has changed the marker.
        pendingStore ??= Store(); pendingStore.Save(request);
        details.Text = $"Đang gửi yêu cầu {pending.RequestId}. Đợi kết quả máy chủ…";
        try
        {
            using var result = await Host.Workflow.SubmitAsync(request.RequestId, request.Term, request.Add, request.Remove, request.CartVersion);
            await CompleteAsync(result);
        }
        catch (PersistenceException e) when (e.Kind == PersistenceFailure.BusinessRejected)
        {
            pendingStore!.Clear(request.RequestId); pending = null; details.Text = "Máy chủ từ chối; không ghi thay đổi. " + e.Message; UpdateActions();
        }
        catch
        {
            details.Text = pending is null ? $"Máy chủ đã xác nhận giao dịch {request.RequestId}; cần tải lại giao diện để đối soát."
                : $"Chưa xác định kết quả yêu cầu {request.RequestId}. Tra cứu hoặc gửi lại cùng yêu cầu; không tạo yêu cầu mới.";
            UpdateActions(); throw;
        }
    }
    private async Task LookupPendingAsync()
    {
        if (pending == null) return;
        using var result = await Host.Workflow.ResultAsync(pending.RequestId);
        if (result.Rows.Count == 0) { details.Text = $"Chưa có kết quả đã ghi cho {pending.RequestId}. Có thể gửi lại cùng yêu cầu."; return; }
        await CompleteAsync(result);
    }
    private async Task CompleteAsync(DataTable result)
    {
        if (result.Rows.Count != 1) throw new InvalidOperationException("Máy chủ chưa xác nhận kết quả. Hãy tra cứu lại.");
        var id = pending!.RequestId; pendingStore!.Clear(id); pending = null; Host.NotifyDataChanged();
        await RefreshDataAsync(); details.Text = $"Máy chủ đã xác nhận giao dịch {id}. Xem Kết quả đăng ký, thời khóa biểu và thông báo để đối soát.";
    }
    private void UpdateActions()
    {
        var waiting = pending != null; remove.Enabled = check.Enabled = !waiting;
        send.Enabled = !waiting && (!codes.IsEmpty || RemovalIds().Length > 0);
        retry.Enabled = lookup.Enabled = waiting; additions.Enabled = current.Enabled = combinations.Enabled = !waiting;
    }
    public override void ApplyDeadline() => UpdateActions();
    public override void ClearData()
    {
        Ui.Bind(additions, null); Ui.Bind(current, null); Ui.Bind(combinations, null);
        cartData?.Dispose(); activeData?.Dispose(); combinationData?.Dispose(); cartData = activeData = combinationData = null;
    }
}
