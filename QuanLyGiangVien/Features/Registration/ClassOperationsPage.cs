using System.Data;
using System.Text.Json;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class ClassOperationsPage : ModulePage
{
    private readonly LifecycleRepository repository;
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView classes = Ui.Grid(), terms = Ui.Grid(), calendar = Ui.Grid(), combinations = Ui.Grid(), history = Ui.Grid();
    private readonly ComboBox target = PhaseThreeUi.Choices(new("REGISTRATION", "Mở đăng ký"), new("CLOSED", "Đóng đăng ký"),
        new("TEACHING", "Đang học"), new("COMPLETED", "Hoàn tất"), new("CANCELED", "Hủy"), new("ARCHIVED", "Lưu trữ"));
    private readonly ComboBox scheduleKind = PhaseThreeUi.Choices(new("REPLACE", "Thay buổi học"), new("CANCEL", "Nghỉ buổi học"), new("MAKEUP", "Thêm buổi bù"));
    private readonly ComboBox scheduleClass = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = AppTheme.S(150) };
    private readonly TextBox stateReason = PhaseThreeUi.Text("txtLifecycleReason", 500), scheduleReason = PhaseThreeUi.Text("txtScheduleReason", 500),
        holidayReason = PhaseThreeUi.Text("txtHolidayReason", 500), room = PhaseThreeUi.Text("txtReplacementRoom"), lecturer = PhaseThreeUi.Text("txtReplacementLecturer"),
        combinationId = PhaseThreeUi.Text("txtCombinationId"), course = PhaseThreeUi.Text("txtCombinationCourse"), memberCodes = PhaseThreeUi.Text("txtCombinationMembers", 500);
    private readonly NumericUpDown start = new() { Minimum = 1, Maximum = 12, Value = 1, Width = AppTheme.S(60) },
        end = new() { Minimum = 1, Maximum = 12, Value = 3, Width = AppTheme.S(60) };
    private readonly DateTimePicker source = DayPicker(), destination = DayPicker(), holiday = DayPicker();
    private readonly CheckBox holidayActive = new() { Text = "Ngày nghỉ có hiệu lực", Checked = true, AutoSize = true };
    private readonly Label notice = new() { Name = "lblOperationsNotice", Text = "Lịch sử được giữ nguyên; mọi thay đổi kiểm lại phiên bản, quyền và lịch của toàn bộ sinh viên bị ảnh hưởng.", Dock = DockStyle.Top,
        BackColor = AppTheme.Lilac, Height = AppTheme.S(34), Padding = new(AppTheme.S(6)) };
    private readonly List<DataTable> tables = [];
    private readonly List<Control> writePanels = [];
    private bool loading;
    private long? sourceMeetingId;
    private bool combinationDirty, changingCombination, targetIsTerm;
    public override bool HasUnsavedChanges => stateReason.TextLength > 0 || scheduleReason.TextLength > 0 || holidayReason.TextLength > 0 || combinationDirty;
    public ClassOperationsPage(RegistrationWorkspace host) : base(host)
    {
        Name = "ClassOperationsPage"; repository = new(host.ConnectionString);
        classes.Name = "dgvLifecycleClasses"; calendar.Name = "dgvActualCalendar"; terms.Name = "dgvLifecycleTerms";
        var stateTab = AddTab("Vòng đời lớp / kỳ");
        var states = new TabControl { Dock = DockStyle.Fill }; var classTab = new TabPage("Lớp học phần"); classTab.Controls.Add(classes);
        var termTab = new TabPage("Học kỳ"); termTab.Controls.Add(terms); states.TabPages.AddRange([classTab, termTab]);
        var stateBar = Editor(112); PhaseThreeUi.Field(stateBar, "Chuyển sang", target); PhaseThreeUi.Field(stateBar, "Lý do", stateReason);
        var transition = Ui.Button("Chuyển trạng thái", true); stateBar.Controls.Add(transition);
        var stateEditor = new TabPage("Chuyển lớp / kỳ đã chọn"); stateBar.Dock = DockStyle.Fill; stateEditor.Controls.Add(stateBar); states.TabPages.Add(stateEditor);
        states.SelectedIndexChanged += (_, _) => { if (states.SelectedTab == classTab) targetIsTerm = false; if (states.SelectedTab == termTab) targetIsTerm = true; };
        stateTab.Controls.Add(states);
        transition.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            var isTerm = targetIsTerm; var row = Ui.Current(isTerm ? terms : classes);
            if (row == null) return; var id = Convert.ToString(row[isTerm ? "ma_hk" : "ma_lhp"])!;
            if (!Confirm("Chuyển " + id + " sang " + target.Text + "?")) return;
            if (isTerm) await repository.TransitionSemesterAsync(id, PhaseThreeUi.Choice(target), PhaseThreeUi.Version(row["phien_ban"]), stateReason.Text);
            else await repository.TransitionClassAsync(id, PhaseThreeUi.Choice(target), PhaseThreeUi.Version(row["phien_ban"]), stateReason.Text);
            stateReason.Clear(); Host.NotifyDataChanged(); await RefreshDataAsync(); notice.Text = "Đã chuyển trạng thái và lưu lịch sử. Bấm Làm mới ở lớp/phòng và TKB để đối soát.";
        });
        var scheduleTab = AddTab("Lịch ngoại lệ"); var scheduleBar = Editor(210);
        var classBar = Ui.Toolbar(); classBar.AutoSize = false; classBar.Height = AppTheme.S(48); PhaseThreeUi.Field(classBar, "Lớp", scheduleClass);
        PhaseThreeUi.Field(scheduleBar, "Thao tác", scheduleKind);
        PhaseThreeUi.Field(scheduleBar, "Ngày gốc", source); PhaseThreeUi.Field(scheduleBar, "Ngày mới", destination);
        PhaseThreeUi.Field(scheduleBar, "Phòng mới", room); PhaseThreeUi.Field(scheduleBar, "GV mới", lecturer);
        PhaseThreeUi.Field(scheduleBar, "Từ tiết", start); PhaseThreeUi.Field(scheduleBar, "Đến tiết", end); PhaseThreeUi.Field(scheduleBar, "Lý do", scheduleReason);
        Button preview = Ui.Button("Xem ảnh hưởng"), apply = Ui.Button("Lưu thay đổi lịch", true), loadCalendar = Ui.Button("Xem ngày học thực tế");
        classBar.Controls.Add(loadCalendar); scheduleBar.Controls.AddRange([preview, apply]);
        var scheduleViews = new TabControl { Dock = DockStyle.Fill }; var actualTab = new TabPage("Ngày học / ảnh hưởng"); actualTab.Controls.Add(calendar);
        var changeTab = new TabPage("Thay đổi buổi đã chọn"); scheduleBar.Dock = DockStyle.Fill; changeTab.Controls.Add(scheduleBar); scheduleViews.TabPages.AddRange([actualTab, changeTab]);
        scheduleTab.Controls.Add(scheduleViews); scheduleTab.Controls.Add(classBar);
        loadCalendar.Click += async (_, _) => await Host.ExecuteAsync(LoadCalendarAsync);
        scheduleClass.SelectedIndexChanged += async (_, _) => { sourceMeetingId = null; if (!loading) await Host.ExecuteAsync(LoadCalendarAsync); };
        source.ValueChanged += (_, _) => sourceMeetingId = null;
        calendar.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0 || Ui.Current(calendar) is not { } r || !r.Table.Columns.Contains("ngay_hoc")) return;
            source.Value = Convert.ToDateTime(r.Table.Columns.Contains("source_date") ? r["source_date"] : r["ngay_hoc"]);
            sourceMeetingId = r.Table.Columns.Contains("source_meeting_id") ? Convert.ToInt64(r["source_meeting_id"]) : null;
            destination.Value = Convert.ToDateTime(r["ngay_hoc"]);
            room.Text = Convert.ToString(r["ma_phong"]); lecturer.Text = Convert.ToString(r["ma_gv"]);
            start.Value = Convert.ToInt32(r["tiet_bd"]); end.Value = Convert.ToInt32(r["tiet_kt"]);
        };
        preview.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            var conflicts = await repository.PreviewScheduleAsync(Change()); Bind(calendar, conflicts);
            notice.Text = conflicts.Rows.Count == 0 ? "Không có xung đột trong lần kiểm tra này. Khi lưu, máy chủ kiểm lại toàn bộ lịch và phiên bản." : "Chưa thể lưu: danh sách xung đột của phòng, giảng viên hoặc sinh viên ở bên dưới.";
        });
        apply.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            if (!Confirm("Lưu lịch ngoại lệ của " + scheduleClass.Text + "?")) return;
            await repository.ChangeScheduleAsync(Change()); scheduleReason.Clear(); Host.NotifyDataChanged(); await RefreshDataAsync(); await LoadCalendarAsync();
            notice.Text = "Đã lưu lịch ngoại lệ; lịch tuần gốc và các phiên bản trước được giữ lại.";
        });
        var holidayTab = AddTab("Ngày nghỉ"); var holidayBar = Editor(160); holidayBar.Dock = DockStyle.Fill;
        var guidance = Ui.Label("Ngày nghỉ áp dụng toàn học kỳ đang chọn. Bỏ ngày nghỉ sẽ kiểm lại mọi lịch trước khi khôi phục.");
        guidance.Dock = DockStyle.Top; holidayTab.Controls.Add(guidance);
        PhaseThreeUi.Field(holidayBar, "Ngày", holiday); PhaseThreeUi.Field(holidayBar, "Hiệu lực", holidayActive); PhaseThreeUi.Field(holidayBar, "Lý do", holidayReason);
        var saveHoliday = Ui.Button("Lưu ngày nghỉ", true); holidayBar.Controls.Add(saveHoliday); holidayTab.Controls.Add(holidayBar);
        saveHoliday.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            var term = tables.SelectMany(t => t.Rows.Cast<DataRow>()).FirstOrDefault(r => r.Table.Columns.Contains("lifecycle") && r.Table.Columns.Contains("ma_hk") && !r.Table.Columns.Contains("ma_lhp") && Convert.ToString(r["ma_hk"]) == Host.MaHocKy);
            if (term == null) throw new InvalidOperationException("Chưa tải được phiên bản học kỳ. Hãy tải lại.");
            if (!Confirm("Cập nhật ngày nghỉ của học kỳ " + Host.MaHocKy + "?")) return;
            await repository.SetHolidayAsync(Host.MaHocKy, DateOnly.FromDateTime(holiday.Value), holidayActive.Checked, PhaseThreeUi.Version(term["phien_ban"]), holidayReason.Text);
            holidayReason.Clear(); Host.NotifyDataChanged(); await RefreshDataAsync(); notice.Text = "Đã lưu ngày nghỉ; tải lại lịch thực tế để đối soát.";
        });
        var combinationTab = AddTab("Tổ hợp LT–TH"); var combinationBar = Editor(150);
        PhaseThreeUi.Field(combinationBar, "Mã tổ hợp", combinationId); PhaseThreeUi.Field(combinationBar, "Mã học phần", course);
        PhaseThreeUi.Field(combinationBar, "Lớp:loại (VD LHP01:LT,LHP02:TH)", memberCodes); var saveCombination = Ui.Button("Lưu tổ hợp", true); combinationBar.Controls.Add(saveCombination);
        var combinationViews = new TabControl { Dock = DockStyle.Fill }; var comboList = new TabPage("Tổ hợp hiện có"); comboList.Controls.Add(combinations);
        var comboForm = new TabPage("Khai báo / sửa tổ hợp"); combinationBar.Dock = DockStyle.Fill; comboForm.Controls.Add(combinationBar); combinationViews.TabPages.AddRange([comboList, comboForm]); combinationTab.Controls.Add(combinationViews);
        saveCombination.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            var row = combinations.Rows.Cast<DataGridViewRow>().Select(r => (r.DataBoundItem as DataRowView)?.Row).FirstOrDefault(r => r != null && Convert.ToString(r["combination_id"]) == combinationId.Text.Trim());
            var entries = memberCodes.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Split(':', StringSplitOptions.TrimEntries)).ToArray();
            if (entries.Any(v => v.Length != 2) || entries.Select(v => v[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
                throw new ArgumentException("Nhập từng lớp và loại LT/TH/TL, ví dụ LHP01:LT,LHP02:TH.");
            var roles = entries.ToDictionary(v => v[0].ToUpperInvariant(), v => v[1].ToUpperInvariant());
            using var result = await Host.Workflow.SaveCombinationAsync(combinationId.Text.Trim(), Host.MaHocKy, course.Text.Trim(),
                roles.Keys, row is null ? null : (byte[])row["version"], roles);
            combinationDirty = false; await RefreshDataAsync(); notice.Text = "Đã lưu tổ hợp bắt buộc. Tín chỉ học phần chỉ tính một lần; không đổi cấu trúc khi đã có đăng ký.";
        });
        combinations.SelectionChanged += (_, _) =>
        {
            if (loading || combinationDirty || Ui.Current(combinations) is not { } row) return;
            changingCombination = true;
            combinationId.Text = Convert.ToString(row["combination_id"]); course.Text = Convert.ToString(row["course"]);
            var roles = row.Table.Columns.Contains("roles_json") && !row.IsNull("roles_json")
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(Convert.ToString(row["roles_json"])!) : null;
            memberCodes.Text = string.Join(",", JsonSerializer.Deserialize<string[]>(Convert.ToString(row["class_json"])!)!.Select(id => id + ":" + roles?.GetValueOrDefault(id)));
            changingCombination = false;
        };
        foreach (var input in new[] { combinationId, course, memberCodes }) input.TextChanged += (_, _) => { if (!loading && !changingCombination) combinationDirty = true; };
        var historyTab = AddTab("Lịch sử & ngày nghỉ"); history.Name = "dgvLifecycleHistory"; historyTab.Controls.Add(history);
        Controls.Add(tabs); Controls.Add(notice);
    }
    private static DateTimePicker DayPicker() => new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = AppTheme.S(120), Value = DateTime.UtcNow.AddHours(7).Date.AddDays(1) };
    private TabPage AddTab(string title) { var page = new TabPage(title); tabs.TabPages.Add(page); return page; }
    private FlowLayoutPanel Editor(int height)
    {
        var bar = Ui.Toolbar(); bar.Dock = DockStyle.Bottom; bar.AutoSize = false; bar.Height = AppTheme.S(height); bar.AutoScroll = true; writePanels.Add(bar); return bar;
    }
    private bool Confirm(string text) => MessageBox.Show(this, text, "Xác nhận vận hành", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private LifecycleScheduleChange Change()
    {
        var row = (scheduleClass.SelectedItem as DataRowView)?.Row ?? throw new InvalidOperationException("Chọn lớp cần đổi lịch.");
        var kind = PhaseThreeUi.Choice(scheduleKind);
        return new(Convert.ToString(row["ma_lhp"])!, kind, kind == "MAKEUP" ? null : DateOnly.FromDateTime(source.Value),
            kind == "CANCEL" ? null : DateOnly.FromDateTime(destination.Value), kind == "CANCEL" ? null : room.Text.Trim(),
            kind == "CANCEL" ? null : lecturer.Text.Trim(), kind == "CANCEL" ? null : (int)start.Value, kind == "CANCEL" ? null : (int)end.Value,
            PhaseThreeUi.Version(row["phien_ban"]), scheduleReason.Text, SourceMeetingId: kind == "MAKEUP" ? null : sourceMeetingId);
    }
    private async Task LoadCalendarAsync()
    {
        if (scheduleClass.SelectedItem is not DataRowView row) return;
        sourceMeetingId = null;
        var fresh = await repository.CalendarAsync(Convert.ToString(row["ma_lhp"])!); if (IsDisposed) { fresh.Dispose(); return; } Bind(calendar, fresh);
        if (calendar.Columns.Contains("source_meeting_id")) calendar.Columns["source_meeting_id"]!.Visible = false;
        if (calendar.Columns.Contains("ma_lhp")) calendar.Columns["ma_lhp"]!.Visible = false;
    }
    private void Bind(DataGridView grid, DataTable fresh)
    {
        if (grid.DataSource is DataTable old) { Ui.Bind(grid, null); tables.Remove(old); old.Dispose(); }
        tables.Add(fresh); Ui.Bind(grid, fresh);
        foreach (var name in new[] { "phien_ban", "version" }) if (grid.Columns.Contains(name)) grid.Columns[name]!.Visible = false;
    }
    public override async Task RefreshDataAsync()
    {
        loading = true;
        try
        {
            var freshClasses = await repository.ClassesAsync(Host.MaHocKy); var freshTerms = await repository.TermsAsync();
            var components = await Host.Workflow.ComponentsAsync(Host.MaHocKy);
            var pastChanges = await repository.HistoryAsync(Host.MaHocKy);
            if (IsDisposed) { freshClasses.Dispose(); freshTerms.Dispose(); components.Dispose(); pastChanges.Dispose(); return; }
            scheduleClass.DataSource = null; ClearData(); Bind(classes, freshClasses); Bind(terms, freshTerms); Bind(combinations, components); Bind(history, pastChanges);
            scheduleClass.DisplayMember = "ma_lhp"; scheduleClass.DataSource = freshClasses;
            using var permissions = await Host.EventLog.PermissionsAsync(); var canWrite = Service.User.VaiTro == "QUAN_TRI" || permissions.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["permission"]) == "CLASS_OPERATE");
            foreach (var panel in writePanels) panel.Enabled = canWrite;
            notice.Text = canWrite ? "Có quyền vận hành. Chuyển trạng thái theo vòng đời; lịch ngoại lệ luôn kiểm ảnh hưởng trước khi ghi." : "Tài khoản chưa được cấp quyền vận hành lớp. Quản trị có thể cấp CLASS_OPERATE trong Nhật ký & quyền.";
        }
        finally { loading = false; }
    }
    public override void ClearData()
    {
        Ui.Bind(classes, null); Ui.Bind(terms, null); Ui.Bind(calendar, null); Ui.Bind(combinations, null); Ui.Bind(history, null);
        foreach (var table in tables) table.Dispose(); tables.Clear();
    }
}
