using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Academics;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Registration.Visuals;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

// Opt-in native verification against an explicitly named, synthetic showcase only.
internal static class FullDemoUiCheck
{
    internal static void Attach(CampusForm form, string connection)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_FULL_DEMO_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        var database = new SqlConnectionStringBuilder(connection).InitialCatalog;
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, @"^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$")
            || database != Environment.GetEnvironmentVariable("CAMPUS_FULL_DEMO_DATABASE"))
            throw new InvalidOperationException("Full demo verification requires the exact synthetic database.");
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 60, 0);
        form.Shown += async (_, _) => await Run(form, output);
    }

    private static async Task Run(CampusForm form, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var selectChecks = new List<string>();
        var pages = new List<object>();
        var sequence = 0;
        void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
        void SelectExpect(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); selectChecks.Add(message); }
        object[] VerifySelects(string scene)
        {
            var sources = Descendants(form).OfType<ComboBox>().Where(c => c.Visible && c.Name != "cboRegistrationModule").ToArray();
            return sources.Select((combo, index) =>
            {
                string label = $"{scene}: {combo.Name}/{combo.AccessibleName} #{index + 1}";
                SelectExpect(combo.Parent is SoftInputSurface, label + " retains its native model inside SoftInputSurface.");
                var faces = combo.Parent!.Controls.OfType<AnimatedSelect>().ToArray();
                SelectExpect(faces.Length == 1, label + " has exactly one animated select facade.");
                var face = faces.Single();
                var captions = combo.Items.Cast<object>().Select(item => combo.GetItemText(item) ?? "").ToArray();
                SelectExpect(face.Items.SequenceEqual(captions) && face.SelectedIndex == combo.SelectedIndex,
                    label + " mirrors every formatted native item and its selection.");
                string selectedCaption = combo.SelectedIndex < 0 ? "" : captions[combo.SelectedIndex];
                SelectExpect(face.Text == selectedCaption && face.Enabled == combo.Enabled && face.Visible,
                    label + " mirrors the current caption, enabled state and visibility.");
                SelectExpect(!combo.Bounds.IntersectsWith(face.Bounds) && combo.Parent.ClientRectangle.Contains(face.Bounds),
                    label + " keeps native HWND bounds outside the complete animated facade.");
                return (object)new { combo.Name, combo.AccessibleName, items = captions.Length, selectedIndex = combo.SelectedIndex,
                    selectedCaption, enabled = combo.Enabled, facade = face.Name };
            }).ToArray();
        }
        async Task Shot(string name, Control? page = null)
        {
            await Task.Delay(page is MainForm ? 800 : 150);
            var grids = Descendants(page ?? form).OfType<DataGridView>().ToArray();
            var selects = VerifySelects(name);
            pages.Add(new { name, grids = grids.Select(g => new { g.Name, g.AccessibleName, rows = g.Rows.Count }).ToArray(), selectCount = selects.Length, selects });
            using var bitmap = UiCapture.Client(form);
            bitmap.Save(Path.Combine(output, $"{++sequence:00}-{name}.png"));
        }
        async Task SelectScenes(Control page, string prefix)
        {
            var original = Descendants(page).OfType<TabControl>().Select(t => (Tabs: t, Selected: t.SelectedTab)).ToArray();
            var leaves = Descendants(page).OfType<TabPage>().Where(t => Descendants(t).OfType<ComboBox>().Any()
                && !Descendants(t).OfType<TabPage>().Any(child => Descendants(child).OfType<ComboBox>().Any())).ToArray();
            for (int i = 0; i < leaves.Length; i++)
            {
                Activate(leaves[i]);
                await Shot(prefix + "-selects-" + (i + 1), leaves[i]);
            }
            foreach (var item in original) if (item.Selected != null) item.Tabs.SelectedTab = item.Selected;
        }
        async Task StudentCascade(Control page)
        {
            var choices = Descendants(page).OfType<ComboBox>()
                .Where(c => c.Items.Count > 0 && c.Items[0] is not string).ToArray();
            if (choices.Length != 3 || choices[0].Items.Count < 2)
            {
                selectChecks.Add("SKIPPED optional student cascade interaction: the loaded directory does not expose three populated dependent lists and two faculty choices; scene parity is still checked.");
                return;
            }
            var saved = choices.Select(c => c.SelectedIndex).ToArray();
            var visibleRows = Descendants(page).OfType<DataGridView>().Sum(g => g.Rows.Count);
            var before = choices[1].Items.Cast<object>().Select(choices[1].GetItemText).ToArray();
            var facultyFace = choices[0].Parent!.Controls.OfType<AnimatedSelect>().Single();
            int target = saved[0] == 0 ? choices[0].Items.Count - 1 : 0;
            int events = 0;
            void Changed(object? sender, EventArgs e) => events++;
            choices[0].SelectedIndexChanged += Changed;
            try
            {
                facultyFace.HandleSelectionKey(target == 0 ? Keys.Home : Keys.End);
                await Task.Delay(80);
                var after = choices[1].Items.Cast<object>().Select(choices[1].GetItemText).ToArray();
                SelectExpect(events == 1 && choices[0].SelectedIndex == target && !before.SequenceEqual(after),
                    "Student faculty selection through the animated facade runs the actual native cascade and replaces major choices.");
                SelectExpect(choices.Skip(1).All(c => c.SelectedIndex == (c.Items.Count == 0 ? -1 : 0)),
                    "Student major/class dependent lists retain valid selections after the faculty cascade.");
                SelectExpect(Descendants(page).OfType<DataGridView>().Sum(g => g.Rows.Count) == visibleRows,
                    "Student editor cascades preserve the populated student list without saving a record.");
                await Shot("students-faculty-cascade", page);
            }
            finally
            {
                choices[0].SelectedIndexChanged -= Changed;
                for (int i = 0; i < choices.Length; i++) choices[i].SelectedIndex = saved[i];
            }
        }
        async Task Login(bool student)
        {
            await form.QuickLoginAsync(student ? LoginPortal.Student : LoginPortal.Training);
            if (form.ConnectedIdentity == null)
                throw new InvalidOperationException("Demo login failed: " + string.Join(" | ", Descendants(form).OfType<AntdUI.Label>().Select(l => l.Text)));
            Expect(form.ConnectedIdentity?.Role == (student ? "SINH_VIEN" : "DAO_TAO"), $"Real SQL quick-login authenticates {(student ? "student" : "training")} role.");
        }
        async Task Workspace(RegistrationWorkspace host, bool student)
        {
            host.ConfirmAction = _ => true;
            await host.ReloadForSemesterAsync("HK1-2627");
            var tabs = Find<TabControl>(host, "registrationPages");
            foreach (var tab in tabs.TabPages.Cast<TabPage>().ToArray())
            {
                tabs.SelectedTab = tab;
                await Wait(() => !host.IsBusy);
                var page = (ModulePage)tab.Controls[0];
                await host.RefreshCurrentAsync();
                if (page is KetQuaPage)
                {
                    Find<TextBox>(page, "txtMaSv").Text = "SV2024001";
                    await page.RefreshDataAsync();
                }
                if (student && page is RegistrationCartPage)
                {
                    using var precheck = await host.Workflow.PrecheckAsync("HK1-2627", ["LHP0021", "LHP0022"], []);
                    Expect(!precheck.Rows.Cast<System.Data.DataRow>().Any(r => Convert.ToBoolean(r["blocking"])),
                        "The seeded two-class student cart passes real eligibility, capacity and timetable checks.");
                }
                if (page is SchedulePlannerPage)
                {
                    Find<TextBox>(page, "txtPlannerCourses").Text = "IT103";
                    Find<Button>(page, "btnPlannerGenerate").PerformClick();
                    await Wait(() => Find<Button>(page, "btnPlannerGenerate").Enabled);
                    Expect(Descendants(page).OfType<PlannerWeekPreview>().Single().MeetingCardCount > 0, "Student planner generates a real dated option from the seeded classes.");
                }
                if (page is DataImportPage import)
                {
                    var importRoot = Environment.GetEnvironmentVariable("CAMPUS_FULL_DEMO_IMPORT_ROOT")
                        ?? throw new InvalidOperationException("The full-demo import samples were not configured.");
                    import.LoadDocument(File.ReadAllText(Path.Combine(importRoot, "import-valid.json")));
                    Find<Button>(page, "btnImportPreview").PerformClick();
                    await Wait(() => !host.IsBusy);
                    var grid = Descendants(page).OfType<DataGridView>().Single();
                    Expect(grid.Rows.Count == 3 && grid.Rows.Cast<DataGridViewRow>().All(r => Convert.ToBoolean(r.Cells["valid"].Value)),
                        "The supplied valid import file previews all three dependent rows without committing them.");
                    await Shot("training-import-valid", page);
                    import.LoadDocument(File.ReadAllText(Path.Combine(importRoot, "import-invalid.json")));
                    Find<Button>(page, "btnImportPreview").PerformClick();
                    await Wait(() => !host.IsBusy);
                    Expect(grid.Rows.Count == 2 && grid.Rows.Cast<DataGridViewRow>().Any(r => !Convert.ToBoolean(r.Cells["valid"].Value))
                        && !Find<Button>(page, "btnImportCommit").Enabled, "The supplied invalid import file identifies its bad row and prevents commit.");
                }
                else
                {
                    Expect(Descendants(page).OfType<DataGridView>().Sum(g => g.Rows.Count) > 0,
                        $"{(student ? "SV" : "PDT")} {page.GetType().Name} loads populated SQL data.");
                }
                await Shot((student ? "student-" : "training-") + page.GetType().Name, page);
                await SelectScenes(page, (student ? "student-" : "training-") + page.GetType().Name);
                if (page is DataImportPage)
                    Descendants(page).OfType<Button>().Single(b => b.Text == "Bỏ tệp").PerformClick();
            }
        }
        try
        {
            Expect(form.ConnectedIdentity == null && form.CurrentModulePage == null, "Startup remains at the login page before any sample button is pressed.");
            await Shot("login");
            await Login(false);
            Expect(form.SelectedModule == CampusModule.Lecturers, "The sample training button opens Lecturers first.");
            foreach (var module in CampusNavigation.ForRole("DAO_TAO"))
            {
                await form.OpenModuleAsync(module);
                Expect(form.SelectedModule == module && form.CurrentModulePage != null, $"Training module {module} opens with current data.");
                if (form.CurrentModulePage is AcademicPage academic)
                {
                    foreach (var section in academic.Sections)
                    {
                        Activate(section);
                        Expect(section.DataGrid.Rows.Count > 0, $"Academic entity {section.Kind} has visible records.");
                        await Shot("academic-" + section.Kind, section);
                        var record = section.DataGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r =>
                            r.DataBoundItem is System.Data.DataRowView view && view.Row.Table.Columns.Contains("published")
                            && Convert.ToBoolean(view.Row["published"])) ?? section.DataGrid.Rows[0];
                        section.DataGrid.CurrentCell = record.Cells[0]; record.Selected = true;
                        await Shot("academic-" + section.Kind + "-selected", section);
                    }
                    await SelectScenes(academic, "academic");
                    Expect(!academic.HasUnsavedChanges, "Browsing academic seed records does not create unsaved edits.");
                }
                else if (form.CurrentModulePage is RegistrationWorkspace training) await Workspace(training, false);
                else
                {
                    await Shot("module-" + module, form.CurrentModulePage);
                    await SelectScenes(form.CurrentModulePage!, "module-" + module);
                    if (form.CurrentModulePage is QuanLyGiangVien.Features.Students.StudentsPage students) await StudentCascade(students);
                }
            }
            await form.LogoutAsync(); await Login(true);
            await Workspace((RegistrationWorkspace)form.CurrentModulePage!, true);
            await form.OpenModuleAsync(CampusModule.Timetable);
            Expect(form.SelectedModule == CampusModule.Timetable, "Student personal timetable opens after registration pages.");
            await Shot("student-timetable", form.CurrentModulePage);
            await form.LogoutAsync();
            var accountFile = Path.GetFullPath(Path.Combine(output, "..", "test-accounts.private.txt"));
            var account = File.ReadAllLines(accountFile).Single(line => line.StartsWith("sv2024005 | ", StringComparison.Ordinal)).Split('|');
            form.SelectLoginPortal(LoginPortal.Student);
            Find<TextBox>(form, "txtLoginUsername").Text = account[0].Trim();
            Find<TextBox>(form, "txtLoginPassword").Text = account[1].Trim();
            await form.AuthenticateAsync();
            Expect(form.ConnectedIdentity?.StudentId == "SV2024005", "The separate warning/hold scenario account authenticates with its own private credential.");
            using (var holdChecks = await ((RegistrationWorkspace)form.CurrentModulePage!).Academics.PrecheckAsync(["LHP0021"]))
                Expect(holdChecks.Rows.Cast<System.Data.DataRow>().Any(r => Convert.ToBoolean(r["blocking"]) && Convert.ToString(r["code"]) == "HOLD"),
                    "The student with an active registration hold is blocked by the actual academic evaluator.");
            await Shot("student-hold-scenario", form.CurrentModulePage);
            await form.LogoutAsync();
            Expect(form.ConnectedIdentity == null, "Logout returns to login.");
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, selectChecks, pages, scope = "Native offscreen WinForms at the current DPI; SQL password authentication, bound row counts and animated/native select parity by scene. No physical-input or all-DPI claim; notifications may deliver their SQL outbox during reads." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "error.txt"), error.ToString());
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, selectChecks, pages }));
        }
        finally
        {
            if (form.CurrentModulePage is RegistrationWorkspace host) host.ConfirmAction = _ => true;
            form.Close();
        }
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
        while (!predicate()) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Full demo native check timed out."); await Task.Delay(40); }
    }
}
