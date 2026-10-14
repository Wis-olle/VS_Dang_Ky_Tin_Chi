// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/KetQuaPage.cs.txt.
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace QuanLyGiangVien.Features.Registration
{
    internal sealed class KetQuaPage : ModulePage
    {
        private readonly DataGridView grid = Ui.Grid();
        private readonly TextBox student = new TextBox { Name = "txtMaSv", Width = 170, MaxLength = 20, Margin = new Padding(4, 10, 6, 4) };
        private readonly CheckBox history = new CheckBox { Text = "Hiện cả đăng ký đã hủy", AutoSize = true, Margin = new Padding(10, 12, 8, 4) };
        private readonly Button cancel = Ui.Button("Hủy đăng ký đã chọn");
        private readonly Label summary = Ui.Label("");
        private DataTable? data;
        public KetQuaPage(RegistrationWorkspace host) : base(host)
        {
            grid.Name = "dgvKetQua"; cancel.Name = "btnHuyDangKy";
            student.Text = Service.User.MaSv; student.ReadOnly = Service.User.LaSinhVien;
            var bar = Ui.Toolbar(); var search = Ui.Button("Xem kết quả"); var export = Ui.Button("Xuất CSV");
            bar.Controls.AddRange(new Control[] { Ui.Label("Mã sinh viên"), student, history, search, export });
            var bottom = Ui.Toolbar(); bottom.Dock = DockStyle.Bottom;
            bottom.Controls.AddRange(new Control[] { cancel, summary });
            Controls.Add(grid); Controls.Add(bottom); Controls.Add(bar);
            cancel.Visible = Service.User.LaSinhVien;
            grid.SelectionChanged += (s, e) => ApplyDeadline();
            search.Click += async (s, e) => await Host.ExecuteAsync(RefreshDataAsync);
            cancel.Click += async (s, e) => await Host.ExecuteAsync(CancelAsync);
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
            int credits = active.Sum(r => Convert.ToInt32(r["so_tin_chi"]));
            summary.Text = active.Count + " lớp hiệu lực • Tổng " + credits + " tín chỉ";
            ApplyDeadline();
        }
        public override void ClearData()
        {
            Ui.Bind(grid, null); data?.Dispose(); data = null; summary.Text = ""; ApplyDeadline();
        }
        public override void ApplyDeadline()
        {
            var row = Ui.Current(grid);
            cancel.Enabled = Service.User.LaSinhVien && Host.ChoHuy && row != null && Convert.ToString(row["trang_thai"]) == "DANG_KY";
        }
        private async Task CancelAsync()
        {
            var row = Ui.Current(grid); if (row == null) return;
            if (MessageBox.Show(this, "Hủy đăng ký lớp " + row["ma_lhp"] + "?", "Xác nhận hủy",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var result = await Service.HuyAsync(Convert.ToInt64(row["ma_dk"]));
            if (IsDisposed) return;
            MessageBox.Show(this, Convert.ToString(result.Rows[0]["thong_bao"]), "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Host.NotifyDataChanged();
            await Host.RefreshCurrentAsync();
        }
    }
}
