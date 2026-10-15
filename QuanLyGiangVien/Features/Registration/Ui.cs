// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/Ui.cs.txt.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration
{
    internal static class Ui
    {
        public static readonly Color Ink = AppTheme.Ink;
        public static readonly Color Accent = AppTheme.Primary;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, List<Font>> ownedFonts = new();
        public static Font OwnFont(Control control, Font font)
        { ownedFonts.GetOrCreateValue(control).Add(font); return font; }
        public static void DisposeOwnedFonts(Control control)
        {
            foreach (Control child in control.Controls) DisposeOwnedFonts(child);
            if (!ownedFonts.TryGetValue(control, out var fonts)) return;
            foreach (var font in fonts) font.Dispose();
            ownedFonts.Remove(control);
        }
        public static Button Button(string text, bool primary = false)
        {
            return new Button { Text = text, AutoSize = true, Height = AppTheme.S(36), MinimumSize = new Size(AppTheme.S(100), AppTheme.S(36)),
                Padding = new Padding(AppTheme.S(10), AppTheme.S(3), AppTheme.S(10), AppTheme.S(3)), Margin = new Padding(AppTheme.S(4)), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink,
                UseVisualStyleBackColor = false, Cursor = Cursors.Hand, AutoEllipsis = true };
        }
        public static Label Label(string text)
        { return new Label { Text = text, AutoSize = true, Margin = new Padding(AppTheme.S(6), AppTheme.S(12), AppTheme.S(6), AppTheme.S(4)), ForeColor = Ink }; }
        public static FlowLayoutPanel Toolbar()
        { return new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
            Padding = new Padding(AppTheme.S(6)), BackColor = Color.White }; }
        public static DataGridView Grid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToOrderColumns = true, RowHeadersVisible = false,
                AutoGenerateColumns = true, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize };
            grid.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.Lilac;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppTheme.Lilac;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = OwnFont(grid, AppTheme.Font(10, true));
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(AppTheme.S(6));
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.Padding = new Padding(AppTheme.S(6));
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.SelectionBackColor = AppTheme.AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 249, 249);
            grid.DataBindingComplete += (s, e) => Format(grid);
            grid.CellFormatting += (s, e) => {
                if (e.ColumnIndex < 0 || e.Value == null) return;
                string name = grid.Columns[e.ColumnIndex].Name;
                if (name != "trang_thai" && name != "trang_thai_lop" && name != "loai_hp") return;
                if (States.TryGetValue(Convert.ToString(e.Value) ?? "", out var display)) { e.Value = display; e.FormattingApplied = true; }
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
            {"ma_lhp","Mã lớp HP"},{"ma_hp","Mã HP"},{"ten_hp","Tên học phần"},
            {"so_tin_chi","Tín chỉ"},{"loai_hp","Loại HP"},{"ten_gv","Giảng viên"},{"lich_hoc","Lịch học"},
            {"si_so_hien_tai","Đã ĐK"},{"si_so_max","Sĩ số"},{"so_cho_con","Còn chỗ"},
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
                if (Headers.TryGetValue(column.Name, out var caption)) column.HeaderText = caption;
                int minimum = column.Name switch {
                    "ten_hp" or "ho_ten" => 145, "ten_gv" => 130, "lich_hoc" => 210,
                    "so_tin_chi" or "stt" => 48,
                    "si_so_hien_tai" or "si_so_max" or "so_cho_con" => 65,
                    "loai_hp" => 80, _ => 90
                };
                column.MinimumWidth = AppTheme.S(minimum);
                column.FillWeight = minimum;
                if (column.Name == "lich_hoc") column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                if (column.ValueType == typeof(DateTime))
                { column.DefaultCellStyle.Format = column.Name == "ngay_sinh" ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm"; column.MinimumWidth = AppTheme.S(150); column.FillWeight = 150; }
                if (column.Name == "ty_le_lap_day") column.DefaultCellStyle.Format = "N2";
            }
            // Recalculate after captions and widths settle; generated SQL column
            // names may have temporarily required many wrapped header lines.
            grid.AutoResizeColumnHeadersHeight();
        }
        public static DataRow? Current(DataGridView grid)
        {
            var view = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as DataRowView;
            return view == null ? null : view.Row;
        }
        public static void Bind(DataGridView grid, DataTable? table) { grid.DataSource = table; }
    }
}
