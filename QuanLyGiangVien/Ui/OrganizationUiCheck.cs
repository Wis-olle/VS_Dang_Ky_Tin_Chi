using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Catalogs;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Catalogs;
using QuanLyGiangVien.Features.Shell;
using QuanLyGiangVien.Features.Students;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in CRUD verification confined to one explicitly owned disposable showcase.</summary>
internal static class OrganizationUiCheck
{
    private const string FacultyCode = "UI22_FAC", MajorCode = "UI22_MAJOR", ClassCode = "UI22_CLASS";

    internal static void Attach(CampusForm form, string connection)
    {
        string? output = Environment.GetEnvironmentVariable("CAMPUS_ORGANIZATION_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        var target = new SqlConnectionStringBuilder(connection);
        if (!Regex.IsMatch(target.InitialCatalog, @"\ADangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}\z", RegexOptions.CultureInvariant)
            || target.InitialCatalog != Environment.GetEnvironmentVariable("CAMPUS_ORGANIZATION_DATABASE")
            || target.DataSource is not ("." or "localhost" or "(local)"))
            throw new InvalidOperationException("Organization checks require the exact local disposable showcase database.");
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 80, 0);
        bool started = false;
        form.Shown += async (_, _) =>
        {
            if (started) return;
            started = true;
            await Run(form, connection, output);
        };
    }

