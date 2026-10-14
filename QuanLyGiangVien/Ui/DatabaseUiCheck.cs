using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Dialogs;

namespace QuanLyGiangVien.Ui;

/// <summary>Native SQL smoke check; writes only to an explicitly selected disposable database.</summary>
internal static class DatabaseUiCheck
{
    internal static void Attach(MainForm form, DatabaseSnapshot? initial)
    {
        var output = Environment.GetEnvironmentVariable("DATABASE_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, SystemInformation.VirtualScreen.Top);
        form.Shown += async (_, _) =>
        {
            var checks = new List<string>();
            SqlMinhRepository? repository = null;
            string? insertedId = null;
            void Expect(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException(name);
                checks.Add(name);
            }
            try
            {
                Directory.CreateDirectory(output);
                var connection = Environment.GetEnvironmentVariable("MINH_DB_CONNECTION");
                Expect(!string.IsNullOrWhiteSpace(connection) && new SqlConnectionStringBuilder(connection).InitialCatalog
                    .StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal), "Only disposable SQL database is writable by this harness");
                Expect(initial?.Repository != null, "MainForm receives the SQL repository snapshot");
                repository = new SqlMinhRepository(connection!);
                await Task.Delay(1700);
                Expect(form.Store.Items.Count == initial!.Lecturers.Length, "Lecturer list comes from SQL");
                Expect(form.Store.Items.All(item => item.ActiveClasses == initial.Classes.Count(c => c.LecturerId == item.Id
                    && c.Status != Domain.Scheduling.SchedulingLabels.Canceled)), "Lecturer assignments come from real course classes");
                using (var picture = UiCapture.Client(form)) picture.Save(Path.Combine(output, "01-sql-lecturers.png"));
                var candidate = initial.Lecturers.First(item => item.Status == "Đang giảng dạy") with {
                    Id = form.Store.NextId(), Name = "Nguyễn Kiểm Thử", Email = "native-db-check@example.invalid", Phone = "0999988899", ActiveClasses = 0
                };
                using (var editor = new LecturerEditor(form.Store, facultyChoices: initial.Faculties.Select(item => item.Name).ToArray()))
                {
                    editor.Show(form); editor.FillForTest(candidate);
                    Expect(editor.TrySave(), "Native lecturer editor saves through SQL persistence");
                    insertedId = candidate.Id; editor.Close();
                }
                var reloaded = await repository.LoadAsync();
                Expect(reloaded.Lecturers.Any(item => item.Id == candidate.Id && item.Name == candidate.Name),
                    "A new connection reads the lecturer saved by the editor");
                var priorStore = form.Store;
                await form.ReloadDatabaseAsync();
                Expect(!ReferenceEquals(priorStore, form.Store), "Reload replaces the SQL snapshot without restarting the application");
                form.SetFiltersForCheck(candidate.Id);
                Expect(form.ResultCount == 1, "Native search sees the SQL-backed inserted record");
                form.SetFiltersForCheck("");
                var page = form.ShowScheduling();
                await Task.Delay(400);
                Expect(page.Store.Classes.Count == initial.Classes.Length, "Scheduling uses the same SQL class data");
                Expect(page.Store.Classes.Sum(item => item.Enrolled) == initial.Registrations.Count(item => item.Status == "DANG_KY"),
                    "Displayed enrollment equals actual registration rows");
                using (var picture = UiCapture.Client(form)) picture.Save(Path.Combine(output, "02-sql-scheduling.png"));
                repository.DeleteLecturer(insertedId); insertedId = null;
                Expect((await repository.LoadAsync()).Lecturers.Length == initial.Lecturers.Length, "Native test cleans up its saved record");
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.Message }));
                Environment.ExitCode = 1;
            }
            finally
            {
                if (insertedId != null && repository != null)
                {
                    try { repository.DeleteLecturer(insertedId); }
                    catch (Domain.PersistenceException error)
                    {
                        File.WriteAllText(Path.Combine(output, "cleanup-error.txt"), error.Message);
                        Environment.ExitCode = 1;
                    }
                }
                form.Close();
            }
        };
    }
}
