using System.Data;
using System.Text.Json;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Academics;
using QuanLyGiangVien.Features.Accounts;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.Academics.AcademicUi;

namespace QuanLyGiangVien.Features.Academics;

public sealed class AcademicPage : UserControl
{
    private readonly AcademicRepository repository;
    private readonly Action dataChanged;
    private readonly List<AcademicSection> sections = [];
    private readonly List<Font> fonts = [];
    private readonly TabControl tabs = new();
    private readonly Label notice = new();
    private readonly AccountAdminPage accounts;
    private readonly AcademicHistory history;
    private DataSet snapshot = new();
    private bool busy;
    public bool HasUnsavedChanges => sections.Any(s => s.HasUnsavedChanges) || accounts.HasUnsavedChanges;
    public bool IsBusy => busy || sections.Any(s => s.IsBusy);
    internal IReadOnlyList<AcademicSection> Sections => sections;

    public AcademicPage(string connectionString, Action dataChanged)
    {
        repository = new(connectionString); this.dataChanged = dataChanged;
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas; Font = OwnFont(); Padding = new(S(20), S(8), S(20), S(16));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.RowStyles.Add(new(SizeType.Absolute, S(56))); root.RowStyles.Add(new(SizeType.Absolute, S(50))); root.RowStyles.Add(new(SizeType.Percent, 100));
        var heading = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "Học vụ & điều kiện đăng ký", UseMnemonic = false, Font = OwnFont(21, true), ForeColor = AppTheme.Ink, Dock = DockStyle.Fill };
        var reload = Button("Tải lại"); reload.Dock = DockStyle.Right; reload.Font = Font;
        heading.Controls.Add(title); heading.Controls.Add(reload); root.Controls.Add(heading, 0, 0);
        notice.Text = "CTĐT/quy chế chỉ áp dụng sau công bố. Khóa đăng ký và hồ sơ học vụ có hiệu lực khi lưu; kết quả chỉ được xét khi đã công bố.";
        notice.BackColor = AppTheme.SubtleLilac; notice.ForeColor = AppTheme.Muted; notice.Dock = DockStyle.Fill;
        notice.Padding = new(S(12), S(8), S(12), S(8)); notice.AccessibleName = "Trạng thái áp dụng quy chế"; root.Controls.Add(notice, 0, 1);
        tabs.Dock = DockStyle.Fill; tabs.Font = OwnFont(10); tabs.AccessibleName = "Phân hệ học vụ"; root.Controls.Add(tabs, 0, 2);
        var curriculum = GroupTab("Chương trình đào tạo");
        Add(curriculum, "Phiên bản", "Mỗi ngành và khóa có chương trình riêng. Khi đã công bố, hãy tạo phiên bản mới để bảo toàn lịch sử.", "Programs", "PROGRAM", AcademicFields.Programs,
            [new("id", "Mã phiên bản"), new("name", "Chương trình", 180), new("cohort", "Khóa", 75), new("published", "Công bố", 85)], true);
        Add(curriculum, "Nhóm tự chọn", "Khai báo số tín chỉ cần đạt của từng nhóm. Nhóm thuộc một phiên bản chương trình.", "Groups", "GROUP", AcademicFields.Groups,
            [new("id", "Mã nhóm"), new("program", "CTĐT"), new("name", "Tên nhóm", 150), new("requiredCredits", "TC cần đạt", 85)]);
        Add(curriculum, "Học phần & tương đương", "Phân biệt môn bắt buộc và nhóm tự chọn; nhóm tương đương được tính tín chỉ một lần.", "Courses", "COURSE", AcademicFields.Courses,
            [new("program", "CTĐT"), new("course", "Học phần"), new("required", "Bắt buộc", 85), new("equivalence", "Nhóm tương đương")]);
        Add(curriculum, "Điều kiện môn học", "Điều kiện cùng nhóm là OR; các nhóm khác nhau là AND. Hệ thống kiểm chu trình và xét lại khi đăng ký hoặc hủy.", "Requirements", "REQUIREMENT", AcademicFields.Requirements,
            [new("course", "Môn đăng ký"), new("group", "Nhóm OR", 80), new("requiredCourse", "Môn điều kiện"), new("kind", "Loại", 80), new("minGrade", "Điểm", 65)]);
        var rules = GroupTab("Quy chế & ngoại lệ");
        Add(rules, "Quy chế tín chỉ", "Đặt giới hạn theo học kỳ và học lực; lưu nháp để rà soát rồi công bố. Không tự áp dụng một quy chế giả định cho trường.", "Policies", "POLICY", AcademicFields.Policies,
            [new("name", "Quy chế", 180), new("term", "Học kỳ"), new("standing", "Học lực", 90), new("maxCredits", "TC tối đa", 85), new("published", "Công bố", 85)], true);
        Add(rules, "Duyệt học vượt", "Ngoại lệ theo sinh viên và học kỳ phải có căn cứ duyệt và ngày hết hiệu lực.", "Overrides", "OVERRIDE", AcademicFields.Overrides,
            [new("student", "Sinh viên"), new("term", "Học kỳ"), new("maxCredits", "TC tối đa", 85), new("reason", "Căn cứ", 150)]);
        var students = GroupTab("Hồ sơ & khóa đăng ký");
        Add(students, "Gắn chương trình", "Gắn sinh viên với đúng phiên bản chương trình đã công bố. Không dùng bản mới để sửa ngược lịch sử bản cũ.", "Assignments", "ASSIGNMENT", AcademicFields.Assignments,
            [new("student", "Sinh viên", 130), new("program", "Phiên bản CTĐT", 190)]);
        Add(students, "Tình trạng học vụ", "Trạng thái và mã học lực theo học kỳ quyết định sinh viên có được đăng ký và hạn mức nào được áp dụng.", "Standing", "STANDING", AcademicFields.Standing,
            [new("student", "Sinh viên"), new("term", "Học kỳ"), new("status", "Trạng thái"), new("standing", "Học lực")]);
        Add(students, "Khóa / gỡ khóa", "Khóa có lý do, đơn vị xử lý, phạm vi và thời hạn. Bỏ “Khóa đang hiệu lực” rồi lưu để gỡ; giữ lại căn cứ xử lý.", "Holds", "HOLD", AcademicFields.Holds,
            [new("student", "Sinh viên"), new("scope", "Phạm vi", 90), new("reason", "Lý do", 160), new("active", "Hiệu lực", 85)]);
        var results = GroupTab("Kết quả được công nhận");
        Add(results, "Nhập & sửa kết quả", "Chỉ kết quả có nguồn và thời điểm công bố được công nhận. Khi sửa phải nêu lý do; lịch sử phiên bản được giữ lại.", "Results", "RESULT", AcademicFields.Results,
            [new("student", "Sinh viên"), new("course", "Học phần"), new("attempt", "Lần", 65), new("grade", "Điểm", 65), new("passed", "Đạt", 65), new("source", "Nguồn", 140)]);
        history = new(); var historyTab = new TabPage("Lịch sử sửa kết quả") { BackColor = AppTheme.Canvas }; historyTab.Controls.Add(history); results.TabPages.Add(historyTab);
        accounts = new(connectionString, dataChanged); var accountTab = new TabPage("Tài khoản") { BackColor = AppTheme.Canvas };
        accountTab.Controls.Add(accounts); tabs.TabPages.Add(accountTab);
        reload.Click += async (_, _) =>
        {
            if (!CanLeave()) return;
            await RunAsync(ReloadAsync);
        };
        Controls.Add(root);
    }

    private Font OwnFont(float size = 9, bool bold = false) { var f = AppTheme.Font(size, bold); fonts.Add(f); return f; }
    private TabControl GroupTab(string title)
    {
        var page = new TabPage(title) { BackColor = AppTheme.Canvas, Padding = new(0, S(8), 0, 0) };
        var children = new TabControl { Dock = DockStyle.Fill, Font = Font, AccessibleName = title }; page.Controls.Add(children); tabs.TabPages.Add(page); return children;
    }
    private void Add(TabControl parent, string title, string description, string table, string kind, AcademicField[] fields, AcademicColumn[] columns, bool publish = false)
    {
        var section = new AcademicSection(title, description, table, kind, "id", columns, fields, SaveAsync,
            publish ? PublishAsync : null);
        sections.Add(section); var tab = new TabPage(title) { BackColor = AppTheme.Canvas }; tab.Controls.Add(section); parent.TabPages.Add(tab);
    }
    private async Task SaveAsync(AcademicSection section)
    {
        var values = section.ReadValues(); string id = AcademicValidation.Code(section.RecordId);
        await repository.SaveAsync(new AcademicChange(section.Kind, id, section.Version.Length == 0 ? null : section.Version, JsonSerializer.Serialize(values)));
        dataChanged(); await RefreshAfterSaveAsync(section, id);
    }
    private async Task PublishAsync(AcademicSection section)
    {
        if (section.Kind == "PROGRAM") await repository.PublishProgramAsync(section.SelectedId, section.Version);
        else await repository.PublishPolicyAsync(section.SelectedId, section.Version);
        dataChanged(); await RefreshAfterSaveAsync(section, section.SelectedId);
    }
    private async Task RefreshAfterSaveAsync(AcademicSection saved, string id)
    {
        var fresh = await repository.SnapshotAsync(); if (IsDisposed) { fresh.Dispose(); return; }
        var old = snapshot; snapshot = fresh;
        foreach (var section in sections)
        {
            section.BindReferences(snapshot);
            if (snapshot.Tables.Contains(section.TableName)) section.BindTable(snapshot.Tables[section.TableName]!, ReferenceEquals(section, saved) ? id :
                section.HasUnsavedChanges || section.SelectedId.Length == 0 ? null : section.SelectedId);
        }
        old.Dispose(); saved.ShowNotice("Đã lưu. Dữ liệu mới được kiểm tra lại khi đăng ký học.");
        history.Bind(snapshot);
    }
    public async Task ReloadAsync()
    {
        var fresh = await repository.SnapshotAsync(); if (IsDisposed) { fresh.Dispose(); return; }
        var old = snapshot; snapshot = fresh;
        foreach (var section in sections)
        {
            section.BindReferences(snapshot);
            if (snapshot.Tables.Contains(section.TableName)) section.BindTable(snapshot.Tables[section.TableName]!, section.HasUnsavedChanges || section.SelectedId.Length == 0 ? null : section.SelectedId);
        }
        old.Dispose(); history.Bind(snapshot); await accounts.ReloadAsync();
    }
    public bool CanLeave()
    {
        if (IsBusy) return false;
        var dirty = sections.Where(s => s.HasUnsavedChanges).ToArray();
        if (dirty.Length > 0 && MessageBox.Show(this, "Có thay đổi học vụ chưa lưu. Bỏ các thay đổi này?", "Campus",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
        if (!accounts.CanLeave()) return false;
        foreach (var section in dirty) section.New(); return true;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy || IsDisposed) return; busy = true; UseWaitCursor = true;
        try { await action(); }
        catch (Exception error) when (error is PersistenceException or ArgumentException or InvalidOperationException)
        { if (!IsDisposed) { notice.Text = error.Message + " Bản nháp được giữ nguyên."; notice.ForeColor = AppTheme.Danger; } }
        finally { busy = false; if (!IsDisposed) UseWaitCursor = false; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) snapshot.Dispose(); base.Dispose(disposing); if (disposing) foreach (var font in fonts) font.Dispose();
    }
}
