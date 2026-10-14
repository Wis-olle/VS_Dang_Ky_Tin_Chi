using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace DangKyTinChi.Forms
{
    internal static class Ui
    {
        public static readonly Color Ink = Color.FromArgb(25, 40, 64);
        public static readonly Color Accent = Color.FromArgb(31, 92, 178);
        public static Button Button(string text, bool primary = false)
        {
            return new Button { Text = text, AutoSize = true, Height = 36, MinimumSize = new Size(100, 36),
                Padding = new Padding(10, 3, 10, 3), Margin = new Padding(4), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink,
                UseVisualStyleBackColor = false, Cursor = Cursors.Hand };
        }
        public static Label Label(string text)
        { return new Label { Text = text, AutoSize = true, Margin = new Padding(6, 12, 6, 4), ForeColor = Ink }; }
        public static FlowLayoutPanel Toolbar()
        { return new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
            Padding = new Padding(6), BackColor = Color.White }; }
        public static DataGridView Grid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToOrderColumns = true, RowHeadersVisible = false,
                AutoGenerateColumns = true, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 42, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 237, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grid.DefaultCellStyle.Padding = new Padding(6);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 232, 251);
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
            grid.DataBindingComplete += (s, e) => Format(grid);
            grid.CellFormatting += (s, e) => {
                if (e.ColumnIndex < 0 || e.Value == null) return;
                string name = grid.Columns[e.ColumnIndex].Name;
                if (name != "trang_thai" && name != "trang_thai_lop" && name != "loai_hp") return;
                string display;
                if (States.TryGetValue(Convert.ToString(e.Value), out display)) { e.Value = display; e.FormattingApplied = true; }
            };
            return grid;
        }
        private static readonly Dictionary<string, string> States = new Dictionary<string, string>
        {
            {"DANG_MO","Đang mở"},{"FULL","Đã đầy"},{"DONG","Đã đóng"},{"HUY","Đã hủy lớp"},
            {"DANG_KY","Đang đăng ký"},{"DA_HUY","Đã hủy"},{"BAT_BUOC","Bắt buộc"},{"TU_CHON","Tự chọn"}
        };
        private static readonly Dictionary<string, string> Headers = new Dictionary<string, string>
        {
            {"ma_lhp","Mã lớp HP"},{"ma_hp","Mã học phần"},{"ten_hp","Tên học phần"},
            {"so_tin_chi","Tín chỉ"},{"loai_hp","Loại HP"},{"ten_gv","Giảng viên"},{"lich_hoc","Lịch học"},
            {"si_so_hien_tai","Đã đăng ký"},{"si_so_max","Sĩ số tối đa"},{"so_cho_con","Chỗ còn"},
            {"trang_thai","Trạng thái"},{"ma_dk","Mã đăng ký"},{"ngay_dk","Ngày đăng ký"},{"ngay_huy","Ngày hủy"},
            {"trang_thai_lop","Trạng thái lớp"},{"ty_le_lap_day","Lấp đầy (%)"},
            {"tong_tin_chi_dang_ky","Tổng TC đăng ký"},{"stt","STT"},{"ma_sv","Mã sinh viên"},
            {"ho_ten","Họ tên"},{"ngay_sinh","Ngày sinh"},{"gioi_tinh","Giới tính"},
            {"email","Email"},{"ma_lop","Lớp hành chính"},{"ten_nganh","Ngành"}
        };
        private static void Format(DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                string caption;
                if (Headers.TryGetValue(column.Name, out caption)) column.HeaderText = caption;
                column.Width = 125;
                if (column.Name == "ten_hp" || column.Name == "ho_ten" || column.Name == "ten_gv") column.Width = 230;
                if (column.Name == "lich_hoc") { column.Width = 390; column.DefaultCellStyle.WrapMode = DataGridViewTriState.True; }
                if (column.ValueType == typeof(DateTime))
                { column.DefaultCellStyle.Format = column.Name == "ngay_sinh" ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm"; column.Width = 170; }
                if (column.Name == "ty_le_lap_day") column.DefaultCellStyle.Format = "N2";
            }
        }
        public static DataRow Current(DataGridView grid)
        {
            var view = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as DataRowView;
            return view == null ? null : view.Row;
        }
        public static void Bind(DataGridView grid, DataTable table) { grid.DataSource = table; }
    }
}
