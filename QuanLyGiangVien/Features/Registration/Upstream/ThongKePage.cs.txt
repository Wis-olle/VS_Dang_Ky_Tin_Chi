using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DangKyTinChi.Services;

namespace DangKyTinChi.Forms
{
    public sealed class ThongKePage : ModulePage
    {
        private readonly DataGridView classes = Ui.Grid();
        private readonly DataGridView students = Ui.Grid();
        private readonly CheckBox lowOnly = new CheckBox { Text = "Lớp có số SV nhỏ hơn", AutoSize = true, Margin = new Padding(8, 12, 4, 4) };
        private readonly NumericUpDown threshold = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 15, Width = 65, Margin = new Padding(4, 10, 6, 4) };
        private readonly Label summary = Ui.Label("");
        private readonly Label rosterTitle = Ui.Label("Chọn lớp phía trên rồi bấm Xem danh sách sinh viên.");
        private DataTable classData, studentData;
        public ThongKePage(MainForm host) : base(host)
        {
            classes.Name = "dgvThongKeLop"; students.Name = "dgvSinhVien";
            var bar = Ui.Toolbar(); var filter = Ui.Button("Thống kê"); var exportStats = Ui.Button("Xuất thống kê");
            bar.Controls.AddRange(new Control[] { lowOnly, threshold, filter, exportStats, summary });
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                Size = new System.Drawing.Size(1200, 600), SplitterDistance = 260, Panel1MinSize = 120, Panel2MinSize = 140 };
            split.Panel1.Controls.Add(classes);
            var detailBar = Ui.Toolbar(); var view = Ui.Button("Xem danh sách sinh viên", true); var export = Ui.Button("Xuất danh sách");
            detailBar.Controls.AddRange(new Control[] { view, export, rosterTitle });
            split.Panel2.Controls.Add(students); split.Panel2.Controls.Add(detailBar);
            Controls.Add(split); Controls.Add(bar);
            filter.Click += async (s, e) => await Host.ExecuteAsync(RefreshDataAsync);
            view.Click += async (s, e) => await Host.ExecuteAsync(LoadRosterAsync);
            classes.SelectionChanged += (s, e) => {
                studentData = null; Ui.Bind(students, null); rosterTitle.Text = "Bấm Xem danh sách sinh viên để tải lớp đã chọn.";
            };
            export.Click += async (s, e) => await Host.ExecuteAsync(() => {
                CsvExporter.Save(this, studentData, "DanhSachSinhVien.csv"); return Task.CompletedTask; });
            exportStats.Click += async (s, e) => await Host.ExecuteAsync(() => {
                CsvExporter.Save(this, classData, "ThongKeLop.csv"); return Task.CompletedTask; });
        }
        public override async Task RefreshDataAsync()
        {
            ClearData();
            classData = await Service.ThongKeAsync(Host.MaHocKy, lowOnly.Checked ? (int?)threshold.Value : null);
            Ui.Bind(classes, classData);
            int registrations = classData.Rows.Cast<DataRow>().Sum(r => Convert.ToInt32(r["si_so_hien_tai"]));
            summary.Text = classData.Rows.Count + " lớp • " + registrations + " lượt đăng ký (theo bộ lọc)";
        }
        public override void ClearData()
        {
            classData = studentData = null;
            Ui.Bind(classes, null); Ui.Bind(students, null); summary.Text = "";
            rosterTitle.Text = "Chọn lớp phía trên rồi bấm Xem danh sách sinh viên.";
        }
        private async Task LoadRosterAsync()
        {
            var row = Ui.Current(classes); if (row == null) throw new InvalidOperationException("Hãy chọn một lớp học phần.");
            string code = Convert.ToString(row["ma_lhp"]);
            studentData = null; Ui.Bind(students, null);
            studentData = await Service.DanhSachAsync(code); Ui.Bind(students, studentData);
            rosterTitle.Text = code + " • " + studentData.Rows.Count + " sinh viên đang đăng ký";
        }
    }
}
