using System.Data;
using System.Globalization;
using System.Text.Json;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.Academics.AcademicUi;

namespace QuanLyGiangVien.Features.Academics;

/// <summary>A typed academic editor. Snapshot changes never replace an unsaved form.</summary>
internal sealed class AcademicSection : UserControl
{
    private readonly AcademicField[] fields;
    private readonly Dictionary<string, Control> inputs = [];
    private readonly List<Font> fonts = [];
    private readonly DataGridView grid = Grid();
    private readonly TextBox search = new();
    private readonly Label notice = new(), state = new(), count = new();
    private readonly Panel editorHost = new SoftSurface();
    private readonly Button save = Button("Lưu thay đổi", true), publish = Button("Công bố"), add = Button("Tạo mới");
    private readonly string idColumn, title;
    private readonly Func<AcademicSection, Task> persist;
    private readonly Func<AcademicSection, Task>? release;
    private string baseline = "", selectedId = "", version = "";
    private bool binding, busy, rowLocked;
    internal string TableName { get; }
    internal string Kind { get; }
    internal string SelectedId => selectedId;
    internal string Version => version;
    internal bool IsBusy => busy;
    internal bool HasUnsavedChanges => baseline != Fingerprint();
    internal DataGridView DataGrid => grid;
    internal Control Input(string key) => inputs[key];
    internal string Notice => notice.Text;
    private HashSet<string> publishedPrograms = [];
    private DataSet? references;

