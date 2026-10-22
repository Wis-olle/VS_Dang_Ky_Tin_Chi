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

    // Thêm hoặc cập nhật phòng học
    public bool TrySaveRoom(Room value, bool isNew, out IReadOnlyDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = ScheduleRules.Normalize(value);
        errors = ValidateRoom(normalized, isNew);
        if (errors.Count > 0) return false;
        try { normalized = persistence?.SaveRoom(normalized, isNew) ?? normalized; }
        catch (PersistenceException exception) { errors = PersistenceErrors(exception); return false; }
        Rooms = Array.AsReadOnly(isNew ? Rooms.Append(normalized).ToArray() :
            Rooms.Select(item => ScheduleRules.Same(item.Id, normalized.Id) ? normalized : item).ToArray());
        return true;
    }

    // Thêm hoặc cập nhật lớp học phần
    public bool TrySaveClass(CourseClass value, bool isNew, out IReadOnlyDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = ScheduleRules.Normalize(value);
        errors = ValidateClass(normalized, isNew, false);
        var existingLifecycle = Classes.FirstOrDefault(c => ScheduleRules.Same(c.Id,value.Id));
        if (existingLifecycle is not null && (existingLifecycle.Lifecycle is not ("REGISTRATION" or "CLOSED")
            || existingLifecycle.Lifecycle != value.Lifecycle || !existingLifecycle.ActualMeetings.Equals(value.ActualMeetings)))
            errors = System.Collections.Immutable.ImmutableDictionary<string,string>.Empty.Add("_global","Dùng quy trình vận hành lớp để thay đổi lịch hoặc vòng đời.");
        if (errors.Count > 0) return false;
        try
        {
            var saved = persistence?.SaveClass(normalized, isNew) ?? normalized;
            var enrolled = saved.Enrolled;
            if (enrolled < 0 || enrolled > normalized.Capacity)
                throw new PersistenceException("Kết quả sĩ số từ SQL chưa hợp lệ. Vui lòng tải lại dữ liệu.");
            normalized = saved;
        }
        catch (PersistenceException exception) { errors = PersistenceErrors(exception); return false; }
        Classes = Array.AsReadOnly(isNew ? Classes.Append(normalized).ToArray() :
            Classes.Select(item => ScheduleRules.Same(item.Id, normalized.Id) ? normalized : item).ToArray());
        return true;
    }

    // Xóa phòng học
    public bool TryDeleteRoom(string id, out string? error)
    {
        var existing = Rooms.FirstOrDefault(item => ScheduleRules.Same(item.Id, id));
        error = existing is null ? "Không tìm thấy phòng học." : Classes.Any(item => (item.Meetings.Any(meeting => ScheduleRules.Same(meeting.RoomId, id))
            || (!item.ActualMeetings.IsDefault && item.ActualMeetings.Any(m=>ScheduleRules.Same(m.RoomId,id)))))
            ? "Phòng đang được tham chiếu trong lịch lớp học. Vui lòng đổi hoặc bỏ lịch trước khi xóa." : null;
        if (error is not null) return false;
        try { persistence?.DeleteRoom(existing!.Id, existing.Version); }
        catch (PersistenceException exception) { error = exception.Message; return false; }
        Rooms = Array.AsReadOnly(Rooms.Where(item => !ScheduleRules.Same(item.Id, id)).ToArray());
        return true;
    }

    // Xóa lớp học phần
    public bool TryDeleteClass(string id, out string? error)
    {
        var existing = Classes.FirstOrDefault(item => ScheduleRules.Same(item.Id, id));
        error = existing is null ? "Không tìm thấy lớp học phần." : existing.Enrolled > 0 || existing.HasRegistrationHistory
            ? "Lớp có lịch sử đăng ký. Không thể xóa lớp." : null;
        if (error is not null) return false;
        try { persistence?.DeleteClass(existing!.Id, existing.Version); }
        catch (PersistenceException exception) { error = exception.Message; return false; }
        Classes = Array.AsReadOnly(Classes.Where(item => !ScheduleRules.Same(item.Id, id)).ToArray());
        return true;
    }

    // Kiểm tra thông tin phòng học
    private IReadOnlyDictionary<string, string> ValidateRoom(Room value, bool isNew)
    {
        var existing = Rooms.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.Id));
        var exists = existing is not null;
        var assigned = Classes.Where(item => LifecycleRules.BlocksResources(item)
            && (item.ActualMeetings.IsDefault ? item.Meetings.Any(meeting => ScheduleRules.Same(meeting.RoomId, value.Id))
                : item.ActualMeetings.Any(meeting => ScheduleRules.Same(meeting.RoomId,value.Id)))).ToArray();
        return ScheduleRules.Errors(
            ("Id", !ScheduleRules.ValidRoomId(value.Id), "Mã phòng gồm 1–20 chữ cái ASCII, chữ số, gạch ngang hoặc gạch dưới."),
            ("Id", isNew && exists, "Mã phòng học đã tồn tại."),
            ("Id", !isNew && !exists, "Không tìm thấy phòng học cần cập nhật."),
            ("_global", !isNew && existing is not null && existing.Version != value.Version,
                "Dữ liệu đã thay đổi. Vui lòng đóng form, tải lại bằng F5 và mở lại."),
            ("Building", value.Building.Length is < 1 or > 80, "Tên tòa nhà phải có 1–80 ký tự."),
            ("Capacity", value.Capacity is < 1 or > 1000, "Sức chứa phải từ 1 đến 1000 chỗ."),
            ("Capacity", assigned.Any(item => item.Capacity > value.Capacity), "Sức chứa phải đáp ứng sĩ số tối đa của lớp đang xếp tại phòng."),
            ("Type", !SchedulingLabels.RoomTypes.Contains(value.Type), "Vui lòng chọn loại phòng học."),
            ("Status", !SchedulingLabels.RoomStatuses.Contains(value.Status), "Vui lòng chọn trạng thái phòng học."),
            ("Status", value.Status == SchedulingLabels.Maintenance && assigned.Length > 0,
                "Phòng đang có lịch học. Vui lòng chuyển lịch trước khi bảo trì."));
    }

    private static IReadOnlyDictionary<string, string> PersistenceErrors(PersistenceException exception) =>
        System.Collections.Immutable.ImmutableDictionary<string, string>.Empty.Add(
            exception.Kind == PersistenceFailure.SessionExpired ? "_session" : "_global", exception.Message);

    // Kiểm tra thông tin và lịch lớp học phần
    private IReadOnlyDictionary<string, string> ValidateClass(CourseClass value, bool isNew, bool allowSeedEnrollment)
    {
        var existing = Classes.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.Id));
        var lecturer = Catalog.Lecturers.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.LecturerId));
        var semester = Catalog.Semesters.FirstOrDefault(item => ScheduleRules.Same(item.Id, value.SemesterId));
        var hasEnrolled = existing is { Enrolled: > 0 };
        var hasHistory = hasEnrolled || existing is { HasRegistrationHistory: true };
        var meetingError = MeetingError(value, semester);
        return ScheduleRules.Errors(
            ("Id", !Regex.IsMatch(value.Id, @"^[A-Z0-9_-]{1,20}$"), "Mã lớp gồm 1–20 chữ cái ASCII, chữ số, gạch ngang hoặc gạch dưới."),
            ("Id", isNew && existing is not null, "Mã lớp học phần đã tồn tại."),
            ("Id", !isNew && existing is null, "Không tìm thấy lớp học phần cần cập nhật."),
            ("_global", !isNew && existing is not null && existing.Version != value.Version,
                "Dữ liệu đã thay đổi. Vui lòng đóng form, tải lại bằng F5 và mở lại."),
            ("CourseId", !Catalog.Courses.Any(item => ScheduleRules.Same(item.Id, value.CourseId)), "Vui lòng chọn học phần."),
            ("CourseId", hasHistory && !ScheduleRules.Same(existing!.CourseId, value.CourseId), "Lớp có đăng ký không thể đổi học phần."),
            ("SemesterId", semester is null, "Vui lòng chọn học kỳ."),
            ("SemesterId", hasHistory && !ScheduleRules.Same(existing!.SemesterId, value.SemesterId), "Lớp có đăng ký không thể đổi học kỳ."),
            ("LecturerId", hasHistory && !ScheduleRules.Same(existing!.LecturerId, value.LecturerId), "Lớp có lịch sử đăng ký không thể đổi giảng viên."),
            ("Meetings", hasHistory && !existing!.Meetings.OrderBy(m => m.ToString()).SequenceEqual(value.Meetings.OrderBy(m => m.ToString())), "Lớp có lịch sử đăng ký không thể đổi buổi học."),
            ("LecturerId", lecturer is null || (LifecycleRules.BlocksResources(value) && value.ActualMeetings.IsDefault && lecturer.Status != "Đang giảng dạy"),
                "Vui lòng chọn giảng viên đang giảng dạy."),
            ("Capacity", value.Capacity is < 1 or > 1000 || value.Capacity < value.Enrolled, "Sĩ số tối đa phải từ 1–1000 và không thấp hơn số đã đăng ký."),
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
            if (LifecycleRules.BlocksResources(value) && value.ActualMeetings.IsDefault && (room.Status != SchedulingLabels.Ready || room.Capacity < value.Capacity))
                return "Phòng học phải sẵn sàng và đủ sức chứa cho sĩ số tối đa của lớp.";
        }
        if(!value.ActualMeetings.IsDefault) foreach(var actual in value.ActualMeetings)
        {
            var room=Rooms.FirstOrDefault(r=>ScheduleRules.Same(r.Id,actual.RoomId));
            var teacher=Catalog.Lecturers.FirstOrDefault(l=>ScheduleRules.Same(l.Id,actual.LecturerId));
            if(actual.Date==default || semester is null || actual.Date<semester.StartDate || actual.Date>semester.EndDate
                || actual.StartPeriod is <1 or >12 || actual.EndPeriod<actual.StartPeriod || actual.EndPeriod>12
                || room is null || teacher is null) return "Ngày học thực tế không hợp lệ.";
            if(LifecycleRules.BlocksResources(value) && (room.Status!=SchedulingLabels.Ready || room.Capacity<value.Capacity
                || teacher.Status!="Đang giảng dạy")) return "Phòng/giảng viên của lịch thực tế không khả dụng.";
        }
        for (var index = 0; index < value.Meetings.Length; index++)
            if (value.Meetings.Skip(index + 1).Any(other => ScheduleRules.Overlaps(value.Meetings[index], other)))
                return "Các buổi học của cùng lớp không được trùng thời gian.";
        return !LifecycleRules.BlocksResources(value) ? null : ConflictError(value);
    }

    private string? ConflictError(CourseClass value)
    {
        foreach (var other in Classes.Where(item => !ScheduleRules.Same(item.Id,value.Id) && LifecycleRules.BlocksResources(item)))
        foreach (var requested in SchedulingQuery.ExpandActual(value,DateOnly.MinValue,DateOnly.MaxValue))
        foreach (var scheduled in SchedulingQuery.ExpandActual(other,requested.Date,requested.Date))
        {
            if (!ScheduleRules.Overlaps(requested.Meeting,scheduled.Meeting)) continue;
            if (ScheduleRules.Same(requested.RoomId,scheduled.RoomId)) return $"Trùng phòng {requested.RoomId} với lớp {other.Id}.";
            if (ScheduleRules.Same(requested.LecturerId,scheduled.LecturerId)) return $"Trùng lịch giảng viên với lớp {other.Id}.";
        }
        return null;
    }
}
