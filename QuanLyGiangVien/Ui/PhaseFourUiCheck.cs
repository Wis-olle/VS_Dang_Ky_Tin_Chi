using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

internal static class PhaseFourUiCheck
{
    internal static void Attach(CampusForm form, string connection)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_PHASE4_UI_CHECK"); if (string.IsNullOrWhiteSpace(output)) return;
        if (!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal)) throw new InvalidOperationException("Native phase four requires a disposable fixture.");
        form.StartPosition = FormStartPosition.Manual; form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => await Run(form, connection, output);
    }
    private static async Task Run(CampusForm form, string cs, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Expect(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks.Add(text); }
        async Task Sql(string text) { await using var c = new SqlConnection(cs); await c.OpenAsync(); using var q = new SqlCommand(text, c); await q.ExecuteNonQueryAsync(); }
        async Task<int> Count(string text) { await using var c = new SqlConnection(cs); await c.OpenAsync(); using var q = new SqlCommand(text, c); return Convert.ToInt32(await q.ExecuteScalarAsync()); }
        async Task Login(bool student) {
            form.SelectLoginPortal(student ? LoginPortal.Student : LoginPortal.Training);
            Find<TextBox>(form, "txtLoginUsername").Text = student ? "sv2024001" : "daotao";
            Find<TextBox>(form, "txtLoginPassword").Text = Environment.GetEnvironmentVariable(student ? "DEMO_STUDENT_PASSWORD" : "DEMO_TRAINING_PASSWORD")!;
            await form.AuthenticateAsync(); Expect(form.ConnectedIdentity?.Role == (student ? "SINH_VIEN" : "DAO_TAO"), "Password authentication opens the intended portal.");
        }
        async Task Select(RegistrationWorkspace workspace, string label) {
            var tabs = Descendants(workspace).OfType<TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Kết quả đăng ký"));
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(p => p.Text == label); await Wait(() => !workspace.IsBusy); await Task.Delay(100);
        }
        async Task Shot(string name, int width = 1100, int height = 720) {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(height)); await Task.Delay(180); using var bmp = UiCapture.Client(form); bmp.Save(Path.Combine(output, name + ".png"));
        }
        try {
            await Sql("UPDATE dbo.HOC_KY SET trang_thai='DANG_MO',lifecycle='REGISTRATION',tg_bat_dau=DATEADD(day,-1,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())) WHERE ma_hk='HK1-2627'; DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_sv='SV2024001'; DELETE dbo.SEAT_WAITLIST_ITEM WHERE entry_id IN(SELECT id FROM dbo.SEAT_WAITLIST WHERE student='SV2024001'); DELETE dbo.SEAT_WAITLIST WHERE student='SV2024001'; DELETE dbo.OP4_WORKER_RUN; DELETE dbo.AP4_COMMENT WHERE student='SV2024001'; DELETE dbo.AP4_PLAN WHERE student='SV2024001'; DELETE dbo.AP4_ADVISOR WHERE student='SV2024001'; DELETE dbo.AP4_SUBSTITUTION WHERE program_id='N4_CT'; DELETE dbo.HV_HISTORY WHERE id LIKE 'N4[_]%'; DELETE dbo.HV_DOCUMENT WHERE id LIKE 'N4[_]%' OR kind='ASSIGNMENT' AND JSON_VALUE(payload,'$.student')='SV2024001'; DELETE dbo.HV_HISTORY WHERE id='N4IMP'; DELETE dbo.HV_DOCUMENT WHERE id='N4IMP'; DELETE dbo.DANG_KY WHERE ma_sv='SV2024001'; UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY'); DELETE dbo.CAMPUS_PERMISSION WHERE account_id IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao');");
            await Login(true); var workspace = (RegistrationWorkspace)form.CurrentModulePage!; await workspace.ReloadForSemesterAsync("HK1-2627");
            if (workspace.PendingStore().Load() is { } pending) workspace.PendingStore().Clear(pending.RequestId);
            Expect(Descendants(workspace).OfType<TabPage>().Any(t => t.Text == "Hàng chờ nhận chỗ"), "Student has a waitlist page.");
            Expect(Descendants(workspace).OfType<TabPage>().Any(t => t.Text == "CTĐT & kế hoạch học"), "Student has a curriculum and planning page.");
            await Select(workspace, "Gợi ý thời khóa biểu"); var planner = Descendants(workspace).OfType<SchedulePlannerPage>().Single();
            Find<TextBox>(planner, "txtPlannerCourses").Text = "???"; Find<Button>(planner, "btnPlannerGenerate").PerformClick();
            await Wait(() => !Find<Button>(planner, "btnPlannerCancel").Enabled);
            Expect(!form.IsDisposed && Find<DataGridView>(planner, "dgvPlannerOptions").Rows.Count == 0, "Invalid course input is rejected without crashing the native app.");
            Find<TextBox>(planner, "txtPlannerCourses").Text = "IT102";
            await Shot("01-planner-input-wide", 1440, 960); await Shot("02-planner-input-compact");
            var offerings = Find<DataGridView>(planner, "dgvPlannerOfferings");
            Expect(offerings.Rows.Count > 0 && offerings.Height >= offerings.ColumnHeadersHeight + offerings.Rows[0].Height, "Compact planner input fits a complete course choice row.");
            Expect(Find<ComboBox>(workspace, "cboRegistrationModule").Text == "Gợi ý thời khóa biểu", "The compact module picker retains the actual selected page text.");
            Find<Button>(planner, "btnPlannerGenerate").PerformClick(); await Wait(() => !Find<Button>(planner, "btnPlannerCancel").Enabled);
            var options = Find<DataGridView>(planner, "dgvPlannerOptions"); Expect(options.Rows.Count > 0, "The real Generate button produces schedule options from SQL actual meetings.");
            Expect(Find<Button>(planner, "btnPlannerToCart").Enabled, "A feasible generated option can be transferred to the cart.");
            await Shot("03-planner-results-compact");
            Expect(options.Height >= options.ColumnHeadersHeight + options.Rows[0].Height, "Compact planner results fit their header and at least one complete selectable option.");
            Find<Button>(planner, "btnPlannerToCart").PerformClick(); await Wait(() => !workspace.IsBusy);
            using (var cart = await workspace.Workflow.CartAsync("HK1-2627")) Expect(Convert.ToString(cart.Rows[0]["class_json"]) != "[]", "The real transfer button saves the selected plan to the versioned SQL cart.");
            Expect(await Count("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024001' AND trang_thai='DANG_KY';") == 0, "Generating and transferring a schedule does not enroll or reserve a seat.");
            await Select(workspace, "CTĐT & kế hoạch học");
            Expect(Find<Label>(workspace, "lblAcademicPlanNotice").Text.Contains("Chưa có CTĐT"), "Missing official curriculum is reported instead of fabricating progress.");
            await Shot("04-curriculum-missing-compact");
            await Select(workspace, "Hàng chờ nhận chỗ"); var seats = Descendants(workspace).OfType<SeatsAndWaitlistPage>().Single();
            Find<Button>(seats, "btnSeatJoin").PerformClick(); await Wait(() => !workspace.IsBusy);
            var queue = Find<DataGridView>(seats, "dgvSeatWaitlist"); Expect(queue.Rows.Count == 1, "The real Join button puts the complete cart selection on the student's waitlist.");
            Expect(Convert.ToString(queue.Rows[0].Cells["status"].Value) == "Waiting", "Joining the waitlist leaves the request Waiting without holding a seat.");
            await Shot("05-student-waitlist-compact"); Expect(queue.Height >= queue.ColumnHeadersHeight + queue.Rows[0].Height, "Compact waitlist fits its header and a complete request row.");
            await form.LogoutAsync(); await Login(false); await form.OpenModuleAsync(QuanLyGiangVien.Domain.Shell.CampusModule.Reports);
            workspace = (RegistrationWorkspace)form.CurrentModulePage!; await workspace.ReloadForSemesterAsync("HK1-2627");
            using (var grants = await workspace.EventLog.PermissionsAsync()) Expect(grants.Rows.Count == 0, "Sample training account receives no new operational permissions implicitly.");
            await Sql("INSERT dbo.CAMPUS_PERMISSION SELECT t.ma_tk,p.permission FROM dbo.TAI_KHOAN t CROSS JOIN (VALUES('IMPORT_DATA'),('OPERATIONS_READ'),('WAITLIST_OPERATE'),('SEAT_MANAGE'))p(permission) WHERE t.ten_dang_nhap='daotao';");
            await Select(workspace, "Nhu cầu & sức khỏe"); var operations = Descendants(workspace).OfType<OperationsDashboardPage>().Single();
            Expect(Descendants(operations).OfType<DataGridView>().Single().Rows.Count > 0, "An explicitly authorized account sees live capacity and demand data.");
            await Shot("06-demand-wide", 1440, 960); await Shot("07-demand-compact");
            Find<Button>(operations, "btnOperationsRun").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(await Count("SELECT COUNT(*) FROM dbo.OP4_WORKER_RUN WHERE status='SUCCEEDED';") > 0, "The real Run-once button records a successful server-timed worker run.");
            Expect(await Count("SELECT COUNT(*) FROM dbo.SEAT_WAITLIST WHERE student='SV2024001' AND status='Offered';") == 1, "The authenticated worker offers the complete waitlist selection once.");
            await Select(workspace, "Nhập dữ liệu nguồn"); var import = Descendants(workspace).OfType<DataImportPage>().Single();
            var text = JsonSerializer.Serialize(new { batchId = Guid.NewGuid(), source = "Native fixture source only", rows = new[] { new { kind = "PROGRAM", id = "N4IMP", payload = new { name = "Native test draft", major = "CNTT", cohort = 2024, requiredCredits = 120 } } } });
            import.LoadDocument(text); Find<Button>(import, "btnImportPreview").PerformClick(); await Wait(() => !workspace.IsBusy);
            var importGrid = Descendants(import).OfType<DataGridView>().Single(); Expect(importGrid.Rows.Count == 1 && Convert.ToBoolean(importGrid.Rows[0].Cells["valid"].Value), "The real import preview reports validation per row.");
            Expect(await Count("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE id='N4IMP';") == 0, "Native dry-run leaves no academic document behind.");
            Expect(!Find<Button>(import, "btnImportCommit").Enabled, "Import cannot commit before source confirmation.");
            Descendants(import).OfType<CheckBox>().Single().Checked = true; Find<Button>(import, "btnImportCommit").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(await Count("SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='N4IMP' AND published=0;") == 1, "Confirmed import commits one draft program through existing academic validation.");
            Expect(!import.HasUnsavedChanges, "Successful batch commit clears the pending import draft."); await Shot("08-import-compact");
            await Select(workspace, "Đợt, quota & hàng chờ"); await Shot("09-seat-management-compact");
            await form.LogoutAsync(); await Login(true); workspace = (RegistrationWorkspace)form.CurrentModulePage!;
            await workspace.ReloadForSemesterAsync("HK1-2627"); await Select(workspace, "Hàng chờ nhận chỗ"); seats = Descendants(workspace).OfType<SeatsAndWaitlistPage>().Single();
            Expect(Find<Button>(seats, "btnSeatClaim").Enabled, "The student's own unexpired offer enables the real Claim button after login.");
            Find<Button>(seats, "btnSeatClaim").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(await Count("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024001' AND trang_thai='DANG_KY';") == 1, "The real Claim button enrolls exactly once through the shared registration engine.");
            Find<Button>(seats, "btnSeatResult").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(Find<Label>(seats, "lblSeatNotice").Text.Contains("đã xác nhận"), "The real result lookup resolves the persisted claim request after enrollment.");
            Expect(await Count("SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024001' AND trang_thai='DANG_KY';") == 1, "Claim result lookup creates no duplicate enrollment.");
            await Shot("10-claim-result-compact");
            await PhaseFourAcademicUiCheck.RunAsync(form, cs, output, checks);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, scope = "Native controls and code-driven events, offscreen 96-DPI captures; no physical-input, FPS or production-load claim." }, new JsonSerializerOptions { WriteIndented = true }));
        } catch (Exception e) {
            File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
        } finally { form.Close(); }
    }
    private static T Find<T>(Control parent, string name) where T : Control => Descendants(parent).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static async Task Wait(Func<bool> predicate) { var until = DateTime.UtcNow.AddSeconds(30); while (!predicate()) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Native action timed out."); await Task.Delay(40); } }
}
