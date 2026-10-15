using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Students;

/// <summary>Student search, profile and maintenance integrated with the Campus shell.</summary>
public sealed class StudentsPage : UserControl
{
    private sealed record Choice(string Code, string Name) { public override string ToString() => Name; }
    private readonly StudentRepository repository;
    private readonly Action changed;
    private readonly List<Font> fonts = [];
    private readonly DataGridView grid = new();
    private readonly TextBox search = new(), code = new(), name = new(), email = new(), phone = new(), address = new();
    private readonly ComboBox faculty = new(), major = new(), studentClass = new(), gender = new();
    private readonly DateTimePicker birthday = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true };
    private readonly CheckBox active = new() { Text = "Đang hoạt động", Checked = true, AutoSize = true };
    private readonly CheckBox includeInactive = new() { Text = "Gồm sinh viên ngừng hoạt động", Checked = true, AutoSize = true };
    private readonly Label count = new(), editorTitle = new();
    private DataTable choices = new();
    private StudentRecord? baseline;
    private bool busy, binding;
    private bool isNew = true;

    private int S(int value) => AppTheme.S(value);
    private Font OwnFont(float size = 9, bool bold = false) { var font = AppTheme.Font(size, bold); fonts.Add(font); return font; }
    public StudentsPage(string connectionString, Action changed)
    {
        repository = new(connectionString); this.changed = changed;
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas; Font = OwnFont(); Padding = new(S(20), S(8), S(20), S(16));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.RowStyles.Add(new(SizeType.Absolute, S(58))); root.RowStyles.Add(new(SizeType.Absolute, S(76))); root.RowStyles.Add(new(SizeType.Percent, 100));
        var heading = Label("Quản lý sinh viên", 21, true); heading.Dock = DockStyle.Fill; root.Controls.Add(heading, 0, 0);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Margin = Padding.Empty };
        search.Width = S(240); search.MaxLength = 200; search.PlaceholderText = "Mã sinh viên, họ tên hoặc email";
        foreach (var input in new[] { search, code, name, email, phone, address }) input.Font = Font;
        includeInactive.Margin = new(S(8), S(10), S(12), 0);
        var find = Button("Tìm kiếm", true); var refresh = Button("Tải lại");
        toolbar.Controls.AddRange([search, includeInactive, find, refresh]); root.Controls.Add(toolbar, 0, 1);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new(SizeType.Percent, 100)); body.ColumnStyles.Add(new(SizeType.Absolute, S(350)));
        body.RowStyles.Add(new(SizeType.Percent, 100)); root.Controls.Add(body, 0, 2);
        var list = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new(S(12)), Margin = new(0, 0, S(16), 0) };
        count.Dock = DockStyle.Bottom; count.Height = S(38); count.TextAlign = ContentAlignment.MiddleLeft; count.ForeColor = AppTheme.Muted;
        BuildGrid(); list.Controls.Add(grid); list.Controls.Add(count); body.Controls.Add(list, 0, 0);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, BackColor = Color.White, Padding = new(S(18)), Margin = Padding.Empty };
        editor.ColumnStyles.Add(new(SizeType.Percent, 100)); body.Controls.Add(editor, 1, 0);
        void Add(Control control) { int row = editor.RowCount++; editor.RowStyles.Add(new(SizeType.AutoSize)); control.Anchor = AnchorStyles.Left | AnchorStyles.Right; control.Margin = new(0, 0, 0, S(10)); editor.Controls.Add(control, 0, row); }
        void Field(string text, Control control) { var label = Label(text); label.Margin = new(0, S(3), 0, S(4)); Add(label); Add(control); }
        editorTitle.AutoSize = true; editorTitle.Font = OwnFont(12, true); Add(editorTitle);
        code.MaxLength = 20; name.MaxLength = 100; email.MaxLength = 254; phone.MaxLength = 20; address.MaxLength = 250;
        foreach (var combo in new[] { faculty, major, studentClass, gender }) { combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Font = Font; }
        birthday.MaxDate = DateTime.Today; birthday.MinDate = new(1900, 1, 1); birthday.Value = DateTime.Today.AddYears(-18); birthday.Checked = false;
        gender.Items.AddRange(["Chưa cập nhật", "Nam", "Nữ", "Khác"]); gender.SelectedIndex = 0;
        Field("Mã sinh viên *", code); Field("Họ và tên *", name); Field("Ngày sinh (chọn để nhập)", birthday); Field("Giới tính", gender);
        Field("Email", email); Field("Số điện thoại", phone); Field("Địa chỉ", address); Field("Khoa *", faculty); Field("Ngành *", major); Field("Lớp *", studentClass); Add(active);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        var save = Button("Lưu sinh viên", true); var add = Button("Thêm mới"); var status = Button("Đổi trạng thái"); var profile = Button("Xem hồ sơ");
        actions.Controls.AddRange([save, add, status, profile]); Add(actions);
        var hint = Label("Chọn một dòng để sửa. Ngừng hoạt động sẽ chặn sinh viên truy cập đăng ký học."); hint.MaximumSize = new(S(295), 0); hint.ForeColor = AppTheme.Muted; Add(hint);
        Controls.Add(root);
        faculty.SelectedIndexChanged += (_, _) => { if (!binding) PopulateMajors(); };
        major.SelectedIndexChanged += (_, _) => { if (!binding) PopulateClasses(); };
        grid.SelectionChanged += (_, _) => SelectStudent();
        find.Click += async (_, _) => { if (DiscardChanges()) await RunAsync(ReloadAsync); };
        refresh.Click += async (_, _) => { if (!DiscardChanges()) return; search.Clear(); await RunAsync(ReloadAsync); };
        search.KeyDown += async (_, args) => { if (args.KeyCode != Keys.Enter) return; args.SuppressKeyPress = true; if (DiscardChanges()) await RunAsync(ReloadAsync); };
        save.Click += async (_, _) => await RunAsync(SaveAsync);
        add.Click += (_, _) => { if (DiscardChanges()) NewStudent(); };
        status.Click += async (_, _) => await RunAsync(ChangeStatusAsync);
        profile.Click += (_, _) => ShowProfile();
        NewStudent();
    }

    private Label Label(string text, float size = 9, bool bold = false) => new() { Text = text, AutoSize = true, Font = OwnFont(size, bold), ForeColor = AppTheme.Ink, BackColor = Color.Transparent };
    private Button Button(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new(S(95), S(36)), Font = Font, Padding = new(S(10), S(5), S(10), S(5)), Margin = new(0, 0, S(8), S(8)), FlatStyle = FlatStyle.Flat, BackColor = primary ? AppTheme.Primary : Color.White, ForeColor = primary ? Color.White : AppTheme.Ink, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = primary ? AppTheme.Primary : AppTheme.Line; return button;
    }
    private void BuildGrid()
    {
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AutoGenerateColumns = false; grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = false;
        grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.RowHeadersVisible = false; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ColumnHeadersHeight = S(48); grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle = new() { BackColor = AppTheme.Lilac, ForeColor = AppTheme.Ink, Font = OwnFont(9, true), WrapMode = DataGridViewTriState.True };
        grid.DefaultCellStyle = new() { Font = Font, BackColor = Color.White, SelectionBackColor = AppTheme.AccentSoft, SelectionForeColor = AppTheme.Ink, Padding = new(S(5), S(3), S(5), S(3)) };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 250, 250); grid.RowTemplate.Height = S(42);
        void Column(string field, string title, int width) => grid.Columns.Add(new DataGridViewTextBoxColumn { Name = field, DataPropertyName = field, HeaderText = title, MinimumWidth = S(width), FillWeight = width });
        Column("ma_sv", "Mã sinh viên", 115); Column("ho_ten", "Họ và tên", 165); Column("ten_lop", "Lớp", 95); Column("email", "Email", 160); Column("trang_thai", "Trạng thái", 100);
        grid.CellFormatting += (_, args) => { if (args.ColumnIndex >= 0 && grid.Columns[args.ColumnIndex].Name == "trang_thai" && args.Value is bool state) { args.Value = state ? "Hoạt động" : "Đã ngừng"; args.FormattingApplied = true; } };
    }
    private void Bind(ComboBox combo, IEnumerable<Choice> data, string? selected = null)
    {
        bool previous = binding; binding = true;
        try { combo.DataSource = data.ToList(); if (selected != null) for (int i = 0; i < combo.Items.Count; i++) if (combo.Items[i] is Choice choice && choice.Code == selected) { combo.SelectedIndex = i; break; } }
        finally { binding = previous; }
    }
    private IEnumerable<DataRow> ChoiceRows => choices.AsEnumerable();
    private static string? Selected(ComboBox combo) => (combo.SelectedItem as Choice)?.Code;
    private void PopulateFaculties(string? selected = null) => Bind(faculty, ChoiceRows.Select(row => new Choice(row.Field<string>("ma_khoa")!, row.Field<string>("ten_khoa")!)).Distinct(), selected);
    private void PopulateMajors(string? selected = null)
    {
        Bind(major, ChoiceRows.Where(row => row.Field<string>("ma_khoa") == Selected(faculty)).Select(row => new Choice(row.Field<string>("ma_nganh")!, row.Field<string>("ten_nganh")!)).Distinct(), selected);
        PopulateClasses();
    }
    private void PopulateClasses(string? selected = null) => Bind(studentClass, ChoiceRows.Where(row => row.Field<string>("ma_nganh") == Selected(major)).Select(row => new Choice(row.Field<string>("ma_lop")!, row.Field<string>("ten_lop")!)).Distinct(), selected);
    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private StudentRecord ReadEditor() => new(code.Text.Trim(), name.Text.Trim(), birthday.Checked ? birthday.Value.Date : null,
        gender.SelectedIndex <= 0 ? null : gender.Text, Optional(email.Text), Optional(phone.Text), Optional(address.Text), Selected(major) ?? "", Selected(studentClass) ?? "", active.Checked);
    public bool HasUnsavedChanges => baseline != null && baseline != ReadEditor();
    public bool CanLeave() => !busy && DiscardChanges();
    private bool DiscardChanges() => !HasUnsavedChanges || MessageBox.Show(this, "Có thay đổi sinh viên chưa lưu. Bỏ thay đổi này?", "Campus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    public async Task ReloadAsync()
    {
        var newChoices = await repository.ChoicesAsync(); var students = await repository.ListAsync(search.Text, includeInactive.Checked);
        if (IsDisposed || Disposing) { newChoices.Dispose(); students.Dispose(); return; }
        choices.Dispose(); choices = newChoices;
        binding = true; var previous = grid.DataSource as DataTable; grid.DataSource = students; grid.ClearSelection(); binding = false; previous?.Dispose();
        count.Text = $"{students.Rows.Count} sinh viên · Chọn một dòng để xem và sửa"; NewStudent();
    }
    private void NewStudent()
    {
        binding = true; grid.ClearSelection(); isNew = true; code.ReadOnly = false; code.Clear(); name.Clear(); email.Clear(); phone.Clear(); address.Clear();
        birthday.Value = DateTime.Today.AddYears(-18); birthday.Checked = false; gender.SelectedIndex = 0; active.Checked = true;
        PopulateFaculties(); PopulateMajors(); editorTitle.Text = "Thêm sinh viên mới"; baseline = ReadEditor(); binding = false;
    }
    private void SelectStudent()
    {
        if (binding || grid.SelectedRows.Count == 0 || grid.SelectedRows[0].DataBoundItem is not DataRowView view) return;
        var student = StudentRepository.FromRow(view.Row);
        if (!isNew && baseline?.Code == student.Code) return;
        if (!DiscardChanges()) { RestoreSelection(); return; }
        binding = true; isNew = false; code.ReadOnly = true; code.Text = student.Code; name.Text = student.Name;
        birthday.Value = student.BirthDate is { } date && date >= birthday.MinDate && date <= birthday.MaxDate ? date : DateTime.Today.AddYears(-18); birthday.Checked = student.BirthDate != null;
        gender.SelectedIndex = student.Gender == null ? 0 : Math.Max(0, gender.Items.IndexOf(student.Gender)); email.Text = student.Email ?? ""; phone.Text = student.Phone ?? ""; address.Text = student.Address ?? ""; active.Checked = student.Active;
        PopulateFaculties(Convert.ToString(view.Row["ma_khoa"])); PopulateMajors(student.Major); PopulateClasses(student.Class);
        editorTitle.Text = "Hồ sơ · " + student.Code; baseline = ReadEditor(); binding = false;
    }
    private void RestoreSelection()
    {
        binding = true; grid.ClearSelection();
        if (!isNew && baseline != null) foreach (DataGridViewRow row in grid.Rows) if (Convert.ToString(row.Cells["ma_sv"].Value) == baseline.Code) { row.Selected = true; break; }
        binding = false;
    }
    private async Task SaveAsync()
    {
        await repository.SaveAsync(ReadEditor(), isNew); changed(); baseline = ReadEditor(); await ReloadAsync();
        if (!IsDisposed) count.Text += " · Đã lưu thành công";
    }
    private async Task ChangeStatusAsync()
    {
        if (isNew || baseline == null) throw new ArgumentException("Chọn sinh viên cần đổi trạng thái.");
        if (HasUnsavedChanges) throw new ArgumentException("Lưu hoặc bỏ thay đổi trong biểu mẫu trước khi đổi trạng thái.");
        if (baseline.Active && MessageBox.Show(this, "Ngừng hoạt động sẽ chặn sinh viên đăng nhập và đăng ký học. Tiếp tục?", "Campus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await repository.SetStatusAsync(baseline.Code, !baseline.Active); changed(); await ReloadAsync();
    }
    private void ShowProfile()
    {
        if (isNew || grid.SelectedRows.Count == 0 || grid.SelectedRows[0].DataBoundItem is not DataRowView view) { MessageBox.Show(this, "Chọn sinh viên để xem hồ sơ.", "Campus"); return; }
        var student = StudentRepository.FromRow(view.Row);
        MessageBox.Show(this, $"{student.Name} · {student.Code}\n\nNgày sinh: {student.BirthDate?.ToString("dd/MM/yyyy") ?? "Chưa cập nhật"}\nGiới tính: {student.Gender ?? "Chưa cập nhật"}\nEmail: {student.Email ?? "Chưa cập nhật"}\nĐiện thoại: {student.Phone ?? "Chưa cập nhật"}\nĐịa chỉ: {student.Address ?? "Chưa cập nhật"}\n\nKhoa: {view.Row["ten_khoa"]}\nNgành: {view.Row["ten_nganh"]}\nLớp: {view.Row["ten_lop"]}\nTrạng thái: {(student.Active ? "Đang hoạt động" : "Đã ngừng hoạt động")}", "Hồ sơ sinh viên", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (busy || IsDisposed) return; busy = true; Enabled = false; UseWaitCursor = true;
        try { await operation(); }
        catch (Exception error) when (error is ArgumentException or PersistenceException or InvalidOperationException) { if (!IsDisposed) MessageBox.Show(this, error.Message, "Quản lý sinh viên", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { busy = false; if (!IsDisposed) { Enabled = true; UseWaitCursor = false; } }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { choices.Dispose(); (grid.DataSource as DataTable)?.Dispose(); }
        base.Dispose(disposing);
        if (disposing) foreach (var font in fonts) font.Dispose();
    }
}
