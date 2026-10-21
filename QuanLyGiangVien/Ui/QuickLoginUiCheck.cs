using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

internal static class QuickLoginUiCheck
{
    internal static void Attach(CampusForm form, string connectionString)
    {
        AttachLoginScreen(form);
        string? output = Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        if (!new SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Quick-login UI checks require an isolated test database.");
        form.StartPosition = FormStartPosition.Manual; form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => {
            Directory.CreateDirectory(output); var checks = new List<string>();
            string? configuredFile = Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE");
            void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            async Task WaitFor(Func<bool> condition) { for (int n = 0; n < 200 && !condition(); n++) await Task.Delay(50); if (!condition()) throw new InvalidOperationException("Quick-login operation did not complete."); }
            async Task Shot(string name, int width, int height = 960) {
                form.ClientSize = new(AppTheme.S(width), AppTheme.S(height)); await Task.Delay(200);
                using var bitmap = UiCapture.Client(form); bitmap.Save(Path.Combine(output, name + ".png"));
            }
            try
            {
                var student = Find<AntdUI.Button>(form, "btnQuickStudentLogin");
                var training = Find<AntdUI.Button>(form, "btnQuickTrainingLogin");
                var password = Find<TextBox>(form, "txtLoginPassword");
                Expect(student.Visible && training.Visible && student.Enabled && training.Enabled, "Both quick-login buttons are visible for the configured database.");
                Expect(password.Text.Length == 0 && password.UseSystemPasswordChar, "The login screen never displays the stored sample password.");
                await Shot("01-quick-login-wide", 1440);
                await Shot("02-quick-login-compact", 1100, 720);
                var cardOnForm = form.RectangleToClient(student.Parent!.RectangleToScreen(student.Parent.ClientRectangle));
                Expect(form.ClientRectangle.Contains(cardOnForm), "The entire login card fits the actual visible compact client rectangle, including its bottom edge. " + form.ClientRectangle + " / " + cardOnForm);
                Expect(student.Bottom < student.Parent.ClientSize.Height && training.Right < training.Parent!.ClientSize.Width, "Both sample buttons remain inside the login card.");
                form.SelectLoginPortal(LoginPortal.Training); student.PerformClick();
                Expect(!training.Enabled, "A sample login disables other login actions immediately.");
                await WaitFor(() => form.ConnectedIdentity?.Role == "SINH_VIEN" && student.Enabled);
                Expect(form.ConnectedIdentity!.StudentId == "SV2024001", "The student button signs into the actual linked SQL student account.");
                Expect(form.SelectedModule == CampusModule.Registration && !form.NavigationModules.Contains(CampusModule.Academics), "Student quick-login opens registration with student-only navigation.");
                Expect(password.Text.Length == 0, "Successful quick-login clears the password field.");
                await Shot("03-quick-student", 1100);
                await form.LogoutAsync();
                Expect(form.ConnectedIdentity == null && password.Text.Length == 0 && student.Enabled, "Logout returns to reusable quick-login buttons with cleared credentials.");
                form.SelectLoginPortal(LoginPortal.Student); training.PerformClick(); student.PerformClick();
                await WaitFor(() => form.ConnectedIdentity?.Role == "DAO_TAO" && training.Enabled);
                Expect(form.NavigationModules.Contains(CampusModule.Academics), "The training button selects the training portal and retains SQL-authorized management navigation.");
                await Shot("04-quick-training", 1440);
                await form.LogoutAsync();
                Environment.SetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE", Path.Combine(output, "missing.dat"));
                await form.QuickLoginAsync(LoginPortal.Student);
                Expect(form.ConnectedIdentity == null && password.Text.Length == 0, "Missing quick-login credentials never create a session or bypass authentication.");
                Expect(Find<AntdUI.Button>(form, "btnPasswordLogin").Enabled, "Manual password login remains available if the local sample file is missing.");
                await Shot("05-manual-fallback", 1100, 720);
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, dpi = form.DeviceDpi, scope = "Native button events, actual SQL password login, offscreen captures; no physical-mouse E2E claim." }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.Message }));
                Environment.ExitCode = 1;
            }
            finally { Environment.SetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE", configuredFile); form.Close(); }
        };
    }
    // Read-only startup verification: never authenticates or opens a database connection.
    private static void AttachLoginScreen(CampusForm form)
    {
        string? output = Environment.GetEnvironmentVariable("CAMPUS_LOGIN_SCREEN_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => {
            Directory.CreateDirectory(output);
            var checks = new List<string>();
            void Expect(bool condition, string message) {
                if (!condition) throw new InvalidOperationException(message);
                checks.Add(message);
            }
            try
            {
                form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720));
                await Task.Delay(250);
                var student = Find<AntdUI.Button>(form, "btnQuickStudentLogin");
                var training = Find<AntdUI.Button>(form, "btnQuickTrainingLogin");
                bool available = Environment.GetEnvironmentVariable("CAMPUS_LOGIN_SCREEN_EXPECT_AVAILABLE") == "1";
                Expect(student.Visible && training.Visible, "Both sample buttons remain visible even without credentials.");
                Expect(student.Enabled == available && training.Enabled == available, "Buttons reflect credential availability.");
                Expect(Find<TextBox>(form, "txtLoginPassword").Text.Length == 0, "No sample password is displayed.");
                Expect(form.ConnectedIdentity == null, "Screen verification never signs in.");
                var card = form.RectangleToClient(student.Parent!.RectangleToScreen(student.Parent.ClientRectangle));
                Expect(form.ClientRectangle.Contains(card), "The entire login card fits the visible compact client area.");
                using var bitmap = UiCapture.Client(form);
                bitmap.Save(Path.Combine(output, "login.png"));
                File.WriteAllText(Path.Combine(output, "screen.json"), JsonSerializer.Serialize(new {
                    passed = true, checks, executable = Environment.ProcessPath, assembly = typeof(CampusForm).Assembly.Location,
                    available, dpi = form.DeviceDpi, scope = "Actual Program startup and rendered controls; no authentication or business writes."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "screen.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.Message }));
                Environment.ExitCode = 1;
            }
            finally { form.Close(); }
        };
    }
    private static T Find<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
}