    internal AcademicSection(string title, string description, string table, string kind, string id,
        AcademicColumn[] columns, AcademicField[] fields, Func<AcademicSection, Task> persist,
        Func<AcademicSection, Task>? release = null)
    {
        this.title = title; TableName = table; Kind = kind; idColumn = id; this.fields = fields;
        this.persist = persist; this.release = release;
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas; Font = OwnFont();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.RowStyles.Add(new(SizeType.Absolute, S(64))); layout.RowStyles.Add(new(SizeType.Percent, 100));
        var intro = Label(description); intro.Dock = DockStyle.Fill; intro.MaximumSize = Size.Empty; intro.Padding = new(S(4), S(12), 0, 0);
        intro.ForeColor = AppTheme.Muted; layout.Controls.Add(intro, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new(SizeType.Percent, 100)); body.ColumnStyles.Add(new(SizeType.Absolute, S(390)));
        body.RowStyles.Add(new(SizeType.Percent, 100)); layout.Controls.Add(body, 0, 1);
        var list = new SoftSurface { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new(S(14)), Margin = new(0, 0, S(14), 0) };
        search.Dock = DockStyle.Top; search.Height = S(32); search.PlaceholderText = "Lọc danh sách…"; search.AccessibleName = "Lọc " + title;
        count.Dock = DockStyle.Bottom; count.Height = S(36); count.ForeColor = AppTheme.Muted;
        grid.AccessibleName = "Danh sách " + title; grid.Font = Font; grid.ColumnHeadersDefaultCellStyle.Font = OwnFont(9, true);
        foreach (var column in columns) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = column.Key, DataPropertyName = column.Key,
            HeaderText = column.Caption, MinimumWidth = S(Math.Min(column.Width, 85)), FillWeight = column.Width });
        grid.CellFormatting += (_, args) =>
        {
            if (args.Value is bool value) { args.Value = value ? "Có" : "Không"; args.FormattingApplied = true; }
            if (args.ColumnIndex < 0 || args.Value is not string code) return;
            var definition = fields.FirstOrDefault(f => f.Key == grid.Columns[args.ColumnIndex].Name);
            var option = definition?.Options?.FirstOrDefault(o => o.Value == code);
            if (option != null) { args.Value = option.Caption; args.FormattingApplied = true; }
        };
        list.Controls.Add(grid); list.Controls.Add(search); list.Controls.Add(count); body.Controls.Add(list, 0, 0);
        editorHost.Dock = DockStyle.Fill; editorHost.BackColor = Color.White; editorHost.Padding = new(S(16));
        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Margin = Padding.Empty };
        form.ColumnStyles.Add(new(SizeType.Percent, 100)); editorHost.Controls.Add(form); body.Controls.Add(editorHost, 1, 0);
        void Add(Control c) { int row = form.RowCount++; form.RowStyles.Add(new(SizeType.AutoSize)); c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            c.Margin = new(0, 0, 0, S(9)); form.Controls.Add(c, 0, row); }
        state.AutoSize = true; state.Font = OwnFont(12, true); state.ForeColor = AppTheme.Ink; Add(state);
        notice.AutoSize = true; notice.MaximumSize = new(S(333), 0); notice.ForeColor = AppTheme.Danger; notice.Visible = false; Add(notice);
        if (kind == "POLICY") Add(PolicyPresets());
        int tab = 0;
        foreach (var field in fields)
        {
            var caption = Label(field.Caption + (field.Required ? " *" : "")); caption.Margin = new(0, S(4), 0, S(4)); Add(caption);
            var input = BuildInput(field); input.Name = field.Key; input.AccessibleName = field.Caption; input.TabIndex = tab++;
            if (field.Key == "id" && input is TextBox idInput) idInput.MaxLength = 20;
            if (field.Key == "source" && kind == "POLICY" && input is TextBox sourceInput) sourceInput.MaxLength = 500;
            input.Font = Font; inputs.Add(field.Key, input); Add(input);
            if (field.Help is { Length: > 0 }) { var help = Label(field.Help); help.ForeColor = AppTheme.Muted; help.MaximumSize = new(S(333), 0); Add(help); }
        }
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        foreach (var button in new[] { save, add, publish }) { button.Font = Font; button.FlatAppearance.BorderColor = AppTheme.Line; }
        publish.Visible = release != null; actions.Controls.AddRange([save, add, publish]); Add(actions);
        search.TextChanged += (_, _) => Filter(); grid.SelectionChanged += (_, _) => SelectRow();
        foreach (var key in new[] { "program", "student" })
            if (inputs.TryGetValue(key, out var c) && c is ComboBox combo)
                combo.SelectedIndexChanged += (_, _) => { if (!binding) RefreshDependentChoices(false); };
        save.Click += async (_, _) => await RunAsync(() => this.persist(this));
        publish.Click += async (_, _) =>
        {
            if (HasUnsavedChanges) { ShowNotice("Lưu bản nháp trước khi công bố."); return; }
            if (selectedId.Length == 0) { ShowNotice("Chọn bản nháp cần công bố."); return; }
            if (MessageBox.Show(this, "Công bố sẽ khóa phiên bản này và áp dụng quy tắc cho đăng ký học. Tiếp tục?", title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) await RunAsync(() => this.release!(this));
        };
        add.Click += (_, _) => { if (ConfirmDiscard()) New(); };
        Controls.Add(layout); New();
    }

    private Font OwnFont(float size = 9, bool bold = false) { var f = AppTheme.Font(size, bold); fonts.Add(f); return f; }
    private Label Label(string text) => new() { AutoSize = true, Text = text, Font = Font, ForeColor = AppTheme.Ink, BackColor = Color.Transparent, UseMnemonic = false };
    private Control BuildInput(AcademicField f)
    {
        if (f.Type == AcademicFieldType.Check) return new CheckBox { AutoSize = true, Text = "Bật", Checked = f.Default == "true" };
        if (f.Type == AcademicFieldType.Choice) return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = S(30), IntegralHeight = false, DropDownHeight = S(250) };
        if (f.Type == AcademicFieldType.Date) return new DateTimePicker { ShowCheckBox = !f.Required, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Checked = f.Required, Width = S(290) };
        return new TextBox { Multiline = f.Type == AcademicFieldType.List, Height = S(f.Type == AcademicFieldType.List ? 80 : 28),
            ScrollBars = f.Type == AcademicFieldType.List ? ScrollBars.Vertical : ScrollBars.None, MaxLength = f.Type == AcademicFieldType.List ? 12000 : 1000 };
    }
    private Control PolicyPresets()
    {
        var box = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = AppTheme.Lilac, Padding = new(S(10)) };
        box.Controls.Add(Label("Tham khảo UIT · QĐ790/2022 + QĐ1393/2023"));
        var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(300), AccessibleName = "Bộ mẫu quy chế tham khảo UIT" };
        choice.Items.AddRange(["Kỳ chính · 14–24 TC; ĐTBC ≥8 được 30", "Kỳ hè · 0–12 TC", "Cảnh báo học tập · chỉ học lại"]); choice.SelectedIndex = 0;
        box.Controls.Add(choice); var apply = Button("Điền mẫu vào bản nháp"); apply.Font = Font; box.Controls.Add(apply);
        var help = Label("Chọn đúng học kỳ, kiểm tra nguồn và phạm vi trước khi công bố. Điền mẫu chưa lưu hay áp dụng quy chế."); help.MaximumSize = new(S(300), 0); help.ForeColor = AppTheme.Muted; box.Controls.Add(help);
        apply.Click += (_, _) =>
        {
            if (busy || rowLocked) { ShowNotice("Phiên bản đã công bố chỉ để xem. Chọn Tạo mới để lập bản nháp khác."); return; }
            bool summer = choice.SelectedIndex == 1, warning = choice.SelectedIndex == 2;
            var values = new Dictionary<string, string>
            {
                ["name"] = "Tham khảo UIT 790/2022 +1393/2023 · " + (summer ? "hè" : warning ? "cảnh báo học tập" : "kỳ chính"),
                ["source"] = "UIT QĐ790/2022: https://daa.uit.edu.vn/sites/daa/files/202309/790-qd-dhcntt_28-9-22_quy_che_dao_tao.pdf ; QĐ1393/2023: https://oep.uit.edu.vn/sites/oep/files/uploads/files/202409/1393-qd-dhcntt_29-12-2023_cap_nhat_quy_che_dao_tao_theo_hoc_che_tin_chi_cho_he_dai_hoc_chinh_quy.pdf",
                ["standing"] = warning ? "WARNING" : "*", ["minCredits"] = summer ? "0" : "14", ["maxCredits"] = summer ? "12" : "24",
                ["passGrade"] = "5", ["maxAttempts"] = "0", ["allowImprove"] = "true", ["enforceCurriculum"] = "true",
                ["excellentThreshold"] = summer || warning ? "" : "8", ["excellentMaxCredits"] = summer || warning ? "" : "30", ["warningRetakeOnly"] = warning ? "true" : "false"
            };
            if (warning) values["source"] += " ; Cảnh báo: https://student.uit.edu.vn/mot-so-quy-trinh-danh-cho-sinh-vien";
            foreach (var pair in values) Set(inputs[pair.Key], pair.Value);
            ShowNotice("Đã điền mẫu tham khảo vào bản nháp; chưa lưu và chưa áp dụng. Kiểm tra học kỳ và mã học lực trước khi lưu.");
        };
        return box;
    }
    internal void BindReferences(DataSet snapshot)
    {
        // Initial choice defaults become selectable only after their data sources exist.
        // Capture the user's draft state first so this system binding is not mistaken for editing.
        bool hadUnsavedChanges = HasUnsavedChanges;
        references = snapshot;
        publishedPrograms = snapshot.Tables.Contains("Programs") ? snapshot.Tables["Programs"]!.AsEnumerable()
            .Where(r => RowText(r, "published").Equals("true", StringComparison.OrdinalIgnoreCase) || RowText(r, "published") == "1")
            .Select(r => RowText(r, "id")).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
        binding = true;
        try
        {
            foreach (var f in fields.Where(f => f.Type == AcademicFieldType.Choice))
            {
                var combo = (ComboBox)inputs[f.Key]; string previous = (combo.SelectedItem as AcademicOption)?.Value ?? f.Default;
                var items = new List<AcademicOption>(); if (!f.Required) items.Add(new("", "— Không chọn —"));
                if (f.Options != null) items.AddRange(f.Options);
                if (f.ReferenceTable != null && snapshot.Tables.Contains(f.ReferenceTable))
                    items.AddRange(snapshot.Tables[f.ReferenceTable]!.AsEnumerable().Select(r => new AcademicOption(RowText(r, f.ReferenceId!),
                        RowText(r, f.ReferenceId!) + " · " + RowText(r, f.ReferenceName!))));
                // A referenced record can be retired later; preserve a current draft's original value.
                if (previous.Length > 0 && items.All(x => x.Value != previous)) items.Add(new(previous, previous + " · đã lưu"));
                combo.DataSource = items.DistinctBy(x => x.Value).ToArray(); Set(combo, previous);
            }
        }
        finally { binding = false; }
        RefreshDependentChoices(true);
        if (!hadUnsavedChanges) baseline = Fingerprint();
    }
    private void RefreshDependentChoices(bool preserveExisting)
    {
        if (references == null) return;
        bool previousBinding = binding; binding = true;
        try
        {
            string program = inputs.TryGetValue("program", out var p) ? Value(p) : "";
            foreach (var f in fields.Where(f => f.Type == AcademicFieldType.Choice && f.ReferenceTable != null))
            {
                if (f.Key != "group" && !(Kind == "REQUIREMENT" && f.ReferenceTable == "CourseChoices") && !(Kind == "ASSIGNMENT" && f.Key == "program")) continue;
                if (!references.Tables.Contains(f.ReferenceTable!)) continue;
                var rows = references.Tables[f.ReferenceTable!]!.AsEnumerable();
                if (f.Key == "group") rows = rows.Where(r => RowText(r, "program") == program);
                if (Kind == "REQUIREMENT" && f.ReferenceTable == "CourseChoices")
                {
                    var included = references.Tables["Courses"]?.AsEnumerable().Where(r => RowText(r, "program") == program).Select(r => RowText(r, "course")).ToHashSet() ?? [];
                    rows = rows.Where(r => included.Contains(RowText(r, "id")));
                }
                if (Kind == "ASSIGNMENT" && f.Key == "program")
                {
                    string student = Value(inputs["student"]);
                    var studentRow = references.Tables["Students"]?.AsEnumerable().FirstOrDefault(r => RowText(r, "id") == student);
                    rows = rows.Where(r => publishedPrograms.Contains(RowText(r, "id")) && (studentRow == null ||
                        RowText(r, "major") == RowText(studentRow, "major") && RowText(r, "cohort") == RowText(studentRow, "cohort")));
                }
                var combo = (ComboBox)inputs[f.Key]; string value = Value(combo);
                var options = rows.Select(r => new AcademicOption(RowText(r, "id"), RowText(r, "id") + " · " + RowText(r, f.ReferenceName!))).ToList();
                if (!f.Required) options.Insert(0, new("", "— Không chọn —"));
                if (preserveExisting && value.Length > 0 && options.All(o => o.Value != value)) options.Add(new(value, value + " · đã lưu"));
                combo.DataSource = options; Set(combo, value);
            }
        }
        finally { binding = previousBinding; }
    }
    internal void BindTable(DataTable source, string? select = null)
    {
        binding = true;
        try
        {
            var old = grid.DataSource as DataTable; grid.DataSource = source.Copy(); old?.Dispose(); Filter();
            grid.ClearSelection();
            string target = select ?? selectedId;
            var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.DataBoundItem is DataRowView v && RowText(v.Row, idColumn).Equals(target, StringComparison.OrdinalIgnoreCase));
            if (row != null) { grid.CurrentCell = row.Cells[0]; row.Selected = true; }
        }
        finally { binding = false; }
        if (select != null) SelectRow(force: true);
        count.Text = $"{grid.Rows.Count} mục · Chọn dòng để xem và sửa";
    }
    private void Filter()
    {
        if (grid.DataSource is not DataTable table) return;
        string escaped = search.Text.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
        table.DefaultView.RowFilter = escaped.Length == 0 ? "" : string.Join(" OR ", table.Columns.Cast<DataColumn>()
            .Where(c => c.DataType == typeof(string)).Select(c => $"[{c.ColumnName}] LIKE '%{escaped}%'"));
    }
    private void SelectRow(bool force = false)
    {
        if (binding || grid.SelectedRows.Count == 0 || grid.SelectedRows[0].DataBoundItem is not DataRowView view) return;
        string next = RowText(view.Row, idColumn); if (!force && next == selectedId) return;
        if (!force && !ConfirmDiscard()) { RestoreSelection(); return; }
        binding = true;
        try
        {
            selectedId = next; version = RowText(view.Row, "version");
            foreach (var f in fields) Set(inputs[f.Key], RowText(view.Row, f.Key));
            RefreshDependentChoices(true);
            bool published = RowText(view.Row, "published").Equals("true", StringComparison.OrdinalIgnoreCase) || RowText(view.Row, "published") == "1"
                || TableName is "Groups" or "Courses" or "Requirements" && publishedPrograms.Contains(RowText(view.Row, "program"));
            rowLocked = published;
            foreach (var input in inputs.Values) input.Enabled = !published;
            if (inputs.TryGetValue(idColumn, out var id)) id.Enabled = false;
            save.Enabled = !published; publish.Enabled = !published; notice.Visible = false;
            state.Text = published ? "Đã công bố · chỉ xem" : title + " · " + selectedId;
            state.ForeColor = published ? AppTheme.Green : AppTheme.Ink;
            baseline = Fingerprint();
        }
        finally { binding = false; }
    }
    private void RestoreSelection()
    {
        binding = true; grid.ClearSelection();
        foreach (DataGridViewRow row in grid.Rows) if (row.DataBoundItem is DataRowView v && RowText(v.Row, idColumn) == selectedId)
        { grid.CurrentCell = row.Cells[0]; row.Selected = true; break; }
        binding = false;
    }
    internal void New()
    {
        selectedId = version = ""; rowLocked = false; binding = true; grid.ClearSelection();
        foreach (var f in fields)
        {
            inputs[f.Key].Enabled = true; Set(inputs[f.Key], f.Default);
            if (f.Type == AcademicFieldType.Date && f.Required && inputs[f.Key] is DateTimePicker date)
                date.Value = f.Key == "expires" ? DateTime.Now.AddMonths(1) : DateTime.Now;
        }
        binding = false; save.Enabled = true; publish.Enabled = false; state.Text = title + " · tạo mới"; state.ForeColor = AppTheme.Ink;
        RefreshDependentChoices(false);
        notice.Visible = false; baseline = Fingerprint();
    }
    internal Dictionary<string, object?> ReadValues()
    {
        var values = new Dictionary<string, object?>();
        foreach (var f in fields)
        {
            var input = inputs[f.Key]; string text = Value(input);
            if (f.Required && text.Length == 0) throw new ArgumentException($"{f.Caption}: vui lòng nhập hoặc chọn giá trị.");
            values[f.Key] = f.Type switch
            {
                AcademicFieldType.Integer when text.Length > 0 => int.TryParse(text, out int n) ? n : throw new ArgumentException($"{f.Caption}: nhập số nguyên."),
                AcademicFieldType.Decimal when text.Length > 0 => decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) ? n : throw new ArgumentException($"{f.Caption}: nhập một số hợp lệ."),
                AcademicFieldType.Check => ((CheckBox)input).Checked,
                AcademicFieldType.List => text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                _ => text.Length == 0 ? null : text
            };
        }
        if (Kind == "RESULT" && Convert.ToString(values["recognition"]) == "GRADED" && values["grade"] == null)
            throw new ArgumentException("Kết quả có điểm đánh giá cần nhập điểm chính thức.");
        return values;
    }
    internal string RecordId => selectedId.Length > 0 ? selectedId : inputs.TryGetValue(idColumn, out var c) ? Value(c) : "";
    private string Fingerprint() => JsonSerializer.Serialize(fields.ToDictionary(f => f.Key, f => Value(inputs[f.Key])));
    private static string Value(Control c) => c switch { ComboBox cb => (cb.SelectedItem as AcademicOption)?.Value ?? "", CheckBox cb => cb.Checked ? "true" : "false",
        DateTimePicker p => p.Checked ? p.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "", _ => c.Text.Trim() };
    private static void Set(Control c, string value)
    {
        switch (c)
        {
            case ComboBox combo:
                combo.SelectedIndex = -1;
                for (int i = 0; i < combo.Items.Count; i++) if (combo.Items[i] is AcademicOption o && o.Value == value) { combo.SelectedIndex = i; break; }
                break;
            case CheckBox check: check.Checked = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1"; break;
            case DateTimePicker date:
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, out var parsed)) { date.Value = parsed < date.MinDate ? date.MinDate : parsed > date.MaxDate ? date.MaxDate : parsed; date.Checked = true; }
                else date.Checked = !date.ShowCheckBox;
                break;
            default: c.Text = value; break;
        }
    }
    internal static string RowText(DataRow row, string column) => !row.Table.Columns.Contains(column) || row[column] == DBNull.Value ? "" :
        row[column] is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? "";
    internal bool ConfirmDiscard() => !HasUnsavedChanges || MessageBox.Show(this, "Có thay đổi học vụ chưa lưu. Bỏ thay đổi này?", "Campus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    internal async Task RunAsync(Func<Task> action)
    {
        if (busy || IsDisposed) return; busy = true; UseWaitCursor = true; save.Enabled = add.Enabled = publish.Enabled = false;
        foreach (var input in inputs.Values) input.Enabled = false;
        grid.Enabled = search.Enabled = false;
        try { await action(); }
        catch (Exception error) when (error is PersistenceException or ArgumentException or InvalidOperationException)
        { if (!IsDisposed) ShowNotice(error.Message.Contains("Bản nháp", StringComparison.OrdinalIgnoreCase) ? error.Message : error.Message + " Bản nháp vẫn được giữ; kiểm tra lại trước khi lưu."); }
        finally
        {
            busy = false;
            if (!IsDisposed)
            {
                UseWaitCursor = false; grid.Enabled = search.Enabled = true;
                foreach (var pair in inputs) pair.Value.Enabled = !rowLocked && (pair.Key != idColumn || selectedId.Length == 0);
                save.Enabled = !rowLocked; add.Enabled = true; publish.Enabled = release != null && selectedId.Length > 0 && !rowLocked;
            }
        }
    }
    internal void ShowNotice(string message)
    {
        notice.Text = message; notice.Visible = true;
        notice.ForeColor = message.StartsWith("Đã lưu", StringComparison.Ordinal) ? AppTheme.Green : message.StartsWith("Đã điền", StringComparison.Ordinal) ? AppTheme.PrimaryPressed : AppTheme.Danger;
        if (notice.Parent is ScrollableControl scroll) { scroll.PerformLayout(); scroll.ScrollControlIntoView(notice); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) (grid.DataSource as DataTable)?.Dispose();
        base.Dispose(disposing); if (disposing) foreach (var font in fonts) font.Dispose();
    }
}
