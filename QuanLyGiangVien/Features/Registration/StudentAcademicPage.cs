using System.Data;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

/// <summary>Own academic read model. SQL binds the student identity; no management fields.</summary>
internal sealed class StudentAcademicPage : ModulePage
{
    private readonly ComboBox view = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = AppTheme.S(260) };
    private readonly DataGridView grid = Ui.Grid();
    private readonly Label note = new() { Dock = DockStyle.Top, Height = AppTheme.S(110), Padding = new(AppTheme.S(14)), BackColor = AppTheme.Lilac };
    private DataSet? data;
    private static readonly (string Table, string Title)[] Views = [("Summary", "Tín chỉ đã tích lũy"), ("DegreeAudit", "Tiến độ học phần trong CTĐT"), ("GroupProgress", "Tiến độ nhóm tự chọn"), ("Assignments", "Chương trình đang theo học"), ("Results", "Kết quả được công nhận"), ("Holds", "Khóa đăng ký và cách xử lý"), ("Standing", "Tình trạng học vụ"), ("Policies", "Quy chế áp dụng"), ("Requirements", "Điều kiện môn học")];
    internal StudentAcademicPage(RegistrationWorkspace host) : base(host)
    {
        var bar = Ui.Toolbar(); bar.Controls.AddRange([Ui.Label("Thông tin học vụ của tôi"), view]);
        view.Items.AddRange(Views.Select(item => (object)item.Title).ToArray()); view.SelectedIndex = 0;
        view.SelectedIndexChanged += (_, _) => Bind();
        Controls.Add(grid); Controls.Add(note); Controls.Add(bar);
    }
    public override async Task RefreshDataAsync()
    {
        var fresh = await Host.Academics.SnapshotAsync(); if (IsDisposed) { fresh.Dispose(); return; }
        Ui.Bind(grid, null); data?.Dispose(); data = fresh; Bind();
        note.Text = "Kết quả ở đây là dữ liệu đã có nguồn công nhận; điểm Nháp không được dùng xét tiên quyết.\n" +
            "Tín chỉ tối thiểu được cảnh báo khi chốt kế hoạch, không chặn thêm từng môn. Xem rõ lý do khóa và đơn vị xử lý trước khi đăng ký.";
    }
    private void Bind()
    {
        if (data == null || view.SelectedIndex < 0) return;
        Ui.Bind(grid, data.Tables[Views[view.SelectedIndex].Table]);
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column.Name is "version" or "actor" or "updated_at") column.Visible = false;
        }
    }
    public override void ClearData() { Ui.Bind(grid, null); data?.Dispose(); data = null; }
    protected override void Dispose(bool disposing) { if (disposing) ClearData(); base.Dispose(disposing); }
}
