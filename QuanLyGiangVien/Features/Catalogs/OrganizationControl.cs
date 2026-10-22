using System.Data;
using System.Text.Json;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Catalogs;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Catalogs;

internal sealed class OrganizationControl : UserControl
{
    private sealed record ParentChoice(string Code, string Name, bool Active)
    { public override string ToString() => Name + (Active ? "" : " (ngừng sử dụng)"); }
    private readonly OrganizationRepository repository;
    private readonly Action changed;
    private readonly DataGridView grid = Ui.Grid();
    private readonly TextBox search = new() { Width = 220, MaxLength = 100, PlaceholderText = "Tìm mã hoặc tên" };
    private readonly TextBox code = new() { Name = "txtOrganizationCode", MaxLength = 20, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox title = new() { Name = "txtOrganizationName", MaxLength = 100 };
    private readonly ComboBox parent = new() { Name = "cboOrganizationParent", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox cohort = new() { Name = "txtOrganizationCohort", MaxLength = 4 };
    private readonly TextBox years = new() { Name = "txtOrganizationYears", MaxLength = 9, PlaceholderText = "Ví dụ 2026-2030" };
    private readonly TextBox description = new() { Name = "txtOrganizationDescription", MaxLength = 500, Multiline = true, Height = 66 };
    private readonly CheckBox active = new() { Name = "chkOrganizationActive", Text = "Đang sử dụng", Checked = true, AutoSize = true };
    private readonly CheckBox includeInactive = new() { Text = "Gồm ngừng sử dụng", Checked = true, AutoSize = true, Margin = new(8, 10, 6, 4) };
    private readonly Label mode = Ui.Label("", 12, true), count = Ui.Label(""), hint = Ui.Hint("");
    private readonly Button status = Ui.Button("Ngừng sử dụng"), save = Ui.Button("Lưu", true);
    private OrganizationRecord? editing;
    private ParentChoice[] availableParents = [];
    private string baseline = "";
    private bool binding;
    internal OrganizationKind Kind { get; }
    internal DataGridView DataGrid => grid;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<string, bool> Confirm { get; set; }
    internal bool IsBusy => Ui.IsBusy(this);
    internal bool HasUnsavedChanges => baseline != Fingerprint();
    private string Entity => Kind switch { OrganizationKind.Faculty => "khoa", OrganizationKind.Major => "ngành", _ => "lớp sinh viên" };

    internal OrganizationControl(OrganizationKind kind, string connection, Action changed)
    {
        Kind = kind; repository = new(connection); this.changed = changed;
        Confirm = text => Ui.Confirm(this, text);
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas; Padding = new(12);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new(SizeType.Percent, 100)); body.ColumnStyles.Add(new(SizeType.Absolute, 350));
        body.RowStyles.Add(new(SizeType.Percent, 100)); Controls.Add(body);
        var list = new SoftSurface { Dock = DockStyle.Fill, Padding = new(14), Margin = new(0, 0, 16, 0), BackColor = Color.White };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        var find = Ui.Button("Tìm kiếm"); var reload = Ui.Button("Tải lại");
        toolbar.Controls.AddRange([search, includeInactive, find, reload]);
        count.Dock = DockStyle.Bottom; count.AutoSize = false; count.Height = 36; count.ForeColor = AppTheme.Muted;
        grid.Name = "grdOrganization"; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Ui.Column(grid, "code", "Mã", 105); Ui.Column(grid, "name", "Tên " + Entity, 190);
        if (kind != OrganizationKind.Faculty) Ui.Column(grid, "parent_name", kind == OrganizationKind.Major ? "Khoa" : "Ngành", 150);
        Ui.Column(grid, "active", "Trạng thái", 115);
        foreach (DataGridViewColumn column in grid.Columns) { column.MinimumWidth = column.Name == "name" ? 145 : 95; }
        grid.CellFormatting += (_, e) => { if (e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "active" && e.Value is bool state)
            { e.Value = state ? "Đang sử dụng" : "Ngừng sử dụng"; e.FormattingApplied = true; } };
        list.Controls.Add(grid); list.Controls.Add(count); list.Controls.Add(toolbar); body.Controls.Add(list, 0, 0);
        var surface = new SoftSurface { Dock = DockStyle.Fill, Padding = new(10), Margin = Padding.Empty, BackColor = Color.White };
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true,
            Padding = new(10, 0, 10, 8), BackColor = Color.White, Margin = Padding.Empty };
        editor.ColumnStyles.Add(new(SizeType.Percent, 100));
        surface.Controls.Add(editor); body.Controls.Add(surface, 1, 0);
        Ui.Add(editor, mode); Ui.Field(editor, "Mã " + Entity + " *", code); Ui.Field(editor, "Tên " + Entity + " *", title);
        if (kind != OrganizationKind.Faculty) Ui.Field(editor, kind == OrganizationKind.Major ? "Khoa *" : "Ngành *", parent);
        if (kind == OrganizationKind.Class)
        { Ui.Field(editor, "Khóa tuyển sinh", cohort); Ui.Field(editor, "Niên khóa", years); }
        else Ui.Field(editor, "Mô tả", description);
        Ui.Add(editor, active);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = AppTheme.S(100), WrapContents = true, Padding = new(10, 2, 10, 6) };
        var add = Ui.Button("Thêm mới"); add.Name = "btnOrganizationNew"; save.Name = "btnOrganizationSave"; status.Name = "btnOrganizationStatus";
        status.MinimumSize = new(AppTheme.S(148), AppTheme.S(34));
        actions.Controls.AddRange([save, add, status]); surface.Controls.Add(actions); Ui.Add(editor, hint);
        find.Click += async (_, _) => { if (Discard()) await Ui.RunAsync(this, () => ReloadAsync()); };
        reload.Click += async (_, _) => { if (Discard()) await Ui.RunAsync(this, () => ReloadAsync()); };
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (Discard()) await Ui.RunAsync(this, () => ReloadAsync()); } };
        includeInactive.CheckedChanged += async (_, _) =>
        {
            if (binding) return;
            if (!Discard()) { binding = true; includeInactive.Checked = !includeInactive.Checked; binding = false; return; }
            await Ui.RunAsync(this, () => ReloadAsync());
        };
        add.Click += (_, _) => { if (Discard()) ResetEditor(); };
        save.Click += async (_, _) => await Ui.RunAsync(this, SaveAsync);
        status.Click += async (_, _) => await Ui.RunAsync(this, ChangeStatusAsync);
        grid.SelectionChanged += (_, _) => SelectRow();
        ResetEditor();
    }

    private string Fingerprint() => JsonSerializer.Serialize(new object?[] { code.Text, title.Text, (parent.SelectedItem as ParentChoice)?.Code,
        active.Checked, cohort.Text, years.Text, description.Text });
    private bool Discard() => !HasUnsavedChanges || Confirm("Có thay đổi " + Entity + " chưa lưu. Bỏ các thay đổi này?");
    internal void ResetEditor()
    {
        binding = true;
        try
        {
            editing = null; grid.ClearSelection(); code.ReadOnly = false; code.Clear(); title.Clear(); cohort.Clear(); years.Clear(); description.Clear(); active.Checked = true;
            BindParents(null);
            mode.Text = "Thêm " + Entity + " mới"; status.Enabled = false; UpdateHint(); baseline = Fingerprint();
        }
        finally { binding = false; }
    }
    internal async Task ReloadAsync(string? selectCode = null)
    {
        var rows = await repository.ListAsync(Kind, search.Text, includeInactive.Checked ? null : true);
        DataTable? parents = null;
        try
        {
            if (Kind != OrganizationKind.Faculty) parents = await repository.ListAsync(Kind == OrganizationKind.Major ? OrganizationKind.Faculty : OrganizationKind.Major);
            if (IsDisposed) { rows.Dispose(); return; }
            binding = true;
            var old = grid.DataSource as DataTable; grid.DataSource = rows; old?.Dispose();
            if (parents != null)
            {
                availableParents = parents.AsEnumerable().Select(r => new ParentChoice(r.Field<string>("code")!, r.Field<string>("name")!, r.Field<bool>("active"))).ToArray();
            }
            binding = false; count.Text = $"{rows.Rows.Count} {Entity} · Chọn dòng để xem và sửa";
            ResetEditor();
            if (selectCode != null) SelectCode(selectCode);
        }
        catch { if (!ReferenceEquals(grid.DataSource, rows)) rows.Dispose(); throw; }
        finally { binding = false; parents?.Dispose(); }
    }
    private void SelectCode(string selected)
    {
        foreach (DataGridViewRow row in grid.Rows)
            if (Convert.ToString(row.Cells["code"].Value) == selected) { grid.CurrentCell = row.Cells[0]; row.Selected = true; SelectRow(); break; }
    }
    private void BindParents(string? selected)
    {
        if (Kind == OrganizationKind.Faculty) return;
        parent.DataSource = availableParents.Where(p => p.Active || p.Code == selected).ToList();
        for (int i = 0; i < parent.Items.Count; i++) if (parent.Items[i] is ParentChoice choice && choice.Code == selected) { parent.SelectedIndex = i; break; }
        parent.Enabled = parent.Items.Count > 0;
    }
    private void SelectRow()
    {
        if (binding || grid.SelectedRows.Count == 0 || grid.SelectedRows[0].DataBoundItem is not DataRowView view) return;
        var item = OrganizationRepository.FromRow(Kind, view.Row);
        if (editing?.Code == item.Code) return;
        if (!Discard())
        {
            binding = true; grid.ClearSelection(); if (editing != null) SelectCode(editing.Code); binding = false; return;
        }
        binding = true;
        try
        {
            editing = item; code.Text = item.Code; code.ReadOnly = true; title.Text = item.Name; active.Checked = item.Active;
            cohort.Text = item.Cohort?.ToString() ?? ""; years.Text = item.AcademicYears ?? ""; description.Text = item.Description ?? "";
            BindParents(item.ParentCode);
            mode.Text = "Sửa " + Entity; status.Enabled = true; status.Text = item.Active ? "Ngừng sử dụng" : "Sử dụng lại";
            UpdateHint(); baseline = Fingerprint();
        }
        finally { binding = false; }
    }
    private void UpdateHint() => hint.Text = Kind != OrganizationKind.Faculty && parent.Items.Count == 0
        ? (Kind == OrganizationKind.Major ? "Chưa có khoa đang sử dụng. Hãy tạo khoa trước." : "Chưa có ngành đang sử dụng. Hãy tạo ngành trước.")
        : "Mã được giữ nguyên sau khi tạo. Ngừng sử dụng giữ lại hồ sơ và các liên kết đã có.";
    private OrganizationRecord ReadEditor()
    {
        short? year = null;
        if (Kind == OrganizationKind.Class && !string.IsNullOrWhiteSpace(cohort.Text))
        { if (!short.TryParse(cohort.Text, out short parsed)) throw new ArgumentException("Khóa tuyển sinh phải là năm hợp lệ."); year = parsed; }
        return new(Kind, code.Text.Trim(), title.Text.Trim(), Kind == OrganizationKind.Faculty ? null : (parent.SelectedItem as ParentChoice)?.Code,
            active.Checked, year, Kind == OrganizationKind.Class && !string.IsNullOrWhiteSpace(years.Text) ? years.Text.Trim() : null,
            Kind == OrganizationKind.Class ? null : description.Text.Trim(), editing?.Version);
    }
    private async Task SaveAsync()
    {
        var item = ReadEditor(); await repository.SaveAsync(item, editing == null); changed();
        baseline = Fingerprint(); await ReloadAsync(item.Code); count.Text += " · Đã lưu";
    }
    private async Task ChangeStatusAsync()
    {
        if (editing == null) throw new ArgumentException("Chọn bản ghi cần đổi trạng thái.");
        if (HasUnsavedChanges) throw new ArgumentException("Lưu hoặc bỏ thay đổi trước khi đổi trạng thái.");
        if (!Confirm(editing.Active ? "Ngừng sử dụng " + Entity + " này? Hồ sơ hiện có được giữ lại." : "Sử dụng lại " + Entity + " này?")) return;
        var item = editing; await repository.SetStatusAsync(item, !item.Active); changed(); await ReloadAsync(item.Code);
    }
    protected override void Dispose(bool disposing)
    {
        var table = disposing ? grid.DataSource as DataTable : null;
        base.Dispose(disposing); table?.Dispose();
    }
}
