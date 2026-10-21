using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

// Called only by the opt-in native harness; never seeds normal application sessions.
internal static class PhaseFourAcademicUiCheck
{
    internal static async Task RunAsync(CampusForm form, string cs, string output, List<string> checks)
    {
        var catalog = new SqlConnectionStringBuilder(cs).InitialCatalog;
        if (catalog != "DangKyTinChi_Minh_Test_Native4_19" && !System.Text.RegularExpressions.Regex.IsMatch(catalog,
            @"^DangKyTinChi_Minh_Test_Verify_V[0-9a-f]{12}_Native4$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Academic native checks require the explicit disposable native fixture.");
        void Expect(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        async Task<int> Count(string sql)
        {
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync();
            using var command = new SqlCommand(sql, connection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        Expect(form.ConnectedIdentity?.Role == "SINH_VIEN", "Academic native workflow starts in the authenticated student portal.");
        Expect(await Count("SELECT COUNT(*) FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_sv='SV2024001' AND d.trang_thai='DANG_KY' AND l.ma_hp='IT102';") == 1,
            "Academic progress uses the real IT102 enrollment created by Claim, without fabricated grades.");
        await SeedAsync(cs);
        var workspace = (RegistrationWorkspace)form.CurrentModulePage!;
        await workspace.ReloadForSemesterAsync("HK1-2627");
        await SelectPage(workspace, "CTĐT & kế hoạch học");
        var page = Descendants(workspace).OfType<AcademicPlanningPage>().Single();
        await SelectInner(page, "Tiến độ CTĐT");
        var audit = Find<DataGridView>(page, "dgvDegreeAudit");
        Expect(audit.Rows.Count == 2, "The native degree audit renders both required courses from the published assigned curriculum.");
        Expect(audit.Rows.Cast<DataGridViewRow>().Sum(r => Convert.ToInt32(r.Cells["Đang học"].Value)) == 3
            && audit.Rows.Cast<DataGridViewRow>().Sum(r => Convert.ToInt32(r.Cells["Đã đạt"].Value)) == 0,
            "The native audit counts three IT102 credits in progress and no invented completed grades.");
        await Shot("11-audit-compact");
        Expect(CompleteRow(audit), "Compact degree audit fits its header and at least one complete course row.");
        await SelectInner(page, "Xếp kế hoạch");
        Choose(Find<ComboBox>(page, "cboAcademicPlanCourse"), "IT101");
        Choose(Find<ComboBox>(page, "cboAcademicPlanTerm"), "HK2-2627");
        Find<Button>(page, "btnAcademicPlanAdd").PerformClick();
        await SelectInner(page, "Kế hoạch đã xếp");
        var plan = Find<DataGridView>(page, "dgvAcademicPlan");
        Expect(plan.Rows.Count == 1 && Convert.ToString(plan.Rows[0].Cells["course"].Value) == "IT101", "The real Add button places IT101 in the future-semester draft.");
        Find<Button>(page, "btnAcademicPlanSave").PerformClick();
        await Wait(() => !workspace.IsBusy);
        Expect(await Count("SELECT COUNT(*) FROM dbo.AP4_PLAN WHERE student='SV2024001' AND program_id='N4_CT' AND status='DRAFT' AND JSON_VALUE(items_json,'$[0].course')='IT101' AND JSON_VALUE(items_json,'$[0].term')='HK2-2627' AND source_hash=dbo.ufn_AP4_SourceHash(student,program_id);") == 1,
            "The real Save button persists one draft against the current academic source hash.");
        Expect(!page.HasUnsavedChanges, "Saving the academic draft clears its unsaved-change state before navigation.");
        Find<Button>(page, "btnAcademicPlanSubmit").PerformClick();
        await Wait(() => !workspace.IsBusy);
        Expect(await Count("SELECT COUNT(*) FROM dbo.AP4_PLAN WHERE student='SV2024001' AND status='SUBMITTED' AND source_hash=dbo.ufn_AP4_SourceHash(student,program_id);") == 1,
            "The real Submit button submits a fresh plan with a confirmed IT101 offering.");
        await Shot("12-plan-compact");
        Expect(CompleteRow(plan), "Compact academic plan fits its header and a complete planned-course row.");

        await form.LogoutAsync();
        form.SelectLoginPortal(LoginPortal.Training);
        Find<TextBox>(form, "txtLoginUsername").Text = "daotao";
        Find<TextBox>(form, "txtLoginPassword").Text = Environment.GetEnvironmentVariable("DEMO_TRAINING_PASSWORD")
            ?? throw new InvalidOperationException("The native training password must be configured externally.");
        await form.AuthenticateAsync();
        Expect(form.ConnectedIdentity?.Role == "DAO_TAO", "The academic approval uses a separately authenticated training portal.");
        await form.OpenModuleAsync(CampusModule.Reports);
        workspace = (RegistrationWorkspace)form.CurrentModulePage!;
        await workspace.ReloadForSemesterAsync("HK1-2627");
        await SelectPage(workspace, "Cố vấn & kế hoạch");
        page = Descendants(workspace).OfType<AcademicPlanningPage>().Single();
        var students = Find<ComboBox>(page, "cboAcademicPlanStudent");
        Expect(students.Items.Count == 1 && students.SelectedItem is WorkflowChoice { Value: "SV2024001" },
            "The advisor directory exposes only the one explicitly assigned student.");
        await SelectInner(page, "Cố vấn");
        Choose(Find<ComboBox>(page, "cboAcademicAdviceAction"), "COMMENT");
        Find<TextBox>(page, "txtAcademicPlanComment").Text = "Native fixture: đã đối soát căn cứ mở môn IT101 kỳ 2.";
        Find<Button>(page, "btnAcademicPlanAdvise").PerformClick();
        await Wait(() => !workspace.IsBusy);
        Expect(await Count("SELECT COUNT(*) FROM dbo.AP4_COMMENT WHERE student='SV2024001' AND action='COMMENT';") == 1,
            "The real advisor Comment button persists a scoped comment.");
        Choose(Find<ComboBox>(page, "cboAcademicAdviceAction"), "APPROVE");
        Find<TextBox>(page, "txtAcademicPlanComment").Text = "Native fixture: duyệt kế hoạch sau đối soát dữ liệu nguồn.";
        Find<Button>(page, "btnAcademicPlanAdvise").PerformClick();
        await Wait(() => !workspace.IsBusy);
        Expect(await Count("SELECT COUNT(*) FROM dbo.AP4_PLAN WHERE student='SV2024001' AND status='APPROVED' AND approved_hash=source_hash AND approved_hash=dbo.ufn_AP4_SourceHash(student,program_id);") == 1,
            "The real Approve button stores approval against the exact current source hash.");
        Expect(await Count("SELECT COUNT(*) FROM dbo.AP4_COMMENT WHERE student='SV2024001' AND action IN('COMMENT','APPROVE');") == 2,
            "Comment and approval each create exactly one academic decision record.");
        Expect(!page.HasUnsavedChanges, "A successful advisor decision clears its comment draft.");
        await SelectInner(page, "Ý kiến cố vấn");
        await Shot("13-advice-compact");
        var commentGrid = Descendants(page).OfType<DataGridView>().Single(g => g.DataSource is DataTable t && t.Columns.Contains("content") && t.Columns.Contains("action"));
        Expect(commentGrid.Rows.Count == 2 && CompleteRow(commentGrid), "Compact advisor history fits a complete persisted decision row.");

        async Task Shot(string name)
        {
            form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
            await Task.Delay(180);
            using var bitmap = UiCapture.Client(form);
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
    }

    private static async Task SeedAsync(string cs)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        using var command = new SqlCommand("""
            SET XACT_ABORT ON;
            BEGIN TRY
            BEGIN TRAN;
            DECLARE @account int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()),@role varchar(20),@student varchar(20);
            IF @account IS NULL THROW 54000,N'Native fixture requires an explicitly mapped Windows account.',1;
            SELECT @role=vai_tro,@student=ma_sv FROM dbo.TAI_KHOAN WHERE ma_tk=@account;
            UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@account;
            DELETE dbo.AP4_COMMENT WHERE student='SV2024001'; DELETE dbo.AP4_PLAN WHERE student='SV2024001';
            DELETE dbo.AP4_ADVISOR WHERE account_id IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao');
            DELETE dbo.AP4_SUBSTITUTION WHERE program_id='N4_CT';
            DELETE dbo.HV_HISTORY WHERE id LIKE 'N4[_]%'; DELETE dbo.HV_DOCUMENT WHERE id LIKE 'N4[_]%' OR kind='ASSIGNMENT' AND JSON_VALUE(payload,'$.student')='SV2024001';
            IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN s JOIN dbo.LOP l ON l.ma_lop=s.ma_lop WHERE s.ma_sv='SV2024001' AND s.ma_nganh='CNTT' AND l.khoa_hoc=2024)
                THROW 54000,N'Native fixture student must have the real CNTT/2024 profile.',1;
            IF EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp IN('IT101','IT102') AND so_tin_chi<>3)
                THROW 54000,N'Native curriculum requires the fixture three-credit course catalog.',1;
            EXEC dbo.usp_HocVu_Save 'PROGRAM','N4_CT',NULL,N'{"name":"Native fixture CTĐT 2024","major":"CNTT","cohort":2024,"requiredCredits":6}';
            EXEC dbo.usp_HocVu_Save 'GROUP','N4_G',NULL,N'{"program":"N4_CT","name":"Native cơ sở","requiredCredits":6}';
            EXEC dbo.usp_HocVu_Save 'COURSE','N4_C1',NULL,N'{"program":"N4_CT","course":"IT101","group":"N4_G","required":true}';
            EXEC dbo.usp_HocVu_Save 'COURSE','N4_C2',NULL,N'{"program":"N4_CT","course":"IT102","group":"N4_G","required":true}';
            DECLARE @version binary(8)=(SELECT version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='N4_CT');
            EXEC dbo.usp_HocVu_Publish 'PROGRAM','N4_CT',@version;
            EXEC dbo.usp_HocVu_Save 'ASSIGNMENT','N4_ASSIGN',NULL,N'{"student":"SV2024001","program":"N4_CT"}';
            DECLARE @offering binary(8)=(SELECT version FROM dbo.AP4_OFFERING WHERE course='IT101' AND term='HK2-2627');
            EXEC dbo.usp_AP4_SaveOffering 'IT101','HK2-2627',N'Native fixture: nguồn dự kiến mở môn đã xác nhận',@offering;
            DECLARE @advisor int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao');
            IF @advisor IS NULL THROW 54000,N'Native fixture requires its training account.',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=@advisor AND permission='ADVISE_PLAN')
                INSERT dbo.CAMPUS_PERMISSION(account_id,permission) VALUES(@advisor,'ADVISE_PLAN');
            EXEC dbo.usp_AP4_AssignAdvisor 'SV2024001',@advisor,1,N'Native fixture: phân công cố vấn rõ ràng cho một sinh viên';
            UPDATE dbo.TAI_KHOAN SET vai_tro=@role,ma_sv=@student WHERE ma_tk=@account;
            COMMIT;
            END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
            """, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    private static bool CompleteRow(DataGridView grid) => grid.Rows.Count > 0 && grid.Height >= grid.ColumnHeadersHeight + grid.Rows[0].Height;
    private static void Choose(ComboBox combo, string value)
    {
        combo.SelectedItem = combo.Items.Cast<WorkflowChoice>().Single(c => c.Value == value);
    }
    private static async Task SelectPage(RegistrationWorkspace workspace, string title)
    {
        var tab = Descendants(workspace).OfType<TabPage>().Single(t => t.Text == title);
        ((TabControl)tab.Parent!).SelectedTab = tab;
        await Wait(() => !workspace.IsBusy);
        await Task.Delay(100);
    }
    private static async Task SelectInner(AcademicPlanningPage page, string title)
    {
        var tab = Descendants(page).OfType<TabPage>().Single(t => t.Text == title);
        ((TabControl)tab.Parent!).SelectedTab = tab;
        await Task.Delay(80);
    }
    private static T Find<T>(Control parent, string name) where T : Control => Descendants(parent).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static async Task Wait(Func<bool> predicate)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!predicate())
        {
            if (DateTime.UtcNow > until) throw new InvalidOperationException("Native academic action timed out.");
            await Task.Delay(40);
        }
    }
}
