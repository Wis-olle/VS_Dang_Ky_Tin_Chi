using AntdUI;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed partial class SchedulingPage
{
    // Thứ tự tên trong mảng tương ứng giá trị TimetableScopeKind của bộ chọn loại lịch.
    private static readonly string[] ScopeNames = ["Giảng viên", "Sinh viên", "Lớp học phần", "Lớp sinh viên", "Phòng học"];
    private readonly Select timetableScope = new(), timetableSubject = new();
    private readonly AntdUI.Button reloadTimetableSubjects = SchedulingTheme.Button("Tải lại", "ReloadOutlined");
    // Ánh xạ nhãn dễ đọc sang mã thật; mã được dùng để lọc, không dựa vào tên có thể trùng nhau.
    private readonly Dictionary<string, string> timetableSubjects = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource timetableLifetime = new();
    private readonly Func<CancellationToken, Task<TimetableDirectory>>? loadTimetableDirectory;
    private readonly Func<Task>? reloadTimetableSnapshot;
    private TimetableDirectory timetableDirectory;
    private TimetableScopeKind scopeKind;
    private string? timetableSubjectId, timetableDirectoryError;
    private bool timetableDirectoryReady;
    private bool directoryLoading;
    private Task? directoryLoadTask;

    // Hai chế độ này cần danh mục sinh viên và đăng ký ngoài danh mục lớp/phòng có sẵn trong Store.
    private bool UsesStudentDirectory => scopeKind is TimetableScopeKind.Student or TimetableScopeKind.StudentClass;

    // Tạo bộ chọn thời khóa biểu
    private void BuildTimetableSelectors()
    {
        ConfigureSelect(timetableScope, ScopeNames);
        timetableScope.AccessibleName = "Xem thời khóa biểu theo";
        timetableSubject.AccessibleName = "Chọn đối tượng xem thời khóa biểu";
        reloadTimetableSubjects.AccessibleName = "Tải lại dữ liệu và thời khóa biểu";
        timetableSubject.PlaceholderText = "Chọn giảng viên…";
        ConfigureTimetableSubjects();
        // Chọn loại đối tượng xem lịch
        timetableScope.SelectedIndexChanged += async (_, _) => {
            if (!ready) return;
            scopeKind = (TimetableScopeKind)Math.Max(0, timetableScope.SelectedIndex);
            timetableSubjectId = null;
            ConfigureTimetableSubjects(); RefreshData(false);
            if (UsesStudentDirectory) await EnsureTimetableDirectoryAsync();
        };
        // Chọn đối tượng xem lịch
        timetableSubject.SelectedIndexChanged += (_, _) => {
            if (!ready) return;
            timetableSubjectId = timetableSubjects.GetValueOrDefault(timetableSubject.SelectedValue?.ToString() ?? "");
            RefreshData(false);
        };
        // Tải lại thời khóa biểu
        reloadTimetableSubjects.Click += async (_, _) => {
            if (reloadTimetableSnapshot != null) { await reloadTimetableSnapshot(); return; }
            if (UsesStudentDirectory)
            {
                timetableDirectoryReady = false;
                await EnsureTimetableDirectoryAsync();
            }
            else { ConfigureTimetableSubjects(); RefreshData(false); }
        };
    }

    // Cập nhật danh sách đối tượng xem lịch
    private void ConfigureTimetableSubjects()
    {
        // Dựng cặp nhãn/mã theo loại đối tượng; lớp học phần chỉ lấy trong kỳ đang chọn.
        IEnumerable<KeyValuePair<string, string>> values = scopeKind switch {
            TimetableScopeKind.Lecturer => Store.Catalog.Lecturers.OrderBy(l => l.Name)
                .Select(l => KeyValuePair.Create($"{l.Id} · {l.Name}", l.Id)),
            TimetableScopeKind.Room => Store.Rooms.Select(r => KeyValuePair.Create($"{r.Id} · {r.Building}", r.Id)),
            TimetableScopeKind.CourseClass => Store.Classes.Where(c => ScheduleRules.Same(c.SemesterId, SemesterId))
                .Select(c => KeyValuePair.Create($"{c.Id} · {Store.Catalog.Courses.First(p => p.Id == c.CourseId).Name}", c.Id)),
            TimetableScopeKind.Student => timetableDirectory.Students.OrderBy(s => s.Name)
                .Select(s => KeyValuePair.Create($"{s.Id} · {s.Name}", s.Id)),
            TimetableScopeKind.StudentClass => timetableDirectory.Students.GroupBy(s => s.ClassId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key).Select(g => KeyValuePair.Create($"{g.Key} · {g.First().ClassName}", g.Key)),
            _ => []
        };
        var choices = values.ToArray();
        // Tạm chặn sự kiện chọn; chỉ thay Items khi danh mục thực sự đổi để tránh mất lựa chọn.
        bool wasReady = ready;
        ready = false;
        if (!timetableSubjects.SequenceEqual(choices) || timetableSubject.Items.Count != choices.Length)
        {
            timetableSubjects.Clear();
            foreach (var choice in choices) timetableSubjects.Add(choice.Key, choice.Value);
            ConfigureSelect(timetableSubject, choices.Select(c => c.Key));
        }
        timetableSubject.SelectedIndex = timetableSubjectId == null ? -1 : timetableSubject.Items.ToList().FindIndex(x =>
            ScheduleRules.Same(timetableSubjects.GetValueOrDefault(x.ToString() ?? ""), timetableSubjectId));
        // Khôi phục theo mã và khóa danh mục sinh viên đến khi tải thành công.
        timetableSubject.PlaceholderText = "Chọn " + ScopeNames[(int)scopeKind].ToLowerInvariant() + "…";
        timetableSubject.AccessibleName = timetableSubject.PlaceholderText;
        timetableSubject.Enabled = !UsesStudentDirectory || timetableDirectoryReady;
        reloadTimetableSubjects.Enabled = !directoryLoading;
        ready = wasReady;
    }

    // Kiểm tra dữ liệu danh mục sinh viên
    private Task EnsureTimetableDirectoryAsync()
    {
        // Dùng lại tác vụ đang chạy để nhiều sự kiện không đồng thời gửi nhiều yêu cầu tải.
        if (timetableDirectoryReady || loadTimetableDirectory == null || IsDisposed) return Task.CompletedTask;
        if (directoryLoadTask is { IsCompleted: false }) return directoryLoadTask;
        directoryLoadTask = LoadTimetableDirectoryCoreAsync();
        return directoryLoadTask;
    }

    // Tải danh mục sinh viên để xem lịch
    private async Task LoadTimetableDirectoryCoreAsync()
    {
        timetableDirectoryError = null;
        directoryLoading = true;
        timetableSubject.Enabled = false; reloadTimetableSubjects.Enabled = false;
        RefreshData(false);
        try
        {
            // Token gắn với vòng đời trang; không cập nhật control nếu trang đã bị hủy trong lúc chờ.
            var loaded = await loadTimetableDirectory!(timetableLifetime.Token);
            if (IsDisposed || timetableLifetime.IsCancellationRequested) return;
            timetableDirectory = loaded;
            timetableDirectoryReady = true;
        }
        // Đóng trang là hủy có chủ đích; lỗi dữ liệu thật được giữ lại để hiện trong chú thích lịch.
        catch (OperationCanceledException) when (timetableLifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is PersistenceException or InvalidOperationException or ArgumentException)
        {
            if (!IsDisposed) timetableDirectoryError = "Không tải được danh mục sinh viên. " + error.Message;
        }
        finally
        {
            // Dù tải thành công hay lỗi, kết thúc trạng thái chờ và cho phép người dùng tải lại.
            directoryLoading = false;
            if (!IsDisposed)
            {
                ConfigureTimetableSubjects(); reloadTimetableSubjects.Enabled = true; RefreshData(false);
            }
        }
    }

    // Hiển thị tiêu đề và trạng thái lịch
    private string TimetableCaption()
    {
        if (UsesStudentDirectory && !timetableDirectoryReady)
            return timetableDirectoryError ?? (loadTimetableDirectory == null ? "Chưa có danh mục sinh viên để xem lịch."
                : "Đang tải danh mục sinh viên…");
        if (timetableSubjectId == null) return "Chọn " + ScopeNames[(int)scopeKind].ToLowerInvariant() + " để xem thời khóa biểu.";
        var label = timetableSubjects.FirstOrDefault(p => ScheduleRules.Same(p.Value, timetableSubjectId)).Key;
        if (label == null) return "Đối tượng không còn trong danh mục của học kỳ này. Chọn lại đối tượng.";
        if (scopeKind == TimetableScopeKind.StudentClass)
        {
            // Lịch lớp sinh viên là tổng hợp đăng ký từng người, không phải một lớp học phần duy nhất.
            int count = timetableDirectory.Students.Count(s => ScheduleRules.Same(s.ClassId, timetableSubjectId));
            return $"{label} · Lịch đăng ký của {count} sinh viên; từng người có thể học các lớp khác nhau.";
        }
        return $"{ScopeNames[(int)scopeKind]} · {label}";
    }

    // Xem lịch theo đối tượng đã chọn
    private async Task SelectTimetableAsync(TimetableScopeKind kind, string? subjectId)
    {
        // Cho phép mở lịch phòng từ khung chi tiết mà vẫn đi qua cùng luồng chọn loại và chọn mã.
        bool wasReady = ready;
        ready = false; scopeKind = kind; timetableScope.SelectedIndex = (int)kind;
        timetableSubjectId = subjectId;
        ready = wasReady;
        if (UsesStudentDirectory) await EnsureTimetableDirectoryAsync();
        if (IsDisposed) return;
        ConfigureTimetableSubjects(); RefreshData(false);
    }
    // Các điểm kiểm chứng đọc lịch và kích hoạt đúng sự kiện tải lại đang dùng trên giao diện.
    internal Task SetScopeForCheckAsync(TimetableScopeKind kind, string? subjectId) => SelectTimetableAsync(kind, subjectId);
    internal WeeklySchedule CalendarForCheck => calendar;
    internal void ReloadTimetableForCheck() => reloadTimetableSubjects.PerformClick();
    internal bool TimetableReloadEnabledForCheck => reloadTimetableSubjects.Enabled;
    internal string TimetableCaptionForCheck => TimetableCaption();
}
