using System.Text.RegularExpressions;

namespace QuanLyGiangVien.Domain.Scheduling;

public sealed class ScheduleStore
{
    private readonly ISchedulePersistence? persistence;
    public TeachingCatalog Catalog { get; }
    public IReadOnlyList<Room> Rooms { get; private set; } = Array.AsReadOnly(Array.Empty<Room>());
    public IReadOnlyList<CourseClass> Classes { get; private set; } = Array.AsReadOnly(Array.Empty<CourseClass>());

    public ScheduleStore(TeachingCatalog catalog, IEnumerable<Room> rooms, IEnumerable<CourseClass> classes,
        ISchedulePersistence? persistence = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(classes);
        Catalog = catalog;
        foreach (var room in rooms)
        {
            if (room is null || !TrySaveRoom(room, true, out _))
                throw new ArgumentException("Dữ liệu phòng học mẫu không hợp lệ.", nameof(rooms));
        }
        foreach (var item in classes)
        {
            if (item is null)
                throw new ArgumentException("Dữ liệu lớp học phần mẫu không hợp lệ.", nameof(classes));
            var normalized = ScheduleRules.Normalize(item);
            if (ValidateClass(normalized, true, true).Count > 0)
                throw new ArgumentException("Dữ liệu lớp học phần mẫu không hợp lệ.", nameof(classes));
            Classes = Array.AsReadOnly(Classes.Append(normalized).ToArray());
        }
        this.persistence = persistence;
    }

    public string NextRoomId() => ScheduleRules.NextId(Rooms.Select(item => item.Id), "P");
    public string NextClassId() => ScheduleRules.NextId(Classes.Select(item => item.Id), "LHP");

    public bool TrySaveRoom(Room value, bool isNew, out IReadOnlyDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = ScheduleRules.Normalize(value);
        errors = ValidateRoom(normalized, isNew);
        if (errors.Count > 0) return false;
        try { persistence?.SaveRoom(normalized, isNew); }
        catch (PersistenceException exception) { errors = PersistenceErrors(exception); return false; }
        Rooms = Array.AsReadOnly(isNew ? Rooms.Append(normalized).ToArray() :
            Rooms.Select(item => ScheduleRules.Same(item.Id, normalized.Id) ? normalized : item).ToArray());
        return true;
    }

    public bool TrySaveClass(CourseClass value, bool isNew, out IReadOnlyDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = ScheduleRules.Normalize(value);
        errors = ValidateClass(normalized, isNew, false);
        if (errors.Count > 0) return false;
        try
        {
            var enrolled = persistence?.SaveClass(normalized, isNew) ?? normalized.Enrolled;
            if (enrolled < 0 || enrolled > normalized.Capacity)
                throw new PersistenceException("Kết quả sĩ số từ SQL chưa hợp lệ. Vui lòng tải lại dữ liệu.");
            normalized = normalized with { Enrolled = enrolled };
        }
        catch (PersistenceException exception) { errors = PersistenceErrors(exception); return false; }
        Classes = Array.AsReadOnly(isNew ? Classes.Append(normalized).ToArray() :
            Classes.Select(item => ScheduleRules.Same(item.Id, normalized.Id) ? normalized : item).ToArray());
        return true;
    }

    public bool TryDeleteRoom(string id, out string? error)
    {
        var existing = Rooms.FirstOrDefault(item => ScheduleRules.Same(item.Id, id));
        error = existing is null ? "Không tìm thấy phòng học." : Classes.Any(item => item.Meetings.Any(
            meeting => ScheduleRules.Same(meeting.RoomId, id)))
            ? "Phòng đang được tham chiếu trong lịch lớp học. Vui lòng đổi hoặc bỏ lịch trước khi xóa." : null;
        if (error is not null) return false;
        try { persistence?.DeleteRoom(existing!.Id); }
        catch (PersistenceException exception) { error = exception.Message; return false; }
        Rooms = Array.AsReadOnly(Rooms.Where(item => !ScheduleRules.Same(item.Id, id)).ToArray());
        return true;
    }

    public bool TryDeleteClass(string id, out string? error)
    {
        var existing = Classes.FirstOrDefault(item => ScheduleRules.Same(item.Id, id));
        error = existing is null ? "Không tìm thấy lớp học phần." : existing.Enrolled > 0
            ? "Lớp đã có sinh viên đăng ký. Không thể xóa lớp." : null;
        if (error is not null) return false;
        try { persistence?.DeleteClass(existing!.Id); }
        catch (PersistenceException exception) { error = exception.Message; return false; }
        Classes = Array.AsReadOnly(Classes.Where(item => !ScheduleRules.Same(item.Id, id)).ToArray());
        return true;
    }

    private IReadOnlyDictionary<string, string> ValidateRoom(Room value, bool isNew)
    {
        var exists = Rooms.Any(item => ScheduleRules.Same(item.Id, value.Id));
        var assigned = Classes.Where(item => item.Status != SchedulingLabels.Canceled
            && item.Meetings.Any(meeting => ScheduleRules.Same(meeting.RoomId, value.Id))).ToArray();
        return ScheduleRules.Errors(
            ("Id", !ScheduleRules.ValidRoomId(value.Id), "Mã phòng gồm 2–20 chữ cái, chữ số hoặc dấu gạch ngang."),
            ("Id", isNew && exists, "Mã phòng học đã tồn tại."),
            ("Id", !isNew && !exists, "Không tìm thấy phòng học cần cập nhật."),
            ("Building", value.Building.Length is < 1 or > 60, "Tên tòa nhà phải có 1–60 ký tự."),
            ("Capacity", value.Capacity is < 1 or > 500, "Sức chứa phải từ 1 đến 500 chỗ."),
            ("Capacity", assigned.Any(item => item.Capacity > value.Capacity), "Sức chứa phải đáp ứng sĩ số tối đa của lớp đang xếp tại phòng."),
            ("Type", !SchedulingLabels.RoomTypes.Contains(value.Type), "Vui lòng chọn loại phòng học."),
            ("Status", !SchedulingLabels.RoomStatuses.Contains(value.Status), "Vui lòng chọn trạng thái phòng học."),
            ("Status", value.Status == SchedulingLabels.Maintenance && assigned.Length > 0,
                "Phòng đang có lịch học. Vui lòng chuyển lịch trước khi bảo trì."));
    }

