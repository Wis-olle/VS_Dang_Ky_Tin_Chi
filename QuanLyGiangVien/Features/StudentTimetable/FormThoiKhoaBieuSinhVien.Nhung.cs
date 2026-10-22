namespace QuanLyGiangVien.Features.StudentTimetable;

// Gói trạng thái xem bất biến để nơi nhúng có thể ghi nhớ học kỳ, ngày mốc và độ cuộn theo đơn vị bố cục.
internal sealed record StudentTimetableView(string SemesterId, DateOnly Date, int Scroll);

internal sealed partial class StudentTimetableForm
{
    // Chụp trạng thái đang xem; đổi tọa độ cuộn âm của WinForms thành khoảng cuộn dương, độc lập tỉ lệ DPI.
    internal StudentTimetableView CaptureView() => new(semesterId, selectedDate,
        (int)Math.Round(-scroll.AutoScrollPosition.Y / Ui.AppTheme.K));

    // Chọn lại học kỳ hợp lệ và dùng ngày cũ khi cùng học kỳ; việc khôi phục vị trí cuộn do nơi nhúng xử lý.
    internal void RestoreView(string? preferredSemester, StudentTimetableView? prior)
    {
        var index = store.Catalog.Semesters.ToList().FindIndex(item => item.Id == preferredSemester);
        if (index < 0) return;
        // Chặn sự kiện đổi học kỳ trong lúc gán lại bộ chọn để chỉ cập nhật lịch một lần sau khi hoàn tất.
        building = true;
        try { semester.SelectedIndex = index; semesterId = store.Catalog.Semesters[index].Id; }
        finally { building = false; }
        // Nếu học kỳ đã đổi thì lấy hôm nay làm mốc; lần khôi phục này không chạy hiệu ứng chuyển cảnh.
        selectedDate = prior?.SemesterId == semesterId ? prior.Date : DateOnly.FromDateTime(DateTime.Today);
        RefreshWeek(false);
    }
}
