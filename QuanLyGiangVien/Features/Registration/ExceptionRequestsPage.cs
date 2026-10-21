using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class ExceptionRequestsPage : ModulePage
{
    private readonly ExceptionWorkflowRepository repository;
    private readonly DataGridView grid = Ui.Grid();
    private readonly ComboBox kind = PhaseThreeUi.Choices(new("MAX_CREDITS", "Xin vượt tín chỉ"), new("REQUIREMENT", "Xin miễn điều kiện cụ thể"),
        new("LATE_REGISTER", "Xin đăng ký muộn"), new("WITHDRAW", "Xin rút môn sau hạn"));
    private readonly TextBox course = PhaseThreeUi.Text("txtExceptionCourse"), classId = PhaseThreeUi.Text("txtExceptionClass"),
        rule = PhaseThreeUi.Text("txtExceptionRule", 30), reason = PhaseThreeUi.Text("txtExceptionReason", 500);
    private readonly NumericUpDown uses = new() { Minimum = 1, Maximum = 100, Value = 1, Width = AppTheme.S(65) },
        max = new() { Minimum = 1, Maximum = 1000, Value = 30, Width = AppTheme.S(75) };
    private readonly DateTimePicker expires = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", Width = AppTheme.S(160), Value = DateTime.UtcNow.AddHours(7).AddDays(7) };
    private readonly Label notice = new() { AutoSize = false, Height = AppTheme.S(62), Dock = DockStyle.Top, BackColor = AppTheme.Lilac, Padding = new(AppTheme.S(10)) };
    private readonly FlowLayoutPanel editor = Ui.Toolbar();
    private readonly Button approve = Ui.Button("Duyệt", true), reject = Ui.Button("Từ chối"), revoke = Ui.Button("Thu hồi");
    private DataTable? data;
    private sealed record DraftValues(int Kind, string Course, string Class, string Rule, string Reason, decimal Uses, decimal Max, long Expiry);
    private DraftValues? baseline;
    private DraftValues ReadDraft() => new(kind.SelectedIndex, course.Text, classId.Text, rule.Text, reason.Text, uses.Value, max.Value, expires.Value.Ticks);
    public override bool HasUnsavedChanges => baseline is not null && ReadDraft() != baseline;
    public ExceptionRequestsPage(RegistrationWorkspace host) : base(host)
    {
        Name = "ExceptionRequestsPage"; repository = new(host.ConnectionString); grid.Name = "dgvExceptions";
        var buttons = Ui.Toolbar(); buttons.Dock = DockStyle.Bottom; var reload = Ui.Button("Tải lại đơn");
        buttons.Controls.Add(reload); reload.Click += async (_, _) => await Host.ExecuteAsync(RefreshDataAsync);
        editor.Dock = DockStyle.Fill; editor.AutoSize = false; editor.AutoScroll = true;
        if (Service.User.LaSinhVien)
        {
            PhaseThreeUi.Field(editor, "Loại đơn", kind); PhaseThreeUi.Field(editor, "Mã HP (nếu cần)", course);
            PhaseThreeUi.Field(editor, "Mã lớp (nếu cần)", classId); PhaseThreeUi.Field(editor, "Nhóm điều kiện (GROUP_1…)", rule);
            var submit = Ui.Button("Gửi đơn", true); submit.Name = "btnExceptionSubmit"; buttons.Controls.Add(submit);
            submit.Click += async (_, _) => await Host.ExecuteAsync(async () =>
            {
                using var result = await repository.SubmitAsync(new(PhaseThreeUi.Choice(kind), Host.MaHocKy,
                    PhaseThreeUi.Optional(course), PhaseThreeUi.Optional(classId), PhaseThreeUi.Optional(rule), reason.Text));
                reason.Clear(); course.Clear(); classId.Clear(); rule.Clear(); kind.SelectedIndex = 0;
                baseline = ReadDraft();
                await RefreshDataAsync(); notice.Text = "Đã gửi đơn. Quyết định chỉ có hiệu lực sau duyệt, đúng phạm vi, thời hạn và số lượt.";
            });
        }
        else
        {
            PhaseThreeUi.Field(editor, "Hạn duyệt (giờ Việt Nam)", expires); PhaseThreeUi.Field(editor, "Lượt dùng", uses);
            PhaseThreeUi.Field(editor, "TC tối đa (đơn vượt TC)", max); buttons.Controls.AddRange([approve, reject, revoke]);
            approve.Click += async (_, _) => await Host.ExecuteAsync(() => DecideAsync("APPROVE"));
            reject.Click += async (_, _) => await Host.ExecuteAsync(() => DecideAsync("REJECT"));
            revoke.Click += async (_, _) => await Host.ExecuteAsync(() => DecideAsync("REVOKE"));
        }
        PhaseThreeUi.Field(editor, "Lý do gửi / quyết định", reason);
        var views = new TabControl { Dock = DockStyle.Fill };
        var list = new TabPage("Danh sách đơn"); list.Controls.Add(grid);
        var form = new TabPage(Service.User.LaSinhVien ? "Nội dung đơn mới" : "Nội dung quyết định"); form.Controls.Add(editor);
        views.TabPages.AddRange([list, form]); Controls.Add(views); Controls.Add(buttons); Controls.Add(notice);
        grid.SelectionChanged += (_, _) => ApplyDeadline();
        kind.SelectedIndexChanged += (_, _) => SetScopeFields(); SetScopeFields(); baseline = ReadDraft();
    }
    private void SetScopeFields()
    {
        var value = PhaseThreeUi.Choice(kind); course.Enabled = value == "REQUIREMENT";
        classId.Enabled = value != "MAX_CREDITS"; rule.Enabled = value == "REQUIREMENT";
        if (!course.Enabled) course.Clear(); if (!classId.Enabled) classId.Clear(); if (!rule.Enabled) rule.Clear();
    }
    public override async Task RefreshDataAsync()
    {
        var fresh = await repository.ListAsync(Host.MaHocKy); if (IsDisposed) { fresh.Dispose(); return; }
        ClearData(); data = fresh; Ui.Bind(grid, data);
        foreach (var name in new[] { "version", "submitted_by", "reviewed_by", "program_id", "term", "granted_uses" }) if (grid.Columns.Contains(name)) grid.Columns[name]!.Visible = false;
        notice.Text = "Ngoại lệ chỉ áp dụng đúng SV/kỳ/môn/lớp/nhóm điều kiện đã duyệt; không bỏ qua trùng lịch, hold hoặc sĩ số. Rút môn lưu dấu vết riêng; chưa tích hợp tính điểm/phí. Thời điểm trong danh sách dùng UTC.";
        if (!Service.User.LaSinhVien)
        {
            using var permissions = await Host.EventLog.PermissionsAsync();
            editor.Enabled = Service.User.VaiTro == "QUAN_TRI" || permissions.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["permission"]) == "EXCEPTION_REVIEW");
        }
        ApplyDeadline();
    }
    private async Task DecideAsync(string decision)
    {
        if (Ui.Current(grid) is not { } row) return;
        var isApproval = decision == "APPROVE";
        if (MessageBox.Show(this, "Áp dụng quyết định cho đơn " + row["id"] + " của " + row["student"] + "?", "Xác nhận xử lý đơn",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        using var result = await repository.DecideAsync(new(Convert.ToInt64(row["id"]), PhaseThreeUi.Version(row["version"]), decision, reason.Text,
            isApproval ? DateTime.SpecifyKind(expires.Value.AddHours(-7), DateTimeKind.Utc) : null, isApproval ? (int)uses.Value : null,
            isApproval && Convert.ToString(row["kind"]) == "MAX_CREDITS" ? (int)max.Value : null));
        reason.Clear(); baseline = ReadDraft(); Host.NotifyDataChanged(); await RefreshDataAsync();
    }
    public override void ApplyDeadline()
    {
        var status = Convert.ToString(Ui.Current(grid)?["status"]); var enabled = editor.Enabled && Ui.Current(grid) != null;
        approve.Enabled = reject.Enabled = enabled && status == "PENDING"; revoke.Enabled = enabled && status == "APPROVED";
    }
    public override void ClearData() { Ui.Bind(grid, null); data?.Dispose(); data = null; }
}
