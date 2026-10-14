using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Features.Shell;
using QuanLyGiangVien.Features.StudentTimetable;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Domain.Shell;

namespace QuanLyGiangVien.Ui;

internal static class StudentPortalUiCheck
{
    internal static void Attach(CampusForm form, string connectionString)
    {
        var output = Environment.GetEnvironmentVariable("STUDENT_PORTAL_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, SystemInformation.VirtualScreen.Top);
        form.Shown += async (_, _) => {
            Directory.CreateDirectory(output);
            var checks = new List<string>();
            void Expect(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(message);
                checks.Add(message);
            }
            async Task Shot(string name)
            {
                await Task.Delay(1100);
                using var bitmap = UiCapture.Client(form);
                bitmap.Save(Path.Combine(output, name + ".png"));
            }
            try
            {
                if (!new SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
                    throw new InvalidOperationException("A disposable SQL database is required");
                if (Environment.GetEnvironmentVariable("STUDENT_PORTAL_EXPECT_TERM_FAILURE") == "1")
                {
                    await form.ConnectAsync();
                    Expect(form.ConnectedIdentity == null && form.CurrentModulePage == null,
                        "A failed semester query does not display a timetable from a different default");
                    Expect(Descendants(form).OfType<AntdUI.Label>().Any(label => label.Text?.Contains("Chưa xác định được học kỳ từ SQL") == true),
                        "Semester query failures are visible on the connection page");
                    File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }));
                    return;
                }
                if (Environment.GetCommandLineArgs().Contains("--student-portal"))
                {
                    for (int retry = 0; retry < 200 && form.CurrentModulePage == null; retry++) await Task.Delay(50);
                    Expect(form.CurrentModulePage != null, "Student profile opens its portal automatically");
                }
                else await form.ConnectAsync();
                if (form.CurrentPage?.GetType().Name != "StudentPortalPage")
                    throw new InvalidOperationException("Both student tabs must share one persistent student workspace");
                var portal = (StudentPortalPage)form.CurrentPage;
                Expect(form.ConnectedIdentity?.StudentId == "SV2024001", "Tabs use the actual SQL student identity");
                Expect(form.NavigationModules.SequenceEqual(new[] { CampusModule.Registration, CampusModule.Timetable }), "Student form has exactly two tabs");
                var registration = portal.Registration;
                if (form.SelectedModule == CampusModule.Timetable)
                {
                    Expect(form.CurrentModulePage is StudentTimetableForm, "Timetable launch profile starts on the timetable tab");
                    Expect(portal.Timetable!.Snapshot.Semester.Id == "HK1-2627", "Timetable entrypoint selects the current semester before switching tabs");
                    await form.OpenModuleAsync(CampusModule.Registration);
                }
                Expect(form.CurrentModulePage == registration, "Registration is the first tab");
                Expect(registration.MaHocKy == "HK1-2627", "Both launch profiles select the current semester before any manual selection");
                var keyword = Descendants(registration).OfType<TextBox>().Single(item => item.Name == "txtTuKhoa");
                keyword.Text = "IT";
                await Shot("01-registration");
                await form.OpenModuleAsync(CampusModule.Timetable);
                Expect(form.CurrentPage == portal, "Changing tabs retains the same workspace");
                var timetable = portal.Timetable!;
                Expect(timetable.Snapshot.Classes.Length == 4 && timetable.Snapshot.TotalCredits == 12, "Timetable reads the same student's existing registrations");
                Expect(!timetable.HeaderSurface.Visible && !timetable.TopLevel, "Timetable shares the student form caption and tabs");
                Expect(timetable.Bounds == portal.ClientRectangle, "Timetable fills the shared viewport");
                timetable.ChangeWeek(1);
                var selectedWeek = timetable.Snapshot.WeekStart;
                timetable.ScrollPage(100);
                var selectedScroll = timetable.CaptureView().Scroll;
                await form.OpenModuleAsync(CampusModule.Registration);
                Expect(portal.Registration == registration && !registration.IsDisposed, "Registration controls survive tab changes");
                Expect(keyword.Text == "IT", "Registration search text survives tab changes");
                await form.OpenModuleAsync(CampusModule.Timetable);
                Expect(portal.Timetable!.Snapshot.WeekStart == selectedWeek, "Switching tabs preserves the selected week");
                Expect(portal.Timetable.CaptureView().Scroll == selectedScroll, "Switching tabs preserves the timetable scroll position");
                portal.Timetable.ScrollPage(0);
                await Shot("02-timetable");

                // Select a different semester through the timetable's real ComboBox event.
                portal.Timetable.SemesterPicker.SelectedIndex = 1;
                var otherSemester = portal.Timetable.Snapshot.Semester.Id;
                await form.OpenModuleAsync(CampusModule.Registration);
                Expect(registration.MaHocKy == otherSemester, "Timetable semester transfers to registration");
                Expect(registration.ChoDangKy && registration.ChoHuy, "Fixture deadlines are validated by the SQL server");
                using var available = await registration.Service.TraCuuAsync(otherSemester, "", false);
                var classCode = available.Rows.Cast<System.Data.DataRow>()
                    .Select(row => Convert.ToString(row["ma_lhp"])!).Single(code => code == "LHP0009");
                long registrationId = 0;
                await registration.ExecuteAsync(async () => {
                    using var created = await registration.Service.DangKyAsync(classCode);
                    registrationId = Convert.ToInt64(created.Rows[0]["ma_dk"]);
                    registration.NotifyDataChanged();
                });
                Expect(registrationId > 0, "Huệ's service persists a registration from the active student tab");
                await form.OpenModuleAsync(CampusModule.Timetable);
                Expect(portal.Timetable!.Snapshot.Semester.Id == otherSemester, "Registration semester transfers to timetable");
                Expect(portal.Timetable.Snapshot.Classes.Any(item => item.Id == classCode), "Timetable reloads the newly registered class without restarting");
                await Shot("03-registered-timetable");
                await form.OpenModuleAsync(CampusModule.Registration);
                await registration.ExecuteAsync(async () => {
                    using var canceled = await registration.Service.HuyAsync(registrationId);
                    registration.NotifyDataChanged();
                });
                await form.OpenModuleAsync(CampusModule.Timetable);
                Expect(portal.Timetable!.Snapshot.Classes.All(item => item.Id != classCode), "Timetable removes a canceled class without restarting");
                Expect(portal.Timetable.Snapshot.TotalCredits == 0, "Canceled semester has no remaining credits");

                // Simulate a request still in flight; navigation must retain the active tab.
                await form.OpenModuleAsync(CampusModule.Registration);
                var completion = new TaskCompletionSource();
                var pending = registration.ExecuteAsync(() => completion.Task);
                try
                {
                    await form.OpenModuleAsync(CampusModule.Timetable);
                    Expect(form.SelectedModule == CampusModule.Registration, "Tab switching is blocked during a registration operation");
                    form.Disconnect();
                    Expect(form.CurrentPage == portal, "Disconnect is blocked while the student tab is busy");
                }
                finally { completion.TrySetResult(); await pending; }
                await form.OpenModuleAsync(CampusModule.Timetable);
                form.ClientSize = new(AppTheme.S(1100), AppTheme.S(820));
                await Shot("04-compact");
                Expect(portal.Timetable!.Bounds == portal.ClientRectangle, "Compact timetable stays inside the student form");
                Expect(Application.OpenForms.Cast<Form>().Count(item => item.TopLevel) == 1, "Both student tabs use one top-level form");
                form.Disconnect();
                Expect(portal.IsDisposed && registration.IsDisposed && form.CurrentPage == null, "Disconnect disposes both tab contents");
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
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
