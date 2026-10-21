// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/KetQuaPage.cs.txt.
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyGiangVien.Ui;


namespace QuanLyGiangVien.Features.Registration
{
    internal sealed class KetQuaPage : ModulePage
    {
        private readonly DataGridView grid = Ui.Grid();
        private readonly TextBox student = new TextBox { Name = "txtMaSv", Width = AppTheme.S(170), MaxLength = 20, Margin = new Padding(AppTheme.S(4), AppTheme.S(10), AppTheme.S(6), AppTheme.S(4)) };
        private readonly CheckBox history = new CheckBox { Text = "Hiện cả đăng ký đã hủy", AutoSize = true, Margin = new Padding(AppTheme.S(10), AppTheme.S(12), AppTheme.S(8), AppTheme.S(4)) };
        private readonly Button cancel = Ui.Button("Hủy / đổi lớp qua giỏ");
        private readonly Button checkCredits = Ui.Button("Kiểm tra tổng tín chỉ");
        private readonly TextBox creditCheck = new() { Name = "txtCreditPlanCheck", AccessibleName = "Kết quả kiểm tra tổng tín chỉ", Dock = DockStyle.Bottom, Height = AppTheme.S(96), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = AppTheme.Lilac, BorderStyle = BorderStyle.None, Visible = false };
        private readonly Label summary = Ui.Label("");
        private DataTable? data;
        public KetQuaPage(RegistrationWorkspace host) : base(host)
        {
            grid.Name = "dgvKetQua"; cancel.Name = "btnHuyDangKy";
            student.Text = Service.User.MaSv; student.ReadOnly = Service.User.LaSinhVien;
            var bar = Ui.Toolbar(); var search = Ui.Button("Xem kết quả"); var export = Ui.Button("Xuất CSV");
            bar.Controls.AddRange(new Control[] { Ui.Label("Mã sinh viên"), student, history, search, export });
            var bottom = Ui.Toolbar(); bottom.Dock = DockStyle.Bottom;
            bottom.Controls.AddRange(new Control[] { cancel, checkCredits, summary });
            Controls.Add(grid); Controls.Add(creditCheck); Controls.Add(bottom); Controls.Add(bar);
            cancel.Visible = Service.User.LaSinhVien;
            checkCredits.Visible = Service.User.LaSinhVien; checkCredits.Name = "btnCheckCreditPlan";
            grid.SelectionChanged += (s, e) => ApplyDeadline();
            search.Click += async (s, e) => await Host.ExecuteAsync(RefreshDataAsync);
            cancel.Click += async (s, e) => await Host.ExecuteAsync(CancelAsync);
            checkCredits.Click += async (_, _) => await Host.ExecuteAsync(async () => {
                using var checks = await Host.Academics.PrecheckAsync([], "FINALIZE", null, Host.MaHocKy);
                if (IsDisposed) return;
                creditCheck.Visible = true;
                creditCheck.Text = "Kiểm tra kế hoạch hiện tại; thao tác này không chốt hoặc thay đổi đăng ký.\r\n" +
                    string.Join(Environment.NewLine, checks.Rows.Cast<DataRow>().Select(issue => ((bool)issue["blocking"] ? "Cần xử lý: " : "• ") + Convert.ToString(issue["message"])));
            });
            export.Click += async (s, e) => await Host.ExecuteAsync(() => {
                CsvExporter.Save(this, data, "KetQuaDangKy.csv"); return Task.CompletedTask; });
        }
        public override async Task RefreshDataAsync()
        {
            ClearData();
            if (!Service.User.LaSinhVien && string.IsNullOrWhiteSpace(student.Text))
            { summary.Text = "Nhập mã sinh viên để xem kết quả."; return; }
            var fresh = await Service.KetQuaAsync(Host.MaHocKy, student.Text, history.Checked);
            if (IsDisposed) { fresh.Dispose(); return; }
            data = fresh;
            Ui.Bind(grid, data);
            var active = data.Rows.Cast<DataRow>().Where(r => Convert.ToString(r["trang_thai"]) == "DANG_KY").ToList();
            int credits = active.GroupBy(r => Convert.ToString(r["ma_hp"])).Sum(g => Convert.ToInt32(g.First()["so_tin_chi"]));
            summary.Text = active.Count + " lớp hiệu lực • Tổng " + credits + " tín chỉ";
            ApplyDeadline();
        }
        public override void ClearData()
        {
            Ui.Bind(grid, null); data?.Dispose(); data = null; summary.Text = ""; creditCheck.Clear(); creditCheck.Visible = false; ApplyDeadline();
        }
        public override void ApplyDeadline()
        {
            var row = Ui.Current(grid);
            cancel.Enabled = Service.User.LaSinhVien && row != null && Convert.ToString(row["trang_thai"]) == "DANG_KY";
            checkCredits.Enabled = Service.User.LaSinhVien && Host.MaHocKy.Length > 0;
        }
        private async Task CancelAsync()
        {
            await Host.OpenCartAsync();
        }
    }
}