    private static IReadOnlyDictionary<string, string> PersistenceErrors(PersistenceException exception) =>
        System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add("_global", exception.Message);

    private IReadOnlyDictionary<string, string> ValidateClass(CourseClass value, bool isNew, bool allowSeedEnrollment)
    {
        var existing = Classes.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.Id));
        var lecturer = Catalog.Lecturers.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.LecturerId));
        var semester = Catalog.Semesters.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.SemesterId));
        var hasEnrolled = existing is { Enrolled: > 0 };
        var meetingError = MeetingError(value, semester);
        return ScheduleRules.Errors(
            ("Id", !Regex.IsMatch(value.Id, @"^LHP[0-9]{4}$"), "Mã lớp gồm LHP và 4 chữ số."),
            ("Id", isNew && existing is not null, "Mã lớp học phần đã tồn tại."),
            ("Id", !isNew && existing is null, "Không tìm thấy lớp học phần cần cập nhật."),
            ("CourseId", !Catalog.Courses.Any(item => ScheduleRules.Same(item.Id, value.CourseId)), "Vui lòng chọn học phần."),
            ("CourseId", hasEnrolled && !ScheduleRules.Same(existing!.CourseId, value.CourseId), "Lớp có đăng ký không thể đổi học phần."),
            ("SemesterId", semester is null, "Vui lòng chọn học kỳ."),
            ("SemesterId", hasEnrolled && !ScheduleRules.Same(existing!.SemesterId, value.SemesterId), "Lớp có đăng ký không thể đổi học kỳ."),
            ("LecturerId", lecturer is null || (value.Status != SchedulingLabels.Canceled && lecturer.Status != "Đang giảng dạy"),
                "Vui lòng chọn giảng viên đang giảng dạy."),
            ("Capacity", value.Capacity is < 1 or > 500 || value.Capacity < value.Enrolled, "Sĩ số tối đa phải từ 1–500 và không thấp hơn số đã đăng ký."),
            ("Enrolled", value.Enrolled < 0 || value.Enrolled > value.Capacity, "Số đăng ký không hợp lệ."),
            ("Enrolled", !allowSeedEnrollment && (isNew ? value.Enrolled != 0 : existing is not null && existing.Enrolled != value.Enrolled),
                "Số đăng ký do chức năng đăng ký tín chỉ quản lý."),
            ("Status", !SchedulingLabels.ClassStatuses.Contains(value.Status), "Vui lòng chọn trạng thái lớp học."),
            ("Status", value.Status == SchedulingLabels.Canceled && value.Enrolled > 0, "Lớp có sinh viên đăng ký không thể hủy."),
            ("Meetings", meetingError is not null, meetingError ?? ""));
    }

    private string? MeetingError(CourseClass value, Semester? semester)
    {
        if (value.Meetings.Length > 7) return "Mỗi lớp chỉ có tối đa 7 buổi học mỗi tuần.";
        foreach (var meeting in value.Meetings)
        {
            if (!ScheduleRules.ValidTime(meeting)) return "Buổi học phải có thứ, tiết 1–12 và khoảng ngày có buổi học hợp lệ.";
            if (semester is null || meeting.StartDate < semester.StartDate || meeting.EndDate > semester.EndDate)
                return "Khoảng ngày học phải nằm trong học kỳ đã chọn.";
            var room = Rooms.FirstOrDefault(item => ScheduleRules.Same(item.Id, meeting.RoomId));
            if (room is null) return "Vui lòng chọn phòng học tồn tại cho mỗi buổi học.";
            if (value.Status != SchedulingLabels.Canceled && (room.Status != SchedulingLabels.Ready || room.Capacity < value.Capacity))
                return "Phòng học phải sẵn sàng và đủ sức chứa cho sĩ số tối đa của lớp.";
        }
        for (var index = 0; index < value.Meetings.Length; index++)
            if (value.Meetings.Skip(index + 1).Any(other => ScheduleRules.Overlaps(value.Meetings[index], other)))
                return "Các buổi học của cùng lớp không được trùng thời gian.";
        return value.Status == SchedulingLabels.Canceled ? null : ConflictError(value);
    }

    private string? ConflictError(CourseClass value)
    {
        foreach (var other in Classes.Where(item => !ScheduleRules.Same(item.Id, value.Id)
            && item.Status != SchedulingLabels.Canceled))
        foreach (var requested in value.Meetings)
        foreach (var scheduled in other.Meetings)
        {
            if (!ScheduleRules.Overlaps(requested, scheduled)) continue;
            if (ScheduleRules.Same(requested.RoomId, scheduled.RoomId))
                return $"Trùng phòng {requested.RoomId} với lớp {other.Id}.";
            if (ScheduleRules.Same(value.LecturerId, other.LecturerId))
                return $"Trùng lịch giảng viên với lớp {other.Id}.";
        }
        return null;
    }
}
