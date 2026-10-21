using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Accounts;

internal sealed class AccountAdminPage : UserControl
{
    private readonly AccountRepository repository;
    private readonly Action changed;
    private readonly DataGridView grid = new();
    private readonly TextBox username = new() { MaxLength = 128 }, student = new() { MaxLength = 20 },
        password = new() { MaxLength = 256, UseSystemPasswordChar = true }, reason = new() { MaxLength = 500, Multiline = true, Height = 75 };
    private readonly ComboBox role = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox active = new() { Text = "Cho phép đăng nhập", Checked = true, AutoSize = true };
    private readonly Label notice = new() { AutoSize = true, ForeColor = AppTheme.Primary, MaximumSize = new(AppTheme.S(330), 0) };
    private readonly List<Font> fonts = [];
    private DataTable? rows;
    private DataRow? selected;
    private string baseline = "";
    private bool binding, busy;
    private bool administrator;
    private readonly Panel editor;
    public bool HasUnsavedChanges => baseline != State();
    private int S(int n) => AppTheme.S(n);

    public AccountAdminPage(string connectionString, Action changed)
    {
        repository = new(connectionString); this.changed = changed;
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas;
        var font = AppTheme.Font(9); fonts.Add(font); Font = font;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(S(8)), BackColor = Color.White };
        var add = Button("Tạo tài khoản"); var refresh = Button("Tải lại"); var audit = Button("Nhật ký");
        toolbar.Controls.AddRange([add, refresh, audit]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new(S(8)) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100)); layout.ColumnStyles.Add(new(SizeType.Absolute, S(380)));
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.MultiSelect = false; grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.Lilac;
        grid.DefaultCellStyle.SelectionBackColor = AppTheme.AccentSoft; grid.DefaultCellStyle.SelectionForeColor = AppTheme.Ink;
        grid.RowTemplate.Height = S(40); grid.AutoGenerateColumns = false; grid.Name = "accountGrid";
        foreach (var field in new[] { ("ten_dang_nhap", "Tên đăng nhập", 145), ("vai_tro", "Vai trò", 110), ("ma_sv", "Mã sinh viên", 110), ("trang_thai", "Hoạt động", 75) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = field.Item1, DataPropertyName = field.Item1, HeaderText = field.Item2, MinimumWidth = S(field.Item3), FillWeight = field.Item3 });
        grid.CellFormatting += (_, e) => {
            if (e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name == "vai_tro") { e.Value = RoleName(Convert.ToString(e.Value)); e.FormattingApplied = true; }
            if (grid.Columns[e.ColumnIndex].Name == "trang_thai" && e.Value is bool value) { e.Value = value ? "Đang mở" : "Đã khóa"; e.FormattingApplied = true; }
        };
        layout.Controls.Add(grid, 0, 0);
        editor = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new(S(18)) };
        var fields = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = S(340) };
        void Field(string label, Control input)
        {
            fields.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = AppTheme.Muted, Margin = new(0, S(8), 0, S(4)) });
            input.Width = S(326); input.AccessibleName = label; fields.Controls.Add(input);
        }
        fields.Controls.Add(new Label { Text = "Tài khoản ứng dụng", AutoSize = true, ForeColor = AppTheme.Ink });
        fields.Controls.Add(notice); Field("Tên đăng nhập *", username); Field("Vai trò *", role);
        Field("Mã sinh viên (tài khoản Sinh viên)", student); fields.Controls.Add(active);
        Field("Mật khẩu mới (12–256 ký tự)", password); Field("Lý do tạo / thay đổi *", reason);
        var actions = new FlowLayoutPanel { AutoSize = true, MaximumSize = new(S(326), 0), WrapContents = true };
        var save = Button("Lưu tài khoản", true); var reset = Button("Đặt lại mật khẩu"); actions.Controls.AddRange([save, reset]); fields.Controls.Add(actions);
        fields.Controls.Add(new Label { Text = "Danh tính và vai trò đã tạo được giữ cố định. Khóa hoặc đặt lại mật khẩu sẽ thu hồi các phiên đăng nhập của tài khoản.", AutoSize = true, MaximumSize = new(S(326), 0), ForeColor = AppTheme.Muted });
        editor.Controls.Add(fields); layout.Controls.Add(editor, 1, 0); Controls.Add(layout); Controls.Add(toolbar);
        add.Click += (_, _) => { if (CanLeave()) NewAccount(); };
        refresh.Click += async (_, _) => { if (CanLeave()) await RunAsync(ReloadAsync); };
        audit.Click += async (_, _) => await RunAsync(ShowAuditAsync);
        save.Click += async (_, _) => await RunAsync(SaveAsync);
        reset.Click += async (_, _) => await RunAsync(ResetAsync);
        role.SelectedIndexChanged += (_, _) => student.Enabled = selected == null && Convert.ToString(role.SelectedItem) == "SINH_VIEN";
        grid.SelectionChanged += (_, _) => {
            if (binding) return;
            var row = (grid.CurrentRow?.DataBoundItem as DataRowView)?.Row;
            if (row == selected) return;
            if (!CanLeave()) { binding = true; if (selected != null) { foreach (DataGridViewRow item in grid.Rows) if ((item.DataBoundItem as DataRowView)?.Row == selected) grid.CurrentCell = item.Cells[0]; } else { grid.CurrentCell = null; grid.ClearSelection(); } binding = false; return; }
            Select(row);
        };
        NewAccount();
    }

    private static string RoleName(string? role) => role switch { "SINH_VIEN" => "Sinh viên", "DAO_TAO" => "Phòng đào tạo", "QUAN_TRI" => "Quản trị", _ => role ?? "" };
    private Button Button(string text, bool primary = false) => new QuanLyGiangVien.Features.Registration.ShowcaseButton { Primary = primary, Text = text, AutoSize = true, MinimumSize = new(S(100), S(36)), FlatStyle = FlatStyle.Flat, BackColor = primary ? AppTheme.Primary : Color.White, ForeColor = primary ? Color.White : AppTheme.Ink, Margin = new(0, S(8), S(8), S(8)) };
    private string State() => string.Join("\u001f", username.Text, role.SelectedItem, student.Text, active.Checked, password.Text, reason.Text);
    public bool CanLeave() => !busy && (!HasUnsavedChanges || MessageBox.Show(this, "Bỏ bản nháp tài khoản chưa lưu?", "Bản nháp", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);

    public async Task ReloadAsync()
    {
        var fresh = await repository.ListAsync(); if (IsDisposed) { fresh.Dispose(); return; }
        binding = true;
        try
        {
            grid.DataSource = null; rows?.Dispose(); rows = fresh; grid.DataSource = rows;
            string manager = rows.Rows.Count == 0 ? "DAO_TAO" : Convert.ToString(rows.Rows[0]["vai_tro_nguoi_quan_ly"])!;
            administrator = manager == "QUAN_TRI";
            role.Items.Clear(); role.Items.Add("SINH_VIEN"); if (manager == "QUAN_TRI") role.Items.AddRange(["DAO_TAO", "QUAN_TRI"]);
            role.Format -= FormatRole; role.Format += FormatRole;
            grid.ClearSelection(); NewAccount();
        }
        finally { binding = false; }
    }
    private static void FormatRole(object? sender, ListControlConvertEventArgs e) => e.Value = RoleName(Convert.ToString(e.ListItem));
    private void NewAccount() { selected = null; username.Enabled = role.Enabled = student.Enabled = true; username.Clear(); student.Clear(); password.Clear(); reason.Clear(); active.Checked = true; role.Items.Clear(); role.Items.Add("SINH_VIEN"); if (administrator) role.Items.AddRange(["DAO_TAO", "QUAN_TRI"]); role.SelectedIndex = 0; notice.Text = "Tạo tài khoản mới. Mật khẩu không được lưu trong nhật ký."; baseline = State(); }
    private void Select(DataRow? row)
    {
        if (row == null) return;
        selected = row; username.Text = Convert.ToString(row["ten_dang_nhap"]); student.Text = Convert.ToString(row["ma_sv"]);
        var value = Convert.ToString(row["vai_tro"])!; if (!role.Items.Contains(value)) role.Items.Add(value); role.SelectedItem = value;
        active.Checked = (bool)row["trang_thai"]; username.Enabled = role.Enabled = student.Enabled = false; password.Clear(); reason.Clear();
        notice.Text = (bool)row["la_ban_than"] ? "Tài khoản của bạn. Dùng Đổi mật khẩu ở thanh trên." : !(bool)row["co_mat_khau"] ? "Tài khoản Windows được quản lý riêng." : "Sửa trạng thái hoặc đặt lại mật khẩu; bắt buộc ghi lý do.";
        baseline = State();
    }
    private async Task SaveAsync()
    {
        var draft = new AccountDraft(selected == null ? null : (int)selected["ma_tk"], username.Text, Convert.ToString(role.SelectedItem) ?? "", student.Text, active.Checked,
            selected == null ? null : Convert.ToString(selected["phien_ban"]), reason.Text);
        await repository.SaveAsync(draft, password.Text);
        changed(); await ReloadAsync(); notice.Text = "Đã lưu tài khoản. Các phiên cũ của tài khoản sửa đã được thu hồi.";
    }
    private async Task ResetAsync()
    {
        if (selected == null) throw new ArgumentException("Chọn tài khoản để đặt lại mật khẩu.");
        if (MessageBox.Show(this, "Đặt lại mật khẩu và thu hồi tất cả phiên của tài khoản đã chọn?", "Đặt lại mật khẩu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await repository.ResetAsync((int)selected["ma_tk"], Convert.ToString(selected["phien_ban"])!, password.Text, reason.Text);
        changed(); await ReloadAsync(); notice.Text = "Đã đặt lại mật khẩu. Bàn giao mật khẩu qua kênh riêng cho chủ tài khoản.";
    }
    private async Task ShowAuditAsync()
    {
        using var table = await repository.AuditAsync();
        using var dialog = new Form { Text = "Nhật ký tài khoản", Size = new(S(1000), S(600)), StartPosition = FormStartPosition.CenterParent, Font = Font };
        using var list = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, DataSource = table, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        dialog.Controls.Add(list); dialog.ShowDialog(this);
    }
    private async Task RunAsync(Func<Task> work)
    {
        if (busy) return; busy = true; Enabled = false;
        try { notice.Text = "Đang xử lý…"; await work(); }
        catch (Exception error) when (error is PersistenceException or ArgumentException or InvalidOperationException) { notice.Text = error is InvalidOperationException ? "Chưa thể xử lý. Hãy tải lại dữ liệu." : error.Message; editor.ScrollControlIntoView(notice); }
        finally { if (!IsDisposed) { Enabled = true; busy = false; } }
    }
    protected override void Dispose(bool disposing) { if (disposing) { rows?.Dispose(); password.Clear(); foreach (var font in fonts) font.Dispose(); } base.Dispose(disposing); }
}
