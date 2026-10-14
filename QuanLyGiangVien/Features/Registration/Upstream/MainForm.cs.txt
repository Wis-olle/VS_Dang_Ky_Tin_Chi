using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DangKyTinChi.Services;

namespace DangKyTinChi.Forms
{
    public sealed class MainForm : Form
    {
        public DangKyService Service { get; private set; }
        public string MaHocKy { get { return cboHocKy.SelectedValue as string; } }
        public bool ChoDangKy { get; private set; }
        public bool ChoHuy { get; private set; }
        private readonly ComboBox cboHocKy = new ComboBox { Name = "cboHocKy", DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, Margin = new Padding(4, 8, 12, 4) };
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(20, 10) };
        private readonly FlowLayoutPanel top = Ui.Toolbar();
        private readonly Label identity = Ui.Label("Đang kết nối…");
        private readonly Label deadline = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(18, 12, 18, 12), BackColor = Color.FromArgb(233, 241, 254) };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) };
        private readonly Timer timer = new Timer { Interval = 30000 };
        private bool initialized;
        private bool busy;

        public MainForm(DangKyService service)
        {
            Service = service;
            Text = "Đăng ký học theo tín chỉ";
            Font = new Font("Segoe UI", 10F);
            ForeColor = Ui.Ink; BackColor = Color.FromArgb(243, 246, 251);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1450, 880); MinimumSize = new Size(1100, 700);
            var title = new Label { Text = "ĐĂNG KÝ HỌC THEO TÍN CHỈ", Dock = DockStyle.Top, Height = 64,
                Font = new Font("Segoe UI", 19F, FontStyle.Bold), Padding = new Padding(18, 14, 0, 0) };
            var refresh = Ui.Button("Làm mới"); refresh.Name = "btnLamMoi";
            top.Controls.AddRange(new Control[] { Ui.Label("Học kỳ"), cboHocKy, refresh, identity });
            Controls.Add(tabs); Controls.Add(status); Controls.Add(deadline); Controls.Add(top); Controls.Add(title);
            Shown += async (s, e) => await ExecuteAsync(InitializeAsync);
            refresh.Click += async (s, e) => await ExecuteAsync(async () => {
                if (!initialized) await InitializeAsync(); else await RefreshCurrentAsync();
            });
            cboHocKy.SelectedIndexChanged += async (s, e) => {
                if (initialized) await ExecuteAsync(async () => {
                    foreach (TabPage page in tabs.TabPages) ((ModulePage)page.Controls[0]).ClearData();
                    await RefreshCurrentAsync();
                });
            };
            tabs.SelectedIndexChanged += async (s, e) => {
                if (initialized) await ExecuteAsync(RefreshCurrentAsync);
            };
            timer.Tick += async (s, e) => { if (initialized) await ExecuteAsync(RefreshDeadlineAsync); };
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); };
        }
        private async Task InitializeAsync()
        {
            await Service.InitializeAsync();
            identity.Text = Service.User.HoTen + "  |  " + (Service.User.LaSinhVien ? Service.User.MaSv : "Phòng đào tạo");
            var terms = await Service.HocKyAsync();
            cboHocKy.DisplayMember = "ten_hk"; cboHocKy.ValueMember = "ma_hk"; cboHocKy.DataSource = terms;
            tabs.TabPages.Clear();
            AddPage("Tra cứu lớp mở", new TraCuuPage(this));
            AddPage("Kết quả đăng ký", new KetQuaPage(this));
            if (Service.User.LaDaoTao) AddPage("Thống kê theo lớp", new ThongKePage(this));
            initialized = true;
            timer.Start();
            await RefreshCurrentAsync();
        }
        private void AddPage(string title, ModulePage page)
        { var tab = new TabPage(title); tab.Controls.Add(page); tabs.TabPages.Add(tab); }
        public async Task RefreshDeadlineAsync()
        {
            ChoDangKy = ChoHuy = false;
            ApplyDeadlines();
            deadline.Text = "Đang kiểm tra thời hạn trên máy chủ…";
            if (string.IsNullOrEmpty(MaHocKy)) { deadline.Text = "Chưa có học kỳ. Liên hệ phòng đào tạo."; return; }
            DataTable table;
            try { table = await Service.ThoiHanAsync(MaHocKy); }
            catch
            {
                deadline.Text = "Không kiểm tra được thời hạn. Bấm Làm mới để thử lại.";
                throw;
            }
            if (table.Rows.Count != 1) throw new InvalidOperationException("Không nhận được thời hạn của học kỳ.");
            var row = table.Rows[0];
            ChoDangKy = Convert.ToBoolean(row["cho_dang_ky"]); ChoHuy = Convert.ToBoolean(row["cho_huy"]);
            deadline.Text = string.Format("{0}  •  {1}  •  Giờ máy chủ: {2:dd/MM/yyyy HH:mm:ss} (Việt Nam)\r\nĐăng ký: {3:dd/MM/yyyy HH:mm} → {4:dd/MM/yyyy HH:mm}  |  Hạn hủy: {5:dd/MM/yyyy HH:mm}",
                ChoDangKy ? "ĐANG NHẬN ĐĂNG KÝ" : "NGOÀI HẠN / ĐÃ ĐÓNG", ChoHuy ? "Còn hạn hủy" : "Không thể hủy",
                row["gio_may_chu"], row["tg_bat_dau"], row["tg_ket_thuc"], row["han_huy_dk"]);
            ApplyDeadlines();
        }
        private void ApplyDeadlines()
        {
            foreach (TabPage page in tabs.TabPages) ((ModulePage)page.Controls[0]).ApplyDeadline();
        }
        public async Task RefreshCurrentAsync()
        {
            if (tabs.SelectedTab != null) ((ModulePage)tabs.SelectedTab.Controls[0]).ClearData();
            await RefreshDeadlineAsync();
            if (tabs.SelectedTab != null && !string.IsNullOrEmpty(MaHocKy))
                await ((ModulePage)tabs.SelectedTab.Controls[0]).RefreshDataAsync();
        }
        public async Task ExecuteAsync(Func<Task> action)
        {
            if (busy || IsDisposed) return;
            busy = true; top.Enabled = tabs.Enabled = false; UseWaitCursor = true; status.Text = "Đang xử lý…";
            try { await action(); if (!IsDisposed) status.Text = "Sẵn sàng"; }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                if (!IsDisposed)
                {
                    var sql = ex as SqlException;
                    string message = sql == null ? ex.Message :
                        sql.Number >= 51000 && sql.Number < 52000 ? sql.Message :
                        sql.Number == 1205 ? "Giao dịch bị xung đột. Vui lòng làm mới và thử lại." :
                        "Không hoàn tất được yêu cầu SQL (mã " + sql.Number + "). Kiểm tra kết nối, tài khoản và script database; làm mới kết quả trước khi thử lại thao tác ghi.";
                    status.Text = "Yêu cầu chưa hoàn tất";
                    MessageBox.Show(this, message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally { busy = false; if (!IsDisposed) { top.Enabled = tabs.Enabled = true; UseWaitCursor = false; } }
        }
    }
}
