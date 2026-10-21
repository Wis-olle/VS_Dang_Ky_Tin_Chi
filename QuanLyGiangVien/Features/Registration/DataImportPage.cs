using System.Data;
using System.Globalization;
using System.Text;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Operations;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class DataImportPage : ModulePage
{
    private readonly DataImportRepository repository;
    private readonly DataGridView grid = Ui.Grid();
    private readonly TextBox content = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false };
    private readonly CheckBox confirmed = new() { Text = "Tôi đã đối chiếu và xác nhận nguồn dữ liệu", AutoSize = true };
    private static string KindCaption(string value) => value switch {
        "PROGRAM" => "Chương trình đào tạo", "GROUP" => "Nhóm học phần", "COURSE" => "Học phần trong CTĐT",
        "REQUIREMENT" => "Điều kiện học phần", "ASSIGNMENT" => "Gắn CTĐT cho sinh viên", "RESULT" => "Kết quả học tập",
        "STUDENT" => "Hồ sơ sinh viên", "CLASS" => "Lớp học phần", _ => value
    };
    private readonly ComboBox kind = PhaseThreeUi.Choices(DataImport.Kinds.Select(k => new WorkflowChoice(k, KindCaption(k))).ToArray());
    private readonly Button commit = Ui.Button("Ghi toàn bộ đợt nhập", true);
    private readonly Label notice = Ui.Label("Nạp JSON có nguồn xác nhận → xem trước từng dòng → ghi toàn bộ. Không tự công bố CTĐT.");
    private readonly TabControl sections = new() { Dock = DockStyle.Fill };
    private DataTable? data; private string? document; private string? validatedDocument;
    public override bool HasUnsavedChanges => document is not null;
    public DataImportPage(RegistrationWorkspace host) : base(host)
    {
        Name = "DataImportPage"; repository = new(host.ConnectionString);
        grid.CellFormatting += (_, e) => {
            if (e.ColumnIndex < 0) return;
            // TextBox cells require a string once FormattingApplied is true.
            // Keep the zero-based source index unchanged for import reconciliation.
            if (grid.Columns[e.ColumnIndex].Name == "row_index" && e.Value is int index) {
                e.Value = (index + 1).ToString(CultureInfo.CurrentCulture); e.FormattingApplied = true;
            }
            if (grid.Columns[e.ColumnIndex].Name == "kind" && e.RowIndex >= 0 && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string value) { e.Value = KindCaption(value); e.FormattingApplied = true; }
            if (grid.Columns[e.ColumnIndex].Name == "message" && e.Value is string message) { e.Value = message.Replace("Batch", "Đợt nhập", StringComparison.Ordinal).Replace("batch", "đợt nhập", StringComparison.Ordinal); e.FormattingApplied = true; }
        };
        grid.DataBindingComplete += (_, _) => FormatColumns();
        grid.VisibleChanged += (_, _) => { if (grid.Visible) FormatColumns(); };
        var input = new TabPage("Tệp & nguồn") { BackColor = Color.White }; var preview = new TabPage("Đối soát từng dòng") { BackColor = Color.White };
        var tools = Ui.Toolbar(); var load = Ui.Button("Nạp JSON"); var template = Ui.Button("Lưu tệp mẫu"); var clear = Ui.Button("Bỏ tệp");
        kind.Width = AppTheme.S(225); tools.Controls.AddRange([load, kind, template, clear]); input.Controls.Add(content); input.Controls.Add(tools);
        var actions = Ui.Toolbar(); var check = Ui.Button("Kiểm tra không ghi"); check.Name = "btnImportPreview"; commit.Name = "btnImportCommit";
        actions.Controls.AddRange([check, confirmed, commit]); preview.Controls.Add(grid); preview.Controls.Add(actions);
        sections.TabPages.AddRange([input, preview]); notice.AutoSize = false; notice.Dock = DockStyle.Top; notice.Padding = new(AppTheme.S(8)); notice.Height = AppTheme.S(44);
        Controls.Add(sections); Controls.Add(notice); commit.Enabled = false;
        load.Click += (_, _) => {
            using var picker = new OpenFileDialog { Filter = "Campus JSON (*.json)|*.json", CheckFileExists = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try {
                if (new FileInfo(picker.FileName).Length > 4_000_000) throw new ArgumentException("Tệp vượt giới hạn 4 MB UTF-8.");
                LoadDocument(File.ReadAllText(picker.FileName, Encoding.UTF8));
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { MessageBox.Show(this, e is ArgumentException ? e.Message : "Không đọc được tệp đã chọn.", "Campus"); }
        };
        template.Click += (_, _) => {
            using var picker = new SaveFileDialog { Filter = "Campus JSON (*.json)|*.json", FileName = "Campus-" + PhaseThreeUi.Choice(kind) + ".json" };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(picker.FileName, DataImport.Template(PhaseThreeUi.Choice(kind)), new UTF8Encoding(false)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(this, "Không ghi được tệp mẫu vào vị trí đã chọn.", "Campus"); }
        };
        clear.Click += (_, _) => { document = validatedDocument = null; content.Clear(); confirmed.Checked = false; commit.Enabled = false; ClearData(); };
        confirmed.CheckedChanged += (_, _) => commit.Enabled = confirmed.Checked && document is not null && document == validatedDocument;
        check.Click += async (_, _) => await Host.ExecuteAsync(async () => {
            if (document is null) throw new ArgumentException("Nạp tệp trước khi kiểm tra.");
            var fresh = await repository.PreviewAsync(document); if (IsDisposed) { fresh.Dispose(); return; }
            Bind(fresh); validatedDocument = data!.AsEnumerable().All(r => (bool)r["valid"]) ? document : null;
            commit.Enabled = validatedDocument == document && confirmed.Checked;
            notice.Text = validatedDocument is null ? "Có lỗi; chưa ghi dòng nào. Sửa nguồn rồi nạp lại tệp." : "Xem trước hợp lệ. Xác nhận nguồn để ghi; khi ghi SQL sẽ kiểm lại toàn bộ.";
        });
        commit.Click += async (_, _) => await Host.ExecuteAsync(async () => {
            if (document is null || document != validatedDocument || !confirmed.Checked) throw new ArgumentException("Kiểm tra và xác nhận nguồn trước khi ghi.");
            var fresh = await repository.CommitAsync(document, true); if (IsDisposed) { fresh.Dispose(); return; } Bind(fresh);
            if (data!.AsEnumerable().All(r => (bool)r["committed"])) {
                document = validatedDocument = null; confirmed.Checked = false; commit.Enabled = false;
                notice.Text = "Đã ghi toàn bộ đợt nhập. Nếu mất phản hồi, nạp lại đúng tệp và mã đợt để đối soát.";
            } else { validatedDocument = null; commit.Enabled = false; notice.Text = "Dữ liệu đổi hoặc có lỗi; chưa ghi dòng nào trong đợt. Xem từng dòng và kiểm tra lại."; }
        });
    }
    internal void LoadDocument(string text)
    {
        var batch = DataImport.Parse(text); document = batch.Json; validatedDocument = null; confirmed.Checked = false; commit.Enabled = false;
        content.Text = batch.Json; notice.Text = $"{batch.Rows.Length} dòng • Nguồn: {batch.Source}";
        notice.AccessibleDescription = $"Mã đợt nhập: {batch.Id}. Đối chiếu từng dòng trước khi xác nhận nguồn và ghi.";
        ClearData(); sections.SelectedIndex = 1;
    }
    private void Bind(DataTable fresh) { ClearData(); data = fresh; Ui.Bind(grid, data); }
    private void FormatColumns()
    {
        if (grid.Columns.Contains("kind")) grid.Columns["kind"]!.HeaderText = "Loại dữ liệu";
        if (grid.Columns.Contains("row_index")) { grid.Columns["row_index"]!.MinimumWidth = AppTheme.S(55); grid.Columns["row_index"]!.FillWeight = 45; }
    }
    public override Task RefreshDataAsync() => Task.CompletedTask;
    public override void ClearData() { Ui.Bind(grid, null); data?.Dispose(); data = null; }
}
