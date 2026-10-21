using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Academics;

var once = args.Contains("--once", StringComparer.Ordinal);
string? Option(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
var term = Option("--term"); var intervalText = Option("--interval-seconds") ?? "30";
if (args.Contains("--help") || term is null || !int.TryParse(intervalText, out var interval) || interval is < 10 or > 300) {
    Console.WriteLine("Campus worker: --term MA_HK [--once] [--interval-seconds 10..300]. Cần MINH_DB_CONNECTION, CAMPUS_WORKER_USER, CAMPUS_WORKER_PASSWORD; tài khoản có WAITLIST_OPERATE. Ctrl+C để dừng."); return 2;
}
try { term = AcademicValidation.Code(term); } catch (ArgumentException e) { Console.Error.WriteLine(e.Message); return 2; }
var connection = Environment.GetEnvironmentVariable("MINH_DB_CONNECTION");
var user = Environment.GetEnvironmentVariable("CAMPUS_WORKER_USER"); var password = Environment.GetEnvironmentVariable("CAMPUS_WORKER_PASSWORD");
Environment.SetEnvironmentVariable("CAMPUS_WORKER_PASSWORD", null);
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password)) {
    Console.Error.WriteLine("Thiếu cấu hình kết nối hoặc tài khoản worker trong environment. Không ghi mật khẩu trong lệnh hoặc tệp source."); return 2;
}
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var login = new PasswordLogin(connection); var signedIn = false;
try {
    await login.SignInAsync(user, password, LoginPortal.Training); signedIn = true; password = null;
    var repository = new OperationsRepository(connection);
    do {
        try {
            using var result = await repository.RunOnceAsync(term);
            Console.WriteLine($"{DateTime.UtcNow:O} {term}: hoàn tất một lượt hàng chờ và chuyển thông báo nội bộ.");
        } catch (PersistenceException e) {
            Console.Error.WriteLine($"{DateTime.UtcNow:O} Worker: {e.Message}");
            if (once || e.Kind is PersistenceFailure.SessionExpired or PersistenceFailure.SchemaMismatch || e.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase)) return 1;
        }
        if (once) break;
        await Task.Delay(TimeSpan.FromSeconds(interval), stop.Token);
    } while (!stop.IsCancellationRequested);
    return 0;
} catch (OperationCanceledException) when (stop.IsCancellationRequested) { return 0; }
catch (Exception e) when (e is PersistenceException or ArgumentException or InvalidOperationException) { Console.Error.WriteLine(e.Message); return 1; }
finally { if (signedIn) { try { await login.SignOutAsync(); } catch (PersistenceException) { Console.Error.WriteLine("Không xác nhận được đăng xuất worker; phiên sẽ hết hạn theo chính sách."); } } }
