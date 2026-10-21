using QuanLyGiangVien.Ui;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Shell;
using Microsoft.Data.SqlClient;

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
                || HasEnvironment("STUDENT_TIMETABLE_UI_CHECK") || HasEnvironment("WEEKLY_TIMETABLE_UI_CHECK")
                || HasEnvironment("SCOPED_TIMETABLE_UI_CHECK");
            var mode = DatabaseLaunch.Parse(args, Environment.GetEnvironmentVariable("MINH_DB_CONNECTION"), harness);
            bool legacyCheck = HasEnvironment("STUDENT_PORTAL_UI_CHECK") || HasEnvironment("UNIFIED_UI_CHECK");
            bool loginCheck = HasEnvironment("PASSWORD_LOGIN_UI_CHECK") || HasEnvironment("CAMPUS_PHASE1_UI_CHECK") || HasEnvironment("CAMPUS_PHASE2_UI_CHECK") || HasEnvironment("CAMPUS_PHASE3_UI_CHECK") || HasEnvironment("CAMPUS_PHASE4_UI_CHECK") || HasEnvironment("CAMPUS_AUDIT_FIX_UI_CHECK") || HasEnvironment("CAMPUS_SHOWCASE_UI_CHECK") || HasEnvironment("CAMPUS_QUICK_LOGIN_UI_CHECK");
            if (mode.UseDatabase && (legacyCheck || harness || loginCheck)
                && !new SqlConnectionStringBuilder(mode.ConnectionString).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
                throw new InvalidOperationException("Kiểm chứng Windows identity chỉ được chạy trên database kiểm thử riêng.");
            if (mode.UseDatabase && !harness)
            {
                CampusModule? first = args.Contains("--scheduling") ? CampusModule.Scheduling
                    : args.Contains("--student-timetable") ? CampusModule.Timetable : null;
                Application.Run(new CampusForm(mode.ConnectionString, first, args.Contains("--student-portal"), passwordLogin: !legacyCheck));
                return;
            }
            var sqlIdentity = mode.UseDatabase ? new SqlIdentityRepository(mode.ConnectionString).LoadAsync().GetAwaiter().GetResult() : null;
            var timetableData = sqlIdentity?.Role == "SINH_VIEN" ? new StudentTimetableRepository(mode.ConnectionString).LoadAsync().GetAwaiter().GetResult() : null;
            var database = mode.UseDatabase && timetableData == null ? new SqlMinhRepository(mode.ConnectionString).LoadAsync().GetAwaiter().GetResult() : null;
            bool student = args.Contains("--student-timetable") || HasEnvironment("STUDENT_TIMETABLE_UI_CHECK")
                || timetableData != null;
            Application.Run(student ? new Features.StudentTimetable.StudentTimetableForm(database, readModel: timetableData)
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
