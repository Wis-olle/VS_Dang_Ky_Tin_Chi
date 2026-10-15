using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Catalogs;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Shell;
using QuanLyGiangVien.Features.StudentTimetable;

namespace QuanLyGiangVien.Ui;

/// <summary>Single-window integration checks against an explicit disposable database only.</summary>
internal static class UnifiedUiCheck
{
    internal static void Attach(CampusForm form, string connectionString)
    {
        var output = Environment.GetEnvironmentVariable("UNIFIED_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, SystemInformation.VirtualScreen.Top);
        form.Shown += async (_, _) =>
        {
            var checks = new List<string>(); bool mappingChanged = false;
            void Expect(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException(name);
                checks.Add(name);
            }
            async Task Shot(string name)
            {
                await Task.Delay(1100);
                using var bitmap = UiCapture.Client(form);
                bitmap.Save(Path.Combine(output, name + ".png"));
            }
            try
            {
                Directory.CreateDirectory(output);
                Expect(new SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal),
                    "Unified checks are isolated from the working database");
                await form.ConnectAsync();
                Expect(form.ConnectedIdentity?.Role is "DAO_TAO" or "QUAN_TRI", "Connection uses the actual SQL management identity");
                if (Environment.GetEnvironmentVariable("UNIFIED_UI_EMPTY") == "1")
                {
                    Expect(form.CurrentPage is CatalogsPage, "A database without semesters opens the catalog editor");
                    var grids = Descendants(form.CurrentPage!).OfType<DataGridView>().ToArray();
                    Expect(grids.Length == 2 && grids.All(grid => grid.Rows.Count == 0), "Empty course and semester catalogs are accessible");
                    await Shot("00-empty-catalogs");
                    form.Disconnect();
                    Expect(form.ConnectedIdentity == null, "Empty-database session can disconnect");
                    File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }));
                    return;
                }
                Expect(form.NavigationModules.Count == 5, "Management navigation contains five available modules");
                Expect(form.CurrentPage is CatalogsPage, "Phong's catalogs are the initial management page");
                await Shot("01-catalogs");
                foreach (var module in new[] { CampusModule.Lecturers, CampusModule.Scheduling, CampusModule.Catalogs, CampusModule.Reports })
                {
                    await form.OpenModuleAsync(module);
                    Expect(form.SelectedModule == module, "Navigation opens " + CampusNavigation.Title(module));
                    Expect(form.CurrentPage?.Parent == form.PageHost, "Module is hosted inside the shared window: " + module);
                    Expect(form.CurrentPage?.Bounds == form.PageHost.ClientRectangle,
                        $"Module fills the shared content area: {module}; page={form.CurrentPage?.Bounds}; host={form.PageHost.ClientRectangle}; min={form.CurrentPage?.MinimumSize}; max={form.CurrentPage?.MaximumSize}; auto={form.CurrentPage?.AutoSize}");
                    Expect(Application.OpenForms.Cast<Form>().Count(item => item.TopLevel) == 1, "Exactly one top-level window: " + module);
                    if (form.CurrentPage is MainForm minh)
                        Expect(minh.IsEmbedded && !minh.HasOwnChromeForCheck && !minh.TopLevel, "Minh page uses the shell caption/navigation: " + module);
                    if (module == CampusModule.Scheduling) await Shot("02-scheduling");
                }
                Expect(form.CurrentPage is ReportsPage, "Hue reports use the common database");
                await Shot("03-reports");
                var oldPage = form.CurrentPage;
                form.Disconnect();
                Expect(form.ConnectedIdentity == null && form.CurrentPage == null && oldPage!.IsDisposed,
                    "Disconnect disposes the old page and clears its identity");
                await SetTestRole(connectionString, "SINH_VIEN", "SV2024001"); mappingChanged = true;
                await form.ConnectAsync();
                Expect(form.ConnectedIdentity?.StudentId == "SV2024001", "Student identity is re-read from SQL after reconnect");
                Expect(form.NavigationModules.SequenceEqual(new[] { CampusModule.Registration, CampusModule.Timetable }),
                    "Student navigation exposes registration and timetable only");
                Expect(form.CurrentModulePage is RegistrationPage, "Student enters Hue's registration page");
                await ((RegistrationPage)form.CurrentModulePage!).ReloadForSemesterAsync("HK1-2627");
                var lookup = Descendants(form.CurrentPage!).OfType<DataGridView>().First(grid => grid.Name == "dgvLopMo");
                Expect(lookup.Rows.Count > 0, "Hue's empty-keyword lookup displays the shared open classes");
                await Shot("04-registration");
                await form.OpenModuleAsync(CampusModule.Timetable);
                var timetable = form.CurrentModulePage as StudentTimetableForm;
                Expect(timetable is { IsEmbedded: true, TopLevel: false }, "Student timetable is embedded in the same window");
                Expect(timetable!.Snapshot.Classes.Length == 4 && timetable.Snapshot.TotalCredits == 12,
                    "Timetable reads the signed-in student's actual registration rows");
                Expect(!timetable.HeaderSurface.Visible, "Student's standalone header is hidden inside the shell");
                await form.OpenModuleAsync(CampusModule.Catalogs);
                Expect(form.SelectedModule == CampusModule.Timetable, "Student cannot navigate to management catalogs");
                await Shot("05-timetable");
                form.ClientSize = new(AppTheme.S(1100), AppTheme.S(820)); await Task.Delay(200);
                Expect(form.CurrentPage!.Bounds == form.PageHost.ClientRectangle, "Compact shell keeps the embedded page inside its host");
                Expect(Application.OpenForms.Cast<Form>().Count(item => item.TopLevel) == 1, "Role changes do not create extra top-level windows");
                await Shot("06-compact");
                form.Disconnect();
                Expect(form.CurrentPage == null && form.ConnectedIdentity == null, "Student disconnect leaves no old session page");
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }));
            }
            catch (Exception error)
            {
                var message = error is SqlException sql ? "SQL error " + sql.Number : error.Message;
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = message }));
                Environment.ExitCode = 1;
            }
            finally
            {
                if (mappingChanged) await SetTestRole(connectionString, "DAO_TAO", null);
                form.Close();
            }
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
    private static async Task SetTestRole(string connectionString, string role, string? student)
    {
        using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        using var command = new SqlCommand("IF DB_NAME() NOT LIKE N'DangKyTinChi_Minh_Test[_]%' THROW 52099,'Test database required',1; UPDATE dbo.TAI_KHOAN SET vai_tro=@role,ma_sv=@student,trang_thai=1 WHERE ten_dang_nhap=ORIGINAL_LOGIN();", connection);
        command.Parameters.Add("@role", SqlDbType.VarChar, 20).Value = role;
        command.Parameters.Add("@student", SqlDbType.VarChar, 20).Value = (object?)student ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }
}
