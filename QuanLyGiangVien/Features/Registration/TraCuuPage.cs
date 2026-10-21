// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/TraCuuPage.cs.txt.
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration
{
    internal sealed class TraCuuPage : ModulePage
    {
        private readonly TextBox keyword = new TextBox { Name = "txtTuKhoa", Width = AppTheme.S(240), MaxLength = 100, Margin = new Padding(AppTheme.S(4), AppTheme.S(10), AppTheme.S(6), AppTheme.S(4)) };
        private readonly CheckBox conCho = new CheckBox { Name = "chkConCho", Text = "Chỉ lớp còn chỗ", AutoSize = true, Margin = new Padding(AppTheme.S(12), AppTheme.S(12), AppTheme.S(8), AppTheme.S(4)) };
        private readonly DataGridView grid = Ui.Grid();
        private readonly Button register = Ui.Button("Thêm vào giỏ nháp", true);
        private readonly Label summary = Ui.Label("");
        private readonly Button precheck = Ui.Button("Kiểm tra điều kiện");
        private readonly TextBox conditions = new() { Name = "lblAcademicConditions", AccessibleName = "Kết quả kiểm tra điều kiện học vụ", Dock = DockStyle.Bottom, Height = AppTheme.S(116), BackColor = AppTheme.Lilac, BorderStyle = BorderStyle.None, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private DataTable? data;
        public TraCuuPage(RegistrationWorkspace host) : base(host)
        {
            grid.Name = "dgvLopMo"; register.Name = "btnDangKy"; grid.MultiSelect = true;
            precheck.Name = "btnAcademicPrecheck";
            var bar = Ui.Toolbar(); var search = Ui.Button("Tìm kiếm"); search.Name = "btnTimKiem";
            bar.Controls.AddRange(new Control[] { Ui.Label("Mã lớp / mã HP / tên HP"), keyword, conCho, search });
            var bottom = Ui.Toolbar(); bottom.Dock = DockStyle.Bottom;
            bottom.Controls.AddRange(new Control[] { precheck, register, summary });
            Controls.Add(grid); Controls.Add(conditions); Controls.Add(bottom); Controls.Add(bar);
            register.Visible = Service.User.LaSinhVien;
            search.Click += async (s, e) => await Host.ExecuteAsync(RefreshDataAsync);
            keyword.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) {
                e.SuppressKeyPress = true; await Host.ExecuteAsync(RefreshDataAsync); } };
            grid.SelectionChanged += (s, e) => ApplyDeadline();
            register.Click += async (s, e) => await Host.ExecuteAsync(RegisterAsync);
            precheck.Click += async (_, _) => await Host.ExecuteAsync(async () => { await CheckAsync(); });
            grid.SelectionChanged += (_, _) => { conditions.Text = "Chọn một hoặc nhiều lớp (Ctrl/Shift), rồi kiểm tra điều kiện. Kết quả kiểm tra không giữ chỗ; hệ thống kiểm tra lại khi gửi."; };
        }
        public override async Task RefreshDataAsync()
        {
            ClearData(); summary.Text = "Đang tải…";
            var fresh = await Service.TraCuuAsync(Host.MaHocKy, keyword.Text, conCho.Checked);
            if (IsDisposed) { fresh.Dispose(); return; }
            data = fresh;
            Ui.Bind(grid, data); summary.Text = data.Rows.Count + " lớp • Ctrl/Shift để chọn nhóm song hành";
            ApplyDeadline();
        }
        public override void ClearData()
        {
            Ui.Bind(grid, null); data?.Dispose(); data = null; summary.Text = ""; ApplyDeadline();
        }
        public override void ApplyDeadline()
        {
            var row = Ui.Current(grid);
            register.Enabled = Service.User.LaSinhVien && row != null;
            precheck.Enabled = Service.User.LaSinhVien && row != null;
        }
        private string[] Selection() => grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => Convert.ToString(row.Cells["ma_lhp"].Value)!).Order(StringComparer.Ordinal).ToArray();
        private async Task<bool> CheckAsync()
        {
            using var issues = await Host.Workflow.PrecheckAsync(Host.MaHocKy, Selection(), []);
            if (IsDisposed) return false;
            conditions.Text = string.Join(Environment.NewLine, issues.Rows.Cast<DataRow>().Select(row =>
                ((bool)row["blocking"] ? "Cần xử lý: " : "• ") + Convert.ToString(row["message"])));
            return !issues.Rows.Cast<DataRow>().Any(row => (bool)row["blocking"]);
        }
        private async Task RegisterAsync()
        {
            var codes = Selection(); if (codes.Length == 0) return;
            if (Host.PendingStore().Load() is not null) throw new InvalidOperationException("Cần tra cứu yêu cầu đang chờ trong Giỏ nháp trước khi sửa giỏ.");
            using var cart = await Host.Workflow.CartAsync(Host.MaHocKy);
            var saved = cart.Rows[0];
            var prior = System.Text.Json.JsonSerializer.Deserialize<string[]>(Convert.ToString(saved["class_json"])!)!;
            using var result = await Host.Workflow.SaveCartAsync(Host.MaHocKy, prior.Concat(codes).Distinct(StringComparer.OrdinalIgnoreCase),
                saved.IsNull("version") ? null : (byte[])saved["version"]);
            if (!IsDisposed) await Host.OpenCartAsync();
        }
    }
}
