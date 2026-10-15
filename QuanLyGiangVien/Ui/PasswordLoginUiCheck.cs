using System.Text.Json;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

internal static class PasswordLoginUiCheck
{
    internal static void Attach(CampusForm form)
    {
        var output = Environment.GetEnvironmentVariable("PASSWORD_LOGIN_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => {
            Directory.CreateDirectory(output);
            var checks = new List<string>();
            void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            async Task Shot(string name) { await Task.Delay(500); using var bitmap = UiCapture.Client(form); bitmap.Save(Path.Combine(output, name + ".png")); }
            try
            {
                var user = Find<TextBox>(form, "txtLoginUsername"); var password = Find<TextBox>(form, "txtLoginPassword");
                var studentPassword = Environment.GetEnvironmentVariable("DEMO_STUDENT_PASSWORD") ?? throw new InvalidOperationException("Missing test credential");
                var trainingPassword = Environment.GetEnvironmentVariable("DEMO_TRAINING_PASSWORD") ?? throw new InvalidOperationException("Missing test credential");
                Expect(form.ConnectedIdentity == null, "Both portals start at the login screen");
                form.SelectLoginPortal(LoginPortal.Student); await Shot("01-student-login");
                user.Text = "sv2024001"; password.Text = "intentionally-wrong";
                await form.AuthenticateAsync();
                Expect(form.ConnectedIdentity == null && password.Text.Length == 0, "Wrong passwords are rejected and cleared");
                form.SelectLoginPortal(LoginPortal.Training); user.Text = "sv2024001"; password.Text = studentPassword;
                await form.AuthenticateAsync();
                Expect(form.ConnectedIdentity == null, "A student cannot sign into the training portal");
                form.SelectLoginPortal(LoginPortal.Student); password.Text = studentPassword;
                await form.AuthenticateAsync();
                Expect(form.ConnectedIdentity?.Role == "SINH_VIEN" && form.ConnectedIdentity.StudentId == "SV2024001", "Student login resolves the real student identity in SQL");
                Expect(form.NavigationModules.SequenceEqual(new[] { CampusModule.Registration, CampusModule.Timetable }), "Student navigation has no management modules");
                Expect(Find<DataGridView>(form, "dgvLopMo").RowCount > 0, "Registration classes load on first login without pressing Refresh");
                await Shot("02-student-portal");
                await form.OpenModuleAsync(CampusModule.Timetable);
                Expect(form.SelectedModule == CampusModule.Timetable, "Authenticated student can load the timetable from the shared database");
                await Shot("03-student-timetable");
                await form.LogoutAsync();
                Expect(form.ConnectedIdentity == null && form.CurrentPage == null && user.Text.Length == 0 && password.Text.Length == 0,
                    "Logout clears identity, pages and credential fields");
                form.SelectLoginPortal(LoginPortal.Student); user.Text = "daotao"; password.Text = trainingPassword;
                await form.AuthenticateAsync();
                Expect(form.ConnectedIdentity == null, "A training account cannot sign into the student portal");
                form.SelectLoginPortal(LoginPortal.Training); user.Text = "daotao"; password.Text = trainingPassword;
                await form.AuthenticateAsync();
                Expect(form.ConnectedIdentity?.Role == "DAO_TAO", "Training login receives DAO_TAO identity");
                await Shot("04-training-portal");
                await form.OpenModuleAsync(CampusModule.Students);
                Expect(form.SelectedModule == CampusModule.Students, "Training portal can open student management");
                await Shot("05-students");
                form.ClientSize = new(AppTheme.S(1100), AppTheme.S(820)); await Shot("06-compact-students");
                await form.LogoutAsync();
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.Message }));
                Environment.ExitCode = 1;
            }
            finally { form.Close(); }
        };
    }
    private static T Find<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
}
