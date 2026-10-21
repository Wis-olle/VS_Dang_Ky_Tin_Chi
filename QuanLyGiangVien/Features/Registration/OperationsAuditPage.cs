using System.Data;
using QuanLyGiangVien.Ui;
namespace QuanLyGiangVien.Features.Registration;

internal sealed class OperationsAuditPage : ModulePage
{
    private readonly DataGridView grid = Ui.Grid();
    private readonly TextBox account = PhaseThreeUi.Text("txtPermissionAccount"), reason = PhaseThreeUi.Text("txtPermissionReason", 500);
    private readonly ComboBox permission = PhaseThreeUi.Choices(new("CLASS_OPERATE", "Vận hành lớp"), new("EXCEPTION_REVIEW", "Duyệt ngoại lệ"),
        new("PROFILE_EDIT", "Sửa hồ sơ học vụ"), new("HOLD_MANAGE", "Đặt / gỡ hold"), new("AUDIT_READ", "Đọc nhật ký & outbox"),
        new("SEAT_MANAGE", "Cấu hình đợt / quota"), new("WAITLIST_OPERATE", "Vận hành hàng chờ"), new("ADVISE_PLAN", "Cố vấn kế hoạch"),
        new("IMPORT_DATA", "Nhập dữ liệu nguồn"), new("OPERATIONS_READ", "Đọc sức khỏe / nhu cầu"));
    private readonly CheckBox grant = new() { Text = "Cấp quyền (bỏ chọn để thu hồi)", AutoSize = true, Checked = true };
    private DataTable? data;
    public override bool HasUnsavedChanges => reason.TextLength > 0;
    public OperationsAuditPage(RegistrationWorkspace host) : base(host)
    {
        Name = "OperationsAuditPage"; var top = Ui.Toolbar(); var reload = Ui.Button("Tải nhật ký / outbox"); top.Controls.Add(reload);
        var note = Ui.Label("Nhật ký dùng giờ UTC. Quyền chi tiết chỉ do quản trị cấp; tài khoản mẫu không tự nhận thêm quyền."); top.Controls.Add(note);
        var showPermissions = Ui.Button("Xem tài khoản & quyền"); showPermissions.Visible = Service.User.VaiTro == "QUAN_TRI"; top.Controls.Add(showPermissions);
        showPermissions.Click += async (_, _) => await Host.ExecuteAsync(async () => {
            var fresh = await Host.EventLog.PermissionAssignmentsAsync(); if (IsDisposed) { fresh.Dispose(); return; }
            ClearData(); data = fresh; Ui.Bind(grid, data);
        });
        var editor = Ui.Toolbar(); editor.Dock = DockStyle.Bottom; editor.AutoSize = false; editor.Height = AppTheme.S(145); editor.AutoScroll = true;
        PhaseThreeUi.Field(editor, "Mã tài khoản PĐT", account); PhaseThreeUi.Field(editor, "Quyền", permission);
        PhaseThreeUi.Field(editor, "Hiệu lực", grant); PhaseThreeUi.Field(editor, "Lý do", reason); var save = Ui.Button("Lưu quyền", true); editor.Controls.Add(save);
        editor.Visible = Service.User.VaiTro == "QUAN_TRI"; Controls.Add(grid); Controls.Add(editor); Controls.Add(top);
        reload.Click += async (_, _) => await Host.ExecuteAsync(RefreshDataAsync);
        save.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            if (!int.TryParse(account.Text, out var id) || id <= 0) throw new ArgumentException("Nhập mã tài khoản số hợp lệ.");
            using var result = await Host.EventLog.SetPermissionAsync(id, PhaseThreeUi.Choice(permission), grant.Checked, reason.Text);
            reason.Clear(); await RefreshDataAsync();
        });
    }
    public override async Task RefreshDataAsync()
    {
        var fresh = await Host.EventLog.OperationsAsync(); if (IsDisposed) { fresh.Dispose(); return; }
        ClearData(); data = fresh; Ui.Bind(grid, data);
    }
    public override void ClearData() { Ui.Bind(grid, null); data?.Dispose(); data = null; }
}
