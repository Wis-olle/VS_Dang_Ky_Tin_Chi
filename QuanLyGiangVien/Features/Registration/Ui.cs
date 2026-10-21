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
        {
            if (!ownedFonts.TryGetValue(control, out var fonts))
            {
                fonts = new(); ownedFonts.Add(control, fonts);
                control.Disposed += (_, _) => DisposeOwnedFonts(control);
            }
            fonts.Add(font); return font;
        }
        public static void DisposeOwnedFonts(Control control)
        {
            foreach (Control child in control.Controls) DisposeOwnedFonts(child);
            if (!ownedFonts.TryGetValue(control, out var fonts)) return;
            foreach (var font in fonts) font.Dispose();
            ownedFonts.Remove(control);
        }
        public static Button Button(string text, bool primary = false)
        {
            var button = new ShowcaseButton { Primary = primary, Text = text, AutoSize = true, Height = AppTheme.S(36), MinimumSize = new Size(AppTheme.S(100), AppTheme.S(36)),
                Padding = new Padding(AppTheme.S(10), AppTheme.S(3), AppTheme.S(10), AppTheme.S(3)), Margin = new Padding(AppTheme.S(4)), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink,
                UseVisualStyleBackColor = false, Cursor = Cursors.Hand, AutoEllipsis = true };
            button.Font = OwnFont(button, AppTheme.Font(9, true));
            return button;
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
            grid.ColumnAdded += (_, e) => {
                if (e.Column.ValueType == typeof(byte[]) || e.Column.Name is "version" or "phien_ban")
                    e.Column.Visible = false;
            };
            grid.DataError += (_, e) => {
                e.ThrowException = false;
                const string message = "Không hiển thị được ô dữ liệu này. Hãy tải lại.";
                grid.AccessibleDescription = message;
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0) grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = message;
                if (Environment.GetEnvironmentVariable("CAMPUS_PHASE3_UI_CHECK") is { Length: > 0 } output)
                    File.AppendAllText(Path.Combine(output, "grid-errors.txt"), grid.Name + ": " + e.Exception?.GetType().Name + Environment.NewLine);
            };
            grid.DataBindingComplete += (s, e) => Format(grid);
            grid.VisibleChanged += (_, _) => { if (grid.Visible) Format(grid); };
            grid.HandleCreated += (_, _) => Format(grid);
            grid.CellFormatting += (s, e) => {
                if (e.ColumnIndex < 0 || e.Value == null) return;
                string name = grid.Columns[e.ColumnIndex].Name;
                if ((name.EndsWith("_utc", StringComparison.Ordinal) || name == "changed_at") && e.Value is DateTime timestamp)
                {
                    var utc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
                    e.Value = new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
                    e.FormattingApplied = true; return;
                }
                if (name is "class_json" or "roles_json" && e.Value is string json)
                {
                    try {
                        e.Value = name == "class_json" ? string.Join(", ", System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [])
                            : string.Join(", ", (System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []).Select(v => v.Key + ": " + v.Value));
                        e.FormattingApplied = true;
                    } catch (System.Text.Json.JsonException) { e.Value = "Cần tải lại cấu hình thành phần"; e.FormattingApplied = true; }
                    return;
                }
                if (name is not ("trang_thai" or "trang_thai_lop" or "loai_hp" or "kind" or "status" or "effective_status" or "lifecycle" or "resource" or "standing" or "calendar_mode" or "recognition")) return;
                if (States.TryGetValue(Convert.ToString(e.Value) ?? "", out var display)) { e.Value = display; e.FormattingApplied = true; }
            };
            return grid;
        }
        private static readonly Dictionary<string, string> States = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"DANG_MO","Đang mở"},{"FULL","Đã đầy"},{"DONG","Đã đóng"},{"HUY","Đã hủy lớp"},
            {"DANG_KY","Đang đăng ký"},{"DA_HUY","Đã hủy"},{"BAT_BUOC","Bắt buộc"},{"TU_CHON","Tự chọn"},
            {"REGISTRATION","Mở đăng ký"},{"CLOSED","Đóng đăng ký"},{"TEACHING","Đang học"},{"COMPLETED","Hoàn tất"},{"CANCELED","Đã hủy"},{"ARCHIVED","Lưu trữ"},
            {"MAX_CREDITS","Vượt tín chỉ"},{"REQUIREMENT","Miễn điều kiện"},{"LATE_REGISTER","Đăng ký muộn"},{"WITHDRAW","Rút môn"},
            {"PENDING","Chờ xem xét"},{"APPROVED","Đã duyệt"},{"REJECTED","Từ chối"},{"REVOKED","Thu hồi"},{"APPLIED","Đã áp dụng"},{"EXPIRED","Hết hiệu lực"},
            {"ROOM","Phòng học"},{"LECTURER","Giảng viên"},{"STUDENT","Sinh viên"},{"SELF","Chồng buổi cùng lớp"},
            {"Waiting","Đang chờ"},{"Offered","Mời nhận chỗ"},{"Enrolled","Đã nhận chỗ"},{"Declined","Đã từ chối"},{"Cancelled","Đã hủy"},
            {"Draft","Bản nháp"},{"Submitted","Chờ cố vấn"},{"Commented","Đã nhận xét"},
            {"PROGRAM","Chương trình đào tạo"},{"GROUP","Nhóm kiến thức"},{"COURSE","Học phần"},{"ASSIGNMENT","Gắn CTĐT"},{"RESULT","Kết quả học tập"},{"CLASS","Lớp học phần"},
            {"NORMAL","Bình thường"},{"WARNING","Cảnh báo"},{"ELAPSED","Tính theo phút thực"},{"SKIP_HOLIDAYS","Dời hạn qua ngày nghỉ"}
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
            {"student","Sinh viên"},{"program","CTĐT"},{"course","Học phần"},{"term","Học kỳ"},
            {"attempt","Lần học"},{"grade","Điểm"},{"passed","Đạt"},{"source","Nguồn / căn cứ"},
            {"publication","Ngày công bố"},{"reason","Lý do"},{"standing","Học lực"},{"status","Trạng thái học vụ"},
            {"recognition","Hình thức công nhận"},
            {"gpa","ĐTBC (thang 10)"},{"finalTerm","Kỳ cuối"},{"scope","Phạm vi khóa"},{"authority","Đơn vị xử lý"},
            {"expires","Hết hiệu lực"},{"active","Hiệu lực"},{"name","Tên"},{"group","Nhóm"},
            {"required","Bắt buộc"},{"credits","Tín chỉ"},{"minCredits","TC tối thiểu"},{"maxCredits","TC tối đa"},
            {"passGrade","Điểm đạt"},{"allowImprove","Cho cải thiện"},{"maxAttempts","Giới hạn lần học (0: không giới hạn)"},
            {"enforceCurriculum","Kiểm CTĐT"},{"excellentThreshold","Ngưỡng ĐTBC"},{"excellentMaxCredits","TC học vượt"},
            {"warningRetakeOnly","Chỉ học lại"},{"published","Công bố"},{"published_at","Thời điểm công bố"},
            {"requiredCourse","Môn điều kiện"},{"kind","Loại điều kiện"},{"minGrade","Điểm tối thiểu"},
            {"accumulatedCredits","TC tích lũy"},{"requiredCredits","TC yêu cầu"},{"unmetRequiredCourses","Môn bắt buộc chưa đạt"},
            {"note","Ghi chú"},{"earnedCredits","TC đã đạt"},
            {"email","Email"},{"ma_lop","Lớp hành chính"},{"ten_nganh","Ngành"},
            {"id","Mã"},{"class_id","Lớp học phần"},{"rule_code","Nhóm điều kiện"},{"effective_status","Hiệu lực thực tế"},
            {"submitted_utc","Gửi lúc (UTC)"},{"reviewed_utc","Xử lý lúc (UTC)"},{"decision_reason","Lý do quyết định"},
            {"expires_utc","Hết hiệu lực (UTC)"},{"granted_uses","Lượt được cấp"},{"remaining_uses","Lượt còn lại"},{"max_credits","TC được duyệt"},
            {"lifecycle","Vòng đời"},{"ngay_hoc","Ngày học thực tế"},{"tiet_bd","Tiết bắt đầu"},{"tiet_kt","Tiết kết thúc"},
            {"ma_gv","Mã giảng viên"},{"ma_hk","Học kỳ"},{"ma_phong","Phòng"},{"class_json","Các thành phần bắt buộc"},{"combination_id","Tổ hợp"},
            {"event_type","Sự kiện"},{"entity","Đối tượng"},{"message","Nội dung"},{"created_utc","Tạo lúc (UTC)"},{"read_utc","Đọc lúc (UTC)"},
            {"occurred_utc","Thời điểm (UTC)"},{"actor","Người thực hiện"},{"request_id","Mã yêu cầu"},{"before_json","Trước thay đổi"},{"after_json","Sau thay đổi"},
            {"delivered_utc","Chuyển thông báo (UTC)"},{"attempts","Số lần chuyển"},{"next_attempt_utc","Thử lại lúc (UTC)"},{"last_error","Lỗi chuyển"},
            {"ma_tk","Mã tài khoản"},{"ten_dang_nhap","Tên đăng nhập"},{"permission","Quyền chi tiết"},{"other_class","Lớp xung đột"},{"date","Ngày"},{"resource","Loại xung đột"},
            {"roles_json","Loại thành phần LT/TH/TL"},{"tin_chi_tinh","TC tính vào tổng"},{"program_id","CTĐT khi gửi đơn"},
            {"cart_note","Nhận xét lớp"},{"source_date","Ngày gốc"},{"source_meeting_id","Mã buổi gốc"},{"changed_at","Thay đổi lúc (UTC)"},
            {"target_date","Ngày mới"},{"room_id","Phòng mới"},{"lecturer_id","GV mới"},{"start_period","Từ tiết"},{"end_period","Đến tiết"},
            {"enrolled","Đã đăng ký"},{"cart_students","SV có giỏ"},{"waiting","Đang chờ"},{"offered","Đang được mời"},{"reserved","Chỗ được giữ"},{"available","Chỗ khả dụng"},{"planned_students","SV dự kiến học"},
            {"position","Vị trí chờ"},{"priority","Thứ tự ưu tiên"},{"joined_utc","Vào hàng chờ (UTC)"},{"offer_expires_utc","Hạn nhận chỗ (UTC)"},{"claim_request_id","Mã yêu cầu nhận chỗ"},
            {"starts_utc","Mở đợt (UTC)"},{"ends_utc","Đóng đợt (UTC)"},{"public_utc","Mở chung (UTC)"},{"release_utc","Trả phần dư (UTC)"},{"offer_minutes","Phút nhận chỗ"},{"calendar_mode","Quy tắc ngày nghỉ"},{"publish_position","Công bố vị trí"},
            {"row_index","Dòng"},{"valid","Hợp lệ"},{"committed","Đã ghi"},{"batch_id","Mã đợt nhập"},{"row_count","Số dòng"},{"applied_utc","Ghi lúc (UTC)"},
            {"observed_utc","Quan sát (UTC)"},{"schema_version","Phiên bản schema"},{"pending_events","Sự kiện chờ"},{"failed_events","Sự kiện lỗi"},{"oldest_pending_utc","Sự kiện cũ nhất (UTC)"},{"students_without_curriculum","SV chưa gắn CTĐT"},{"stale_runs","Lượt chạy cần kiểm tra"},
            {"started_utc","Bắt đầu (UTC)"},{"ended_utc","Kết thúc (UTC)"},{"duration_ms","Thời gian (ms)"},{"error_code","Mã lỗi"},{"opens_utc","Lịch hẹn (UTC)"},{"last_attempt_utc","Đã xét lúc (UTC)"}
        };
        private static void Format(DataGridView grid)
        {
            // Hidden TabPages do not yet have the native display bands needed by
            // WinForms fill sizing. Apply widths once the grid is actually shown.
            if (!grid.IsHandleCreated || !grid.Visible || grid.IsDisposed) return;
            var sizing = grid.AutoSizeColumnsMode;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            try
            {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (Headers.TryGetValue(column.Name, out var caption)) column.HeaderText = caption.Replace("(UTC)", "(VN)", StringComparison.Ordinal);
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
                {
                    bool dateOnly = column.Name is "ngay_sinh" or "ngay_hoc" or "source_date" or "target_date" or "date";
                    column.DefaultCellStyle.Format = dateOnly ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm";
                    column.MinimumWidth = AppTheme.S(dateOnly ? 110 : 150); column.FillWeight = dateOnly ? 110 : 150;
                }
                if (column.Name == "ty_le_lap_day") column.DefaultCellStyle.Format = "N2";
            }
            // Recalculate after captions and widths settle; generated SQL column
            // names may have temporarily required many wrapped header lines.
            grid.AutoResizeColumnHeadersHeight();
            }
            finally { if (!grid.IsDisposed) grid.AutoSizeColumnsMode = sizing; }
        }
        public static DataRow? Current(DataGridView grid)
        {
            var view = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as DataRowView;
            return view == null ? null : view.Row;
        }
        public static void Bind(DataGridView grid, DataTable? table) { grid.DataSource = table; }
    }
}
