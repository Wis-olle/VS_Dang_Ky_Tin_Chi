using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Shell;
using QuanLyGiangVien.Features.Academics;

namespace QuanLyGiangVien.Ui;

internal static class StartupUiCheck
{
    internal static void Attach(CampusForm form, string connection)
    {
        var selectedOutput = Environment.GetEnvironmentVariable("CAMPUS_SELECTED_SHOWCASE_VERIFY");
        bool selectedShowcase = !string.IsNullOrWhiteSpace(selectedOutput);
        var output = selectedShowcase ? selectedOutput : Environment.GetEnvironmentVariable("CAMPUS_STARTUP_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        if (selectedShowcase && (!Environment.GetCommandLineArgs().Contains("--showcase")
            || new SqlConnectionStringBuilder(connection).InitialCatalog != Environment.GetEnvironmentVariable("CAMPUS_SELECTED_SHOWCASE_DATABASE")))
            throw new InvalidOperationException("Selected showcase checks require the exact explicitly named showcase database.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(new SqlConnectionStringBuilder(connection).InitialCatalog,
            @"^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$")) throw new InvalidOperationException("Startup checks require their own synthetic database.");
        form.StartPosition = FormStartPosition.Manual; form.Location = new(SystemInformation.VirtualScreen.Right + 60, 0);
        bool running = false;
        form.Shown += async (_, _) =>
        {
            if (running) return; running = true;
            Directory.CreateDirectory(output); var checks = new List<string>();
            void Expect(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
            async Task Shot(string name)
            {
                form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720)); await Task.Delay(1800);
                using var bitmap = UiCapture.Client(form); bitmap.Save(Path.Combine(output, name + ".png"));
            }
            try
            {
                var login = Find<AntdUI.Button>(form, "btnPasswordLogin");
                var password = Find<TextBox>(form, "txtLoginPassword");
                await Task.Delay(150);
                var until = DateTime.UtcNow.AddSeconds(40);
                while (!login.Enabled) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Startup login timed out."); await Task.Delay(40); }
                bool success = selectedShowcase || Environment.GetEnvironmentVariable("CAMPUS_STARTUP_EXPECT") == "success";
                Expect(form.ConnectedIdentity == null && form.SelectedModule == null && login.Visible,
                    "Start/F5 opens the login screen without automatically authenticating.");
                Expect(password.Text.Length == 0 && password.UseSystemPasswordChar, "Startup never leaves a sample password in the password field.");
                await Shot("00-start-login");
                var training = Find<AntdUI.Button>(form, "btnQuickTrainingLogin");
                async Task WaitForLogin()
                {
                    var end = DateTime.UtcNow.AddSeconds(40);
                    while (!login.Enabled) { if (DateTime.UtcNow > end) throw new InvalidOperationException("Sample login timed out."); await Task.Delay(40); }
                }
                if (success)
                {
                    Expect(training.Enabled, "The configured training sample button is available on the login screen.");
                    training.PerformClick(); await WaitForLogin();
                    Expect(form.ConnectedIdentity?.Role == "DAO_TAO", "Clicking the training sample button authenticates through SQL.");
                    Expect(form.SelectedModule == CampusModule.Lecturers && form.CurrentModulePage is MainForm,
                        "Training sample login opens the actual lecturer management page first.");
                    Expect(password.Text.Length == 0, "Successful sample login clears the password field.");
                    await Shot("01-start-lecturers");
                    if (selectedShowcase)
                    {
                        await form.OpenModuleAsync(CampusModule.Academics);
                        var academic = form.CurrentModulePage as AcademicPage
                            ?? throw new InvalidOperationException("Academic page did not open.");
                        foreach (var (kind, minimum) in new[] { ("PROGRAM", 3), ("GROUP", 6), ("RESULT", 50) })
                        {
                            var section = academic.Sections.Single(s => s.Kind == kind);
                            Expect(section.DataGrid.Rows.Count >= minimum, $"Selected demo loads at least {minimum} visible {kind} rows.");
                        }
                        await Shot("02-academic-programs");
                        Expect(!academic.HasUnsavedChanges, "Inspecting the populated academic page does not create unsaved edits.");
                    }
                    await form.LogoutAsync(); form.Hide(); form.Show(); await Task.Delay(300);
                    Expect(form.ConnectedIdentity == null && login.Visible && login.Enabled, "Logout stays on the login screen without automatically signing in again.");
                    Find<AntdUI.Button>(form, "btnQuickStudentLogin").PerformClick(); await WaitForLogin();
                    Expect(form.ConnectedIdentity?.Role == "SINH_VIEN" && form.SelectedModule == CampusModule.Registration,
                        "The student sample button still opens student registration.");
                    await form.LogoutAsync();
                }
                else
                {
                    var status = QuickLoginCredentials.Load(connection, Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE"));
                    Expect(training.Enabled == (status != null), "The sample button is disabled only when its local configuration is unavailable.");
                    if (training.Enabled) { training.PerformClick(); await WaitForLogin(); }
                    Expect(form.ConnectedIdentity == null && login.Visible && login.Enabled, "Missing or rejected sample credentials fall back to usable manual login.");
                    Expect(password.Text.Length == 0, "Unavailable or rejected sample login leaves no password in the field.");
                    await Shot("02-fallback-login");
                }
                var valid = QuickLoginCredentials.Load(connection, Environment.GetEnvironmentVariable(selectedShowcase
                    ? "CAMPUS_QUICK_LOGIN_FILE" : "CAMPUS_STARTUP_VALID_FILE"))
                    ?.For(LoginPortal.Training) ?? throw new InvalidOperationException("The fixture's valid login file is unavailable.");
                form.SelectLoginPortal(LoginPortal.Training);
                Find<TextBox>(form, "txtLoginUsername").Text = valid.Username; password.Text = valid.Password;
                await form.AuthenticateAsync();
                Expect(form.ConnectedIdentity?.Role == "DAO_TAO" && form.SelectedModule == CampusModule.Catalogs,
                    "Manual training login retains its original default page without leaking the sample button's destination.");
                await form.LogoutAsync();
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks,
                    scope = "Actual Program startup, DPAPI and SQL authentication on a synthetic fixture; offscreen 96-DPI controls, no physical-input claim." }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.Message }));
                Environment.ExitCode = 1;
            }
            finally { form.Close(); }
        };
    }
    private static T Find<T>(Control root, string name) where T : Control => root.Controls.Find(name, true).OfType<T>().Single();
}
