// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/TraCuuPage.cs.txt.
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuanLyGiangVien.Features.Registration
{
    internal sealed class TraCuuPage : ModulePage
    {
        private readonly TextBox keyword = new TextBox { Name = "txtTuKhoa", Width = 290, MaxLength = 100, Margin = new Padding(4, 10, 6, 4) };
        private readonly CheckBox conCho = new CheckBox { Name = "chkConCho", Text = "Chỉ lớp còn chỗ", AutoSize = true, Margin = new Padding(12, 12, 8, 4) };
        private readonly DataGridView grid = Ui.Grid();
        private readonly Button register = Ui.Button("Đăng ký lớp đã chọn", true);
        private readonly Label summary = Ui.Label("");
        private DataTable? data;
        public TraCuuPage(RegistrationWorkspace host) : base(host)
        {
            grid.Name = "dgvLopMo"; register.Name = "btnDangKy";
            var bar = Ui.Toolbar(); var search = Ui.Button("Tìm kiếm"); search.Name = "btnTimKiem";
            bar.Controls.AddRange(new Control[] { Ui.Label("Mã lớp / mã HP / tên HP"), keyword, conCho, search });
            var bottom = Ui.Toolbar(); bottom.Dock = DockStyle.Bottom;
            bottom.Controls.AddRange(new Control[] { register, summary });
            Controls.Add(grid); Controls.Add(bottom); Controls.Add(bar);
            register.Visible = Service.User.LaSinhVien;
            search.Click += async (s, e) => await Host.ExecuteAsync(RefreshDataAsync);
            keyword.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) {
                e.SuppressKeyPress = true; await Host.ExecuteAsync(RefreshDataAsync); } };
            grid.SelectionChanged += (s, e) => ApplyDeadline();
            register.Click += async (s, e) => await Host.ExecuteAsync(RegisterAsync);
        }
        public override async Task RefreshDataAsync()
        {
            ClearData(); summary.Text = "Đang tải…";
            var fresh = await Service.TraCuuAsync(Host.MaHocKy, keyword.Text, conCho.Checked);
            if (IsDisposed) { fresh.Dispose(); return; }
            data = fresh;
            Ui.Bind(grid, data); summary.Text = data.Rows.Count + " lớp • Chọn một dòng để đăng ký";
            ApplyDeadline();
        }
        public override void ClearData()
        {
            Ui.Bind(grid, null); data?.Dispose(); data = null; summary.Text = ""; ApplyDeadline();
        }
        public override void ApplyDeadline()
        {
            var row = Ui.Current(grid);
            register.Enabled = Service.User.LaSinhVien && Host.ChoDangKy && row != null && Convert.ToInt32(row["so_cho_con"]) > 0;
        }
        private async Task RegisterAsync()
        {
            var row = Ui.Current(grid); if (row == null) return;
            string code = Convert.ToString(row["ma_lhp"])!;
            if (MessageBox.Show(this, "Đăng ký " + code + " – " + row["ten_hp"] + "?", "Xác nhận đăng ký",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var result = await Service.DangKyAsync(code);
            if (IsDisposed) return;
            MessageBox.Show(this, Convert.ToString(result.Rows[0]["thong_bao"]), "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Host.NotifyDataChanged();
            await Host.RefreshCurrentAsync();
        }
    }
}
