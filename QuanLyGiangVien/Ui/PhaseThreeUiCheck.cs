using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in native control tests against a disposable SQL database.</summary>
internal static class PhaseThreeUiCheck
{
    internal static void Attach(CampusForm form, string connectionString)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_PHASE3_UI_CHECK"); if (string.IsNullOrWhiteSpace(output)) return;
        if (!new SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Phase-three native checks require a disposable database.");
        form.StartPosition = FormStartPosition.Manual; form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => await Run(form, connectionString, output);
    }
    private static async Task Run(CampusForm form, string cs, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Expect(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks.Add(text); }
        async Task Sql(string text)
        { await using var c = new SqlConnection(cs); await c.OpenAsync(); using var q = new SqlCommand(text, c); await q.ExecuteNonQueryAsync(); }
        async Task Login(bool student)
        {
            form.SelectLoginPortal(student ? LoginPortal.Student : LoginPortal.Training);
            Find<TextBox>(form, "txtLoginUsername").Text = student ? "sv2024001" : "daotao";
            Find<TextBox>(form, "txtLoginPassword").Text = Environment.GetEnvironmentVariable(student ? "DEMO_STUDENT_PASSWORD" : "DEMO_TRAINING_PASSWORD")!;
            await form.AuthenticateAsync(); Expect(form.ConnectedIdentity?.Role == (student ? "SINH_VIEN" : "DAO_TAO"), "Password login opens the intended portal.");
        }
        async Task Select(RegistrationWorkspace workspace, string label)
        {
            var tabs = Descendants(workspace).OfType<TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Kết quả đăng ký"));
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(p => p.Text == label);
            await Wait(() => !workspace.IsBusy); await Task.Delay(80);
        }
        async Task Shot(string name, int width, int height)
        {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(height)); await Task.Delay(150);
            using var bmp = UiCapture.Client(form); bmp.Save(Path.Combine(output, name + ".png"));
        }
        try
        {
            await Sql("UPDATE dbo.HOC_KY SET trang_thai='DANG_MO',lifecycle='REGISTRATION',tg_bat_dau=DATEADD(day,-1,DATEADD(hour,7,SYSUTCDATETIME())),tg_ket_thuc=DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())),han_huy_dk=DATEADD(day,1,DATEADD(hour,7,SYSUTCDATETIME())) WHERE ma_hk='HK1-2627'; DELETE dbo.CAMPUS_PERMISSION WHERE account_id IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao'); DELETE dbo.DANG_KY WHERE ma_sv='SV2024001'; UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY');");
            await Login(true); var workspace = (RegistrationWorkspace)form.CurrentModulePage!;
            await workspace.ReloadForSemesterAsync("HK1-2627");
            var termChoice = Find<ComboBox>(workspace, "cboHocKy");
            Expect(Convert.ToString(termChoice.SelectedValue) == "HK1-2627" && !string.IsNullOrWhiteSpace(termChoice.Text), "Semester selector retains both its actual value and display text in the compact native layout.");
            if (workspace.PendingStore().Load() is { } leftover) workspace.PendingStore().Clear(leftover.RequestId);
            await Sql("DELETE dbo.DK3_EXCEPTION_USE; DELETE dbo.DK3_EXCEPTION_REQUEST WHERE student='SV2024001';");
            Expect(Descendants(workspace).OfType<TabPage>().Any(p => p.Text == "Giỏ nháp & đổi lớp"), "Student has a durable cart tab.");
            var lookup = Descendants(workspace).OfType<TraCuuPage>().Single();
            var offered = Find<DataGridView>(lookup, "dgvLopMo");
            var row = offered.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["ma_lhp"].Value) == "LHP0002");
            offered.ClearSelection(); offered.CurrentCell = row.Cells[0]; row.Selected = true;
            Find<Button>(lookup, "btnDangKy").PerformClick(); await Wait(() => !workspace.IsBusy);
            var cart = Descendants(workspace).OfType<RegistrationCartPage>().Single();
            Expect(Find<DataGridView>(cart, "dgvCart").Rows.Count == 1, "The real Add-to-cart button persists a class without enrolling.");
            using (var saved = await workspace.Workflow.CartAsync("HK1-2627")) Expect(Convert.ToString(saved.Rows[0]["class_json"])!.Contains("LHP0002"), "SQL stores the cart for the signed-in student and semester.");
            Find<Button>(cart, "btnCartPrecheck").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(Find<TextBox>(cart, "txtCartDetails").Text.Contains("Tổng tín chỉ"), "The real cart precheck displays credit and eligibility information.");
            await Shot("01-cart-wide", 1440, 960); await Shot("02-cart-compact", 1100, 720);
            var cartGrid = Find<DataGridView>(cart, "dgvCart");
            Expect(cartGrid.Height >= cartGrid.ColumnHeadersHeight + cartGrid.Rows[0].Height, "Compact cart viewport fits its header and a complete registration row.");
            await form.LogoutAsync(); await Login(true); workspace = (RegistrationWorkspace)form.CurrentModulePage!;
            await workspace.ReloadForSemesterAsync("HK1-2627"); await Select(workspace, "Giỏ nháp & đổi lớp"); cart = Descendants(workspace).OfType<RegistrationCartPage>().Single();
            Expect(Find<DataGridView>(cart, "dgvCart").Rows.Count == 1, "Logging out and back in restores the saved cart.");
            using (var saved = await workspace.Workflow.CartAsync("HK1-2627"))
            {
                var id = Guid.NewGuid(); var version = (byte[])saved.Rows[0]["version"];
                var pending = new PendingRegistration(id, "HK1-2627", ["LHP0002"], [], version); workspace.PendingStore().Save(pending);
                // Commit succeeds but the client deliberately discards its response, emulating lost acknowledgement.
                using var ignored = await workspace.Workflow.SubmitAsync(id, pending.Term, pending.Add, pending.Remove, pending.CartVersion);
                await workspace.ExecuteAsync(cart.RefreshDataAsync);
                Expect(Find<Button>(cart, "btnRequestLookup").Enabled && !Find<Button>(cart, "btnCartSubmit").Enabled, "An unresolved persisted request disables new submissions and exposes result lookup.");
                var retry = Descendants(cart).OfType<Button>().Single(b => b.Text == "Gửi lại cùng yêu cầu"); retry.PerformClick(); await Wait(() => !workspace.IsBusy);
                Expect(workspace.PendingStore().Load() is null, "Retry of a committed request resolves and clears the pending marker.");
                using var result = await workspace.Service.KetQuaAsync("HK1-2627", null, false);
                Expect(result.Rows.Cast<DataRow>().Count(r => Convert.ToString(r["ma_lhp"]) == "LHP0002") == 1, "A lost commit acknowledgement followed by Retry creates exactly one enrollment.");
            }
            await Select(workspace, "Đơn ngoại lệ");
            var petition = Descendants(workspace).OfType<ExceptionRequestsPage>().Single();
            var petitionTabs = Descendants(petition).OfType<TabControl>().Single(); petitionTabs.SelectedIndex = 1;
            await Shot("09-student-petition-form-compact", 1100, 720);
            Find<TextBox>(petition, "txtExceptionReason").Text = "Xin học vượt theo kế hoạch cá nhân";
            Find<Button>(petition, "btnExceptionSubmit").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(Find<DataGridView>(petition, "dgvExceptions").Rows.Count == 1, "The real petition button creates the student's own pending request.");
            Expect(!workspace.HasUnsavedChanges, "Successful petition submission clears its local draft.");
            petitionTabs.SelectedIndex = 0;
            await Shot("03-student-petition-compact", 1100, 720);
            await Select(workspace, "Thông báo");
            Expect(Find<DataGridView>(workspace, "dgvNotifications").Rows.Count > 0, "Committed student events appear in the native notification center.");
            await Shot("04-student-notifications-compact", 1100, 720);
            await form.LogoutAsync(); await Login(false); await form.OpenModuleAsync(CampusModule.Reports);
            workspace = (RegistrationWorkspace)form.CurrentModulePage!; await Select(workspace, "Vận hành lớp & lịch");
            var operations = Descendants(workspace).OfType<ClassOperationsPage>().Single();
            Expect(!Descendants(operations).OfType<Button>().Single(b => b.Text == "Chuyển trạng thái").Enabled, "Sample training account does not implicitly gain new operational permissions.");
            await Shot("05-training-permission-compact", 1100, 720);
            await Sql("INSERT dbo.CAMPUS_PERMISSION(account_id,permission) SELECT ma_tk,'CLASS_OPERATE' FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao'; INSERT dbo.CAMPUS_PERMISSION(account_id,permission) SELECT ma_tk,'EXCEPTION_REVIEW' FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao';");
            await workspace.ExecuteAsync(operations.RefreshDataAsync);
            Expect(Descendants(operations).OfType<Button>().Single(b => b.Text == "Chuyển trạng thái").Enabled, "An explicit fixture-only operational grant enables the controls.");
            var operationTabs = Descendants(operations).OfType<TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Lịch ngoại lệ"));
            operationTabs.SelectedTab = operationTabs.TabPages.Cast<TabPage>().Single(p => p.Text == "Lịch ngoại lệ");
            Descendants(operations).OfType<Button>().Single(b => b.Text == "Xem ngày học thực tế").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(Find<DataGridView>(operations, "dgvActualCalendar").Rows.Count > 0, "Operational schedule editor reads the shared actual-date calendar.");
            var classChoice = Descendants(operations).OfType<ComboBox>().Single(c => c.DisplayMember == "ma_lhp");
            Expect(classChoice.SelectedItem is DataRowView && !string.IsNullOrWhiteSpace(classChoice.Text), "The operational class selector retains its selected class and actual display text.");
            await Shot("06-operations-calendar-wide", 1440, 960); await Shot("07-operations-calendar-compact", 1100, 720);
            var actualGrid = Find<DataGridView>(operations, "dgvActualCalendar");
            Expect(actualGrid.Height >= actualGrid.ColumnHeadersHeight + actualGrid.Rows[0].Height, "Compact operational calendar fits its header and a complete teaching occurrence.");
            await Select(workspace, "Duyệt ngoại lệ");
            Expect(Find<DataGridView>(workspace, "dgvExceptions").Rows.Count == 1, "An explicitly authorized reviewer sees the pending student petition.");
            await Shot("08-training-review-compact", 1100, 720);
            Descendants(Descendants(workspace).OfType<ExceptionRequestsPage>().Single()).OfType<TabControl>().Single().SelectedIndex = 1;
            await Shot("10-training-decision-form-compact", 1100, 720);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, scope = "Native controls and code-driven events, offscreen rendered captures; no physical-input or FPS claim." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString());
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
        }
        finally { form.Close(); }
    }
    private static T Find<T>(Control parent, string name) where T : Control => Descendants(parent).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static async Task Wait(Func<bool> predicate)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!predicate()) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Native action did not complete."); await Task.Delay(40); }
    }
}