    private static async Task Run(CampusForm form, string connection, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var screenshots = new List<object>();
        var repository = new OrganizationRepository(connection);
        bool passed = false;
        string? failure = null;
        void Expect(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
        async Task Shot(string name, int width)
        {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(width > 1100 ? 920 : 720));
            form.PerformLayout(); await Task.Delay(350);
            if (form.CurrentModulePage is CatalogsPage catalog)
            {
                foreach (var section in catalog.Organizations.Sections.Where(s => s.Visible))
                    foreach (string action in new[] { "btnOrganizationSave", "btnOrganizationNew", "btnOrganizationStatus" })
                    {
                        var button = Find<Button>(section, action);
                        var bounds = form.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                        Expect(button.Parent!.ClientRectangle.Contains(button.Bounds) && form.ClientRectangle.Contains(bounds)
                            && TextRenderer.MeasureText(button.Text, button.Font).Width + AppTheme.S(8) <= button.Width,
                            $"{section.Kind} {action} stays fully visible with an uncut caption at width {width}.");
                    }
            }
            using var bitmap = UiCapture.Client(form);
            string file = name + ".png"; bitmap.Save(Path.Combine(output, file));
            screenshots.Add(new { file, width = form.ClientSize.Width, height = form.ClientSize.Height, dpi = form.DeviceDpi });
        }
        async Task<OrganizationControl> Section(OrganizationKind kind)
        {
            if (form.CurrentModulePage is not CatalogsPage) await form.OpenModuleAsync(CampusModule.Catalogs);
            var page = form.CurrentModulePage as CatalogsPage ?? throw new InvalidOperationException("Catalogs did not open.");
            page.OpenOrganizations();
            await Wait(() => !page.IsBusy && !page.Organizations.IsBusy && page.Organizations.Enabled);
            var section = page.Organizations.Sections.Single(s => s.Kind == kind);
            foreach (var item in page.Organizations.Sections) item.Confirm = _ => true;
            Activate(section); await Task.Delay(100);
            await Wait(() => !page.IsBusy && !page.Organizations.IsBusy && section.Enabled && section.Visible && section.DataGrid.DataSource != null);
            return section;
        }
        async Task Click(OrganizationControl section, string button)
        {
            var control = Find<Button>(section, button);
            Expect(control.Enabled && control.Visible, $"{section.Kind} action {button} is available.");
            control.PerformClick(); await Task.Delay(70);
            await Wait(() => !section.IsBusy && section.Enabled);
        }
        async Task CheckStored(OrganizationKind kind, string code, string name, bool active, string? parent = null)
        {
            using var table = await repository.ListAsync(kind, code);
            var row = table.AsEnumerable().Single(r => r.Field<string>("code") == code);
            Expect(row.Field<string>("name") == name && row.Field<bool>("active") == active
                && (parent == null || row.Field<string>("parent_code") == parent),
                $"Fresh SQL read preserves {kind} {code}, name, status and parent.");
        }
        async Task Create(OrganizationKind kind, string code, string name, string? parentCode)
        {
            var section = await Section(kind);
            await Click(section, "btnOrganizationNew");
            Find<TextBox>(section, "txtOrganizationCode").Text = code;
            Find<TextBox>(section, "txtOrganizationName").Text = name;
            if (parentCode != null) await Choose(Find<ComboBox>(section, "cboOrganizationParent"), parentCode);
            if (kind == OrganizationKind.Class)
            {
                Find<TextBox>(section, "txtOrganizationCohort").Text = "2026";
                Find<TextBox>(section, "txtOrganizationYears").Text = "2026-2030";
            }
            else Find<TextBox>(section, "txtOrganizationDescription").Text = "SYNTHETIC DEMO - native organization CRUD fixture";
            await Click(section, "btnOrganizationSave");
            await CheckStored(kind, code, name, true, parentCode);
            Expect(Find<TextBox>(section, "txtOrganizationCode").ReadOnly && !section.HasUnsavedChanges,
                $"Saved {kind} is selected with an immutable code and a clean editor.");
        }
        async Task EditAndStatus(OrganizationKind kind, string code, string name, string? parentCode)
        {
            var section = await Section(kind); SelectRow(section.DataGrid, code);
            Find<TextBox>(section, "txtOrganizationName").Text = name;
            await Click(section, "btnOrganizationSave");
            await CheckStored(kind, code, name, true, parentCode);
            await Click(section, "btnOrganizationStatus");
            await CheckStored(kind, code, name, false, parentCode);
            Expect(!Find<CheckBox>(section, "chkOrganizationActive").Checked && !section.HasUnsavedChanges,
                $"Stopping {kind} reloads its stored inactive state without a dirty draft.");
            await Click(section, "btnOrganizationStatus");
            await CheckStored(kind, code, name, true, parentCode);
        }
        async Task StudentChoices(bool hasChildren)
        {
            await form.OpenModuleAsync(CampusModule.Students);
            var page = form.CurrentModulePage as StudentsPage ?? throw new InvalidOperationException("Students did not open.");
            var combos = Descendants(page).OfType<ComboBox>().ToArray();
            var faculty = combos.Single(c => c.Items.Cast<object>().Any(item => ItemCode(item) == FacultyCode));
            string? original = ItemCode(faculty.SelectedItem);
            try
            {
                await Choose(faculty, FacultyCode);
                if (hasChildren)
                {
                    var major = combos.Single(c => c.Items.Cast<object>().Any(item => ItemCode(item) == MajorCode));
                    await Choose(major, MajorCode);
                    var studentClass = combos.Single(c => c.Items.Cast<object>().Any(item => ItemCode(item) == ClassCode));
                    await Choose(studentClass, ClassCode);
                    Expect(ItemCode(faculty.SelectedItem) == FacultyCode && ItemCode(major.SelectedItem) == MajorCode
                        && ItemCode(studentClass.SelectedItem) == ClassCode && major.Enabled && studentClass.Enabled,
                        "Student native dropdown events cascade through the newly saved faculty, major and class.");
                    await Shot("05-student-cascade-wide", 1600); await Shot("06-student-cascade-compact", 1100);
                }
                else
                {
                    Expect(combos.Count(c => c.Items.Count == 0 && !c.Enabled) == 2,
                        "A new faculty with no children appears in Students and leaves its two child dropdowns empty and disabled.");
                    await Shot("01-faculty-without-children", 1100);
                }
            }
            finally
            {
                // Restore the original user-visible choice instead of bypassing the student's discard confirmation.
                if (original != null && !page.IsDisposed) await Choose(faculty, original);
            }
            Expect(!page.HasUnsavedChanges, "Restoring the initial student dropdown choice leaves no unsaved student edit.");
        }
        try
        {
            Expect(form.ConnectedIdentity == null, "The isolated run starts on login without automatic authentication.");
            Expect(QuickLoginCredentials.Load(connection, Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE")) != null,
                "The disposable database has its own readable DPAPI sample configuration.");
            var login = Find<AntdUI.Button>(form, "btnPasswordLogin");
            Find<AntdUI.Button>(form, "btnQuickTrainingLogin").PerformClick();
            await Wait(() => login.Enabled && form.ConnectedIdentity != null);
            Expect(form.ConnectedIdentity?.Role == "DAO_TAO" && form.SelectedModule == CampusModule.Lecturers,
                "The actual sample training button authenticates through SQL and opens Lecturers.");
            await form.OpenModuleAsync(CampusModule.Students);
            Find<Button>(form.CurrentModulePage!, "btnManageOrganizations").PerformClick();
            await Wait(() => form.SelectedModule == CampusModule.Catalogs && form.CurrentModulePage is CatalogsPage p
                && p.Organizations.Visible && !p.Organizations.IsBusy && p.Organizations.Sections.All(s => s.DataGrid.DataSource != null));
            Expect(form.CurrentModulePage is CatalogsPage, "The Students heading button routes to the organization catalogs.");
            foreach (var section in ((CatalogsPage)form.CurrentModulePage!).Organizations.Sections)
                section.Confirm = _ => true;
            await Create(OrganizationKind.Faculty, FacultyCode, "Khoa thử nghiệm trực quan", null);
            await StudentChoices(false);
            await Create(OrganizationKind.Major, MajorCode, "Ngành thử nghiệm trực quan", FacultyCode);
            await Create(OrganizationKind.Class, ClassCode, "Lớp thử nghiệm 2026", MajorCode);
            await EditAndStatus(OrganizationKind.Class, ClassCode, "Lớp thử nghiệm đã sửa", MajorCode);
            await EditAndStatus(OrganizationKind.Major, MajorCode, "Ngành thử nghiệm đã sửa", FacultyCode);
            await EditAndStatus(OrganizationKind.Faculty, FacultyCode, "Khoa thử nghiệm đã sửa", null);
            using (var faculties = await repository.ListAsync(OrganizationKind.Faculty))
                Expect(new[] { "CNTT", "KT", "NN", "KTH", FacultyCode }.All(code => faculties.AsEnumerable().Any(r => r.Field<string>("code") == code)),
                    "The independent faculty list includes all four baseline faculties and the newly created faculty.");
            var facultySection = await Section(OrganizationKind.Faculty); SelectRow(facultySection.DataGrid, FacultyCode);
            await Shot("02-organizations-faculty-wide", 1600); await Shot("03-organizations-faculty-compact", 1100);
            var classSection = await Section(OrganizationKind.Class); SelectRow(classSection.DataGrid, ClassCode);
            Expect(Find<TextBox>(classSection, "txtOrganizationCohort").Text == "2026"
                && Find<TextBox>(classSection, "txtOrganizationYears").Text == "2026-2030",
                "The class editor reloads its saved cohort and academic years.");
            await Shot("04-organizations-class-compact", 1100);
            await StudentChoices(true);
            await form.LogoutAsync();
            Expect(form.ConnectedIdentity == null && login.Visible, "Logout returns only this checked form to its login screen.");
            passed = true;
        }
        catch (Exception error) { failure = error.ToString(); Environment.ExitCode = 1; }
        finally
        {
            if (failure != null) File.WriteAllText(Path.Combine(output, "error.txt"), failure);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
            {
                passed, checks, screenshots, error = failure,
                scope = "Original native button/popup events and SQL persistence on an explicitly named disposable Showcase UUID; offscreen current-DPI captures, no physical-input or all-DPI claim. Fixture entities are retained for the caller to drop with its owned database."
            }, new JsonSerializerOptions { WriteIndented = true }));
            form.Close();
        }
    }

    private static async Task Choose(ComboBox source, string code)
    {
        int index = source.Items.Cast<object>().Select((item, i) => (item, i)).Single(pair => ItemCode(pair.item) == code).i;
        var face = source.Parent?.Controls.OfType<AnimatedSelect>().SingleOrDefault()
            ?? throw new InvalidOperationException("The original organization choice has no visible select facade.");
        face.Focus(); face.OpenDropDown(); await Task.Delay(250);
        var popup = face.PopupForCheck ?? throw new InvalidOperationException("The actual select popup did not open.");
        popup.Highlight(index); popup.CommitHighlighted(); await Task.Delay(60);
        if (source.SelectedIndex != index) throw new InvalidOperationException("The select popup did not commit the requested source item.");
    }
    private static string? ItemCode(object? item) => item?.GetType().GetProperty("Code")?.GetValue(item) as string;
    private static void SelectRow(DataGridView grid, string code)
    {
        var row = grid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["code"].Value) == code);
        grid.ClearSelection(); grid.CurrentCell = row.Cells[0]; row.Selected = true;
    }
    private static void Activate(Control child)
    {
        if (child.Parent is not { } parent) return;
        Activate(parent);
        if (child is TabPage page && parent is TabControl tabs) tabs.SelectedTab = page;
    }
    private static T Find<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static async Task Wait(Func<bool> predicate)
    {
        var until = DateTime.UtcNow.AddSeconds(45);
        while (!predicate()) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Organization native verification timed out."); await Task.Delay(40); }
    }
}
