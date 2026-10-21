using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Academics;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Academics;
using QuanLyGiangVien.Features.Shell;
using QuanLyGiangVien.Features.Registration;

namespace QuanLyGiangVien.Ui;

/// <summary>Native control verification, opt-in and isolated SQL only. No physical input claims.</summary>
internal static class PhaseTwoUiCheck
{
    internal static void Attach(CampusForm form, string connectionString)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_PHASE2_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        if (!new SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Phase-two UI verification requires an isolated test database.");
        form.StartPosition = FormStartPosition.Manual; form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => await RunAsync(form, connectionString, output);
    }

    private static async Task RunAsync(CampusForm form, string connectionString, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
        async Task Shot(string name, int width)
        {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(960)); await Task.Delay(200);
            using var bitmap = UiCapture.Client(form); bitmap.Save(Path.Combine(output, name + ".png"));
        }
        try
        {
            form.SelectLoginPortal(LoginPortal.Training);
            Descendants(form).OfType<TextBox>().Single(x => x.Name == "txtLoginUsername").Text = "daotao";
            Descendants(form).OfType<TextBox>().Single(x => x.Name == "txtLoginPassword").Text = Environment.GetEnvironmentVariable("DEMO_TRAINING_PASSWORD")
                ?? throw new InvalidOperationException("Missing isolated test credential.");
            await form.AuthenticateAsync(); Expect(form.ConnectedIdentity?.Role == "DAO_TAO", "Real training password login succeeds.");
            await form.OpenModuleAsync(CampusModule.Academics);
            var page = form.CurrentPage as AcademicPage ?? throw new InvalidOperationException("Academic page did not open.");
            var section = page.Sections.Single(s => s.TableName == "Programs");
            Expect(page.Sections.Count == 10, "All ten typed academic editors are mounted.");
            Expect(Descendants(page).OfType<Label>().Any(l => l.Text.Contains("CTĐT/quy chế chỉ áp dụng sau công bố") && l.Text.Contains("có hiệu lực khi lưu")), "The applicability notice distinguishes publication from immediately saved holds and profiles.");
            Expect(Descendants(page).OfType<TabPage>().Any(t => t.Text == "Tài khoản"), "Account administration is mounted in the academic workspace.");
            Expect(page.Sections.All(s => Descendants(s).Where(c => c is TextBox or ComboBox or DateTimePicker).All(c => !string.IsNullOrWhiteSpace(c.AccessibleName))), "Editor inputs have accessible names.");
            Expect(page.Sections.All(s => !Descendants(s).OfType<PropertyGrid>().Any()), "Academic editors expose typed fields instead of raw JSON or a property grid.");
            var resultEditor = page.Sections.Single(s => s.TableName == "Results");
            Expect(((ComboBox)resultEditor.Input("recognition")).Items.Cast<AcademicOption>().Select(o => o.Value).SequenceEqual(["GRADED", "TRANSFER", "EXEMPT"]),
                "Recognized results distinguish graded study, transfer credit and an exemption decision.");
            Expect(resultEditor.Input("grade").Text.Length == 0 && !((DateTimePicker)resultEditor.Input("publication")).Checked,
                "A new result form invents neither a grade nor a publication date.");

            var repository = new AcademicRepository(connectionString);
            using var snapshot = await repository.SnapshotAsync();
            string id = "UI_P2_" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            string major = AcademicSection.RowText(snapshot.Tables["MajorChoices"]!.Rows[0], "id");
            string payload = JsonSerializer.Serialize(new { name = "Chương trình kiểm thử giao diện", major, cohort = 2026, requiredCredits = 120 });
            await repository.SaveAsync(new("PROGRAM", id, null, payload)); await page.ReloadAsync();
            var selected = section.DataGrid.Rows.Cast<DataGridViewRow>().Single(r => r.DataBoundItem is DataRowView v && AcademicSection.RowText(v.Row, "id") == id);
            section.DataGrid.CurrentCell = selected.Cells[0]; selected.Selected = true;
            var input = (TextBox)section.Input("name"); input.Text = "Bản nháp học vụ cần giữ";
            Expect(section.HasUnsavedChanges, "Editing a program creates a real unsaved draft.");
            string oldVersion = section.Version;
            await repository.SaveAsync(new("PROGRAM", id, oldVersion, JsonSerializer.Serialize(new { name = "Đã cập nhật ở phiên khác", major, cohort = 2026, requiredCredits = 121 })));
            var save = Descendants(section).OfType<Button>().Single(b => b.Text == "Lưu thay đổi"); save.PerformClick();
            for (int i = 0; i < 100 && section.IsBusy; i++) await Task.Delay(50);
            Expect(!section.IsBusy && section.Notice.Contains("Bản nháp"), "Actual Save shows a stale-version instruction inline.");
            Expect(input.Text == "Bản nháp học vụ cần giữ" && section.HasUnsavedChanges && section.Version == oldVersion,
                "A failed stale Save preserves both the draft and original version token.");
            Expect(input.TabStop && input.CanSelect, "The retained draft remains keyboard focusable."); form.Show(); form.Activate(); input.Select(); await Task.Delay(80); input.Focus();
            Expect(input.Focused, "Keyboard focus can return to the draft field after validation.");
            await Shot("01-academic-conflict-wide", 1440); await Shot("02-academic-conflict-compact", 1100);

            var error = Descendants(section).OfType<Label>().Single(l => l.Text == section.Notice);
            Expect(error.Top >= 0 && error.Bottom <= error.Parent!.ClientSize.Height, "Inline error scrolls into the visible editor viewport.");
            var parentTabs = section.Parent?.Parent as TabControl;
            Expect(parentTabs != null && parentTabs.TabPages.Count == 4, "Curriculum separates versions, groups, courses and AND/OR conditions.");
            var requirements = page.Sections.Single(s => s.TableName == "Requirements");
            parentTabs!.SelectedTab = (TabPage)requirements.Parent!;
            Expect(section.HasUnsavedChanges && input.Text == "Bản nháp học vụ cần giữ", "Switching internal tabs preserves independent unsaved drafts.");
            await Shot("03-academic-requirements-compact", 1100);
            var policy = page.Sections.Single(s => s.TableName == "Policies");
            var policyInner = policy.Parent!.Parent as TabControl ?? throw new InvalidOperationException("Policy tabs missing.");
            var policyGroup = Ancestor<TabPage>(policyInner);
            var outer = (TabControl)policyGroup.Parent!;
            outer.SelectedTab = policyGroup; policyInner.SelectedTab = (TabPage)policy.Parent!;
            policy.New();
            var preset = Descendants(policy).OfType<ComboBox>().Single(c => c.AccessibleName == "Bộ mẫu quy chế tham khảo UIT");
            var apply = Descendants(policy).OfType<Button>().Single(b => b.Text == "Điền mẫu vào bản nháp");
            apply.PerformClick();
            Expect(policy.Input("minCredits").Text == "14" && policy.Input("maxCredits").Text == "24" && policy.Input("excellentThreshold").Text == "8" && policy.Input("excellentMaxCredits").Text == "30",
                "The UIT main-term reference fills 14–24 credits and the official 8/30 threshold.");
            preset.SelectedIndex = 1; apply.PerformClick();
            Expect(policy.Input("minCredits").Text == "0" && policy.Input("maxCredits").Text == "12" && policy.Input("excellentThreshold").Text == "" && policy.Input("maxAttempts").Text == "0",
                "The UIT amended summer reference allows 0–12 credits without the superseded GPA threshold or an invented attempt limit.");
            using var afterPreset = await repository.SnapshotAsync();
            Expect(policy.HasUnsavedChanges && afterPreset.Tables["Policies"]!.Rows.Count == snapshot.Tables["Policies"]!.Rows.Count,
                "Applying a reference preset changes only the unsaved form and never publishes or writes a policy.");
            await Shot("04-academic-uit-summer-compact", 1100);
            if (policy.Input("maxAttempts").Parent is ScrollableControl policyEditor) policyEditor.ScrollControlIntoView(policy.Input("maxAttempts"));
            await Shot("05-academic-uit-summer-limits-compact", 1100);
            section.New(); policy.New();
            string course = AcademicSection.RowText(snapshot.Tables["CourseChoices"]!.Rows[0], "id");
            string courseRecord = "UI_C2_" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            await repository.SaveAsync(new("COURSE", courseRecord, null, JsonSerializer.Serialize(new { program = id, course, required = true })));
            using var beforePublish = await repository.SnapshotAsync();
            var programRow = beforePublish.Tables["Programs"]!.AsEnumerable().Single(r => AcademicSection.RowText(r, "id") == id);
            int courseCredits = Convert.ToInt32(snapshot.Tables["CourseChoices"]!.Rows[0]["credits"]);
            await repository.SaveAsync(new("PROGRAM", id, AcademicSection.RowText(programRow, "version"), JsonSerializer.Serialize(new { name = "Chương trình kiểm thử đã công bố", major, cohort = 2026, requiredCredits = courseCredits })));
            using var readyToPublish = await repository.SnapshotAsync();
            var readyProgram = readyToPublish.Tables["Programs"]!.AsEnumerable().Single(r => AcademicSection.RowText(r, "id") == id);
            await repository.PublishProgramAsync(id, AcademicSection.RowText(readyProgram, "version")); await page.ReloadAsync();
            var curriculumInner = section.Parent!.Parent as TabControl ?? throw new InvalidOperationException("Curriculum tabs missing.");
            outer.SelectedTab = Ancestor<TabPage>(curriculumInner); curriculumInner.SelectedTab = (TabPage)section.Parent!;
            var publishedRow = section.DataGrid.Rows.Cast<DataGridViewRow>().Single(r => r.DataBoundItem is DataRowView v && AcademicSection.RowText(v.Row, "id") == id);
            section.DataGrid.CurrentCell = publishedRow.Cells[0]; publishedRow.Selected = true;
            Expect(!section.Input("name").Enabled && !Descendants(section).OfType<Button>().Single(b => b.Text == "Lưu thay đổi").Enabled,
                "Published program versions are visibly read-only.");
            await Shot("06-academic-published-wide", 1440);
            var courses = page.Sections.Single(s => s.TableName == "Courses"); curriculumInner.SelectedTab = (TabPage)courses.Parent!;
            var publishedCourse = courses.DataGrid.Rows.Cast<DataGridViewRow>().Single(r => r.DataBoundItem is DataRowView v && AcademicSection.RowText(v.Row, "id") == courseRecord);
            courses.DataGrid.CurrentCell = publishedCourse.Cells[0]; publishedCourse.Selected = true;
            Expect(!courses.Input("course").Enabled && !courses.Input("required").Enabled && !Descendants(courses).OfType<Button>().Single(b => b.Text == "Lưu thay đổi").Enabled,
                "Course details inherit the published parent curriculum's read-only state.");
            await Shot("07-academic-published-course-compact", 1100);
            foreach (var editor in page.Sections) editor.New();
            await form.LogoutAsync(); form.SelectLoginPortal(LoginPortal.Student);
            Descendants(form).OfType<TextBox>().Single(x => x.Name == "txtLoginUsername").Text = "sv2024001";
            Descendants(form).OfType<TextBox>().Single(x => x.Name == "txtLoginPassword").Text = Environment.GetEnvironmentVariable("DEMO_STUDENT_PASSWORD")
                ?? throw new InvalidOperationException("Missing isolated student test credential.");
            await form.AuthenticateAsync(); Expect(form.ConnectedIdentity?.Role == "SINH_VIEN", "A real student password opens the own registration portal.");
            await form.OpenModuleAsync(CampusModule.Registration);
            var workspace = form.CurrentModulePage as RegistrationWorkspace ?? throw new InvalidOperationException("Student registration workspace missing.");
            using var registrationsBefore = await workspace.Service.KetQuaAsync(workspace.MaHocKy, null, true);
            var lookup = Descendants(workspace).OfType<TraCuuPage>().Single();
            var classes = Descendants(lookup).OfType<DataGridView>().Single(g => g.Name == "dgvLopMo");
            Expect(classes.MultiSelect && classes.Rows.Count >= 2, "The registration lookup supports selecting a linked group of classes.");
            classes.CurrentCell = classes.Rows[0].Cells[0]; classes.ClearSelection(); classes.Rows[0].Selected = true; classes.Rows[1].Selected = true;
            var precheck = Descendants(lookup).OfType<Button>().Single(b => b.Name == "btnAcademicPrecheck");
            Expect(precheck.Enabled && classes.SelectedRows.Count == 2, "Academic precheck remains available for a selected group outside the registration window.");
            precheck.PerformClick();
            var conditions = Descendants(lookup).OfType<TextBox>().Single(t => t.Name == "lblAcademicConditions");
            for (int i = 0; i < 100 && !conditions.Text.Contains("Tổng tín chỉ", StringComparison.Ordinal); i++) await Task.Delay(50);
            Expect(conditions.ReadOnly && conditions.Text.Contains("Tổng tín chỉ", StringComparison.Ordinal), "The actual precheck button displays the proposed credit total.");
            Expect(conditions.Text.Contains("chưa được gắn CTĐT", StringComparison.OrdinalIgnoreCase) || conditions.Text.Contains("chưa công bố quy chế", StringComparison.OrdinalIgnoreCase),
                "The precheck explains missing curriculum or unpublished policy without inventing student data.");
            await Shot("08-student-precheck-wide", 1440); await Shot("09-student-precheck-compact", 1100);
            var studentTabs = Descendants(workspace).OfType<TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Học vụ của tôi"));
            studentTabs.SelectedTab = studentTabs.TabPages.Cast<TabPage>().Single(p => p.Text == "Kết quả đăng ký");
            for (int i = 0; i < 100 && !workspace.CanLeave(); i++) await Task.Delay(50);
            var creditButton = Descendants(workspace).OfType<Button>().Single(b => b.Name == "btnCheckCreditPlan");
            Expect(creditButton.Enabled, "Students can check total credits without requiring an open registration window.");
            creditButton.PerformClick();
            var creditPlan = Descendants(workspace).OfType<TextBox>().Single(t => t.Name == "txtCreditPlanCheck");
            for (int i = 0; i < 100 && !creditPlan.Text.Contains("Tổng tín chỉ", StringComparison.Ordinal); i++) await Task.Delay(50);
            Expect(creditPlan.ReadOnly && creditPlan.Text.Contains("tối thiểu", StringComparison.OrdinalIgnoreCase), "The actual credit-plan button explains the minimum-credit requirement without finalizing or writing registrations.");
            await Shot("12-student-credit-plan-compact", 1100);
            studentTabs.SelectedTab = studentTabs.TabPages.Cast<TabPage>().Single(p => p.Text == "Học vụ của tôi");
            var studentAcademic = Descendants(workspace).OfType<StudentAcademicPage>().Single();
            var ownGrid = Descendants(studentAcademic).OfType<DataGridView>().Single();
            for (int i = 0; i < 100 && (ownGrid.DataSource == null || !workspace.CanLeave()); i++) await Task.Delay(50);
            Expect(ownGrid.DataSource is DataTable ownSummary && ownSummary.Rows.Count == 1 && AcademicSection.RowText(ownSummary.Rows[0], "student") == form.ConnectedIdentity!.StudentId,
                "The own academic summary binds only the signed-in student's identity.");
            using var registrationsAfter = await workspace.Service.KetQuaAsync(workspace.MaHocKy, null, true);
            string RegistrationState(DataTable table) => string.Join("|", table.Rows.Cast<DataRow>().Select(r => string.Join(";", r.ItemArray.Select(v => Convert.ToString(v)))).Order(StringComparer.Ordinal));
            Expect(RegistrationState(registrationsBefore) == RegistrationState(registrationsAfter), "Group precheck and academic summary perform no registration writes.");
            await Shot("10-student-academic-wide", 1440); await Shot("11-student-academic-compact", 1100);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, scope = "Native control events and offscreen captures at 96 DPI; no physical-mouse E2E or FPS claim." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = 1;
        }
        finally
        {
            if (form.CurrentPage is AcademicPage academic) foreach (var section in academic.Sections) section.New();
            form.Close();
        }
    }
    private static T Ancestor<T>(Control control) where T : Control
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent) if (parent is T match) return match;
        throw new InvalidOperationException(typeof(T).Name + " ancestor missing.");
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
