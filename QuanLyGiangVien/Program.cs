using QuanLyGiangVien.Ui;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppTheme.Initialize();
        try
        {
            var args = Environment.GetCommandLineArgs();
            bool HasEnvironment(string key) => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key));
            bool harness = HasEnvironment("LECTURER_UI_CHECK") || HasEnvironment("SCHEDULING_UI_CHECK")
                || HasEnvironment("STUDENT_TIMETABLE_UI_CHECK");
            var mode = DatabaseLaunch.Parse(args, Environment.GetEnvironmentVariable("MINH_DB_CONNECTION"), harness);
            if (mode.UseDatabase && !harness && !args.Contains("--standalone"))
            {
                CampusModule? first = args.Contains("--scheduling") ? CampusModule.Scheduling
                    : args.Contains("--student-timetable") ? CampusModule.Timetable : null;
                Application.Run(new CampusForm(mode.ConnectionString, first, args.Contains("--student-portal")));
                return;
            }
            var database = mode.UseDatabase ? new SqlMinhRepository(mode.ConnectionString).LoadAsync().GetAwaiter().GetResult() : null;
            bool student = args.Contains("--student-timetable") || HasEnvironment("STUDENT_TIMETABLE_UI_CHECK")
                || database?.Identity.Role == "SINH_VIEN";
            Application.Run(student ? new Features.StudentTimetable.StudentTimetableForm(database)
                : new MainForm(args.Contains("--scheduling") || HasEnvironment("SCHEDULING_UI_CHECK"), database));
        }
        catch (Exception error) when (error is Domain.PersistenceException or ArgumentException or InvalidOperationException)
        {
            // Repository errors contain only safe messages, never connection strings or SQL diagnostics.
            var message = error is Domain.PersistenceException ? error.Message : "Không thể mở ứng dụng với dữ liệu hiện tại. Kiểm tra script database, danh mục và chế độ chạy.";
            MessageBox.Show(message + "\n\nKhởi tạo SQL bằng scripts/Initialize-Database.ps1; dùng --demo để xem dữ liệu mẫu.",
                "Campus · Kết nối dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Portraits.Release();
            AppTheme.Release();
        }
    }
}
