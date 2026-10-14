namespace QuanLyGiangVien.Domain.Scheduling;

public static class SchedulingLecturerGuard
{
    public static string? ValidationError(ScheduleStore store, Lecturer proposed, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(proposed);
        if (isNew || ScheduleRules.Same(proposed.Status, "Đang giảng dạy")) return null;
        var assigned = AssignedClass(store, proposed.Id, false);
        return assigned is null ? null : $"Giảng viên đang được phân công lớp {assigned.Id}. Vui lòng chuyển phân công trước khi tạm nghỉ.";
    }

    public static string? DeleteError(ScheduleStore store, string id)
    {
        ArgumentNullException.ThrowIfNull(store);
        var assigned = AssignedClass(store, id, true);
        return assigned is null ? null : $"Giảng viên đang được phân công lớp {assigned.Id}. Vui lòng chuyển phân công trước khi xóa.";
    }

    private static CourseClass? AssignedClass(ScheduleStore store, string id, bool includeCanceled) => store.Classes.FirstOrDefault(item =>
        (includeCanceled || item.Status != SchedulingLabels.Canceled) && ScheduleRules.Same(item.LecturerId, id));
}
