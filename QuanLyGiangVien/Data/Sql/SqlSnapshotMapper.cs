using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data.Sql;

public static class SqlSnapshotMapper
{
    public static DatabaseSnapshot Read(DbDataReader reader)
    {
        try
        {
            var identities = Rows(reader, r => new DatabaseIdentity(Text(r, "ma_tk"), Optional(r, "ma_sv"),
                Text(r, "vai_tro"), Text(r, "ho_ten"), Optional(r, "ma_lop"), OptionalColumn(r, "ten_nganh")));
            if (identities.Length != 1) throw InvalidData();
            Next(reader);
            var faculties = Rows(reader, r => new Faculty(Text(r, "ma_khoa"), Text(r, "ten_khoa")));
            Next(reader);
            var lecturers = Rows(reader, r => new Lecturer(Text(r, "ma_gv"), Text(r, "ho_ten"),
                Date(r, "ngay_sinh"), Text(r, "gioi_tinh"), Text(r, "email"), Text(r, "sdt"),
                Text(r, "ten_khoa"), Text(r, "hoc_vi"), Bit(r, "trang_thai") ? "Đang giảng dạy" : "Tạm nghỉ",
                Text(r, "avatar_key"), Number(r, "active_classes"), Version(r)));
            Next(reader);
            var courses = Rows(reader, r => new Course(Text(r, "ma_hp"), Text(r, "ten_hp"),
                Number(r, "so_tin_chi"), Text(r, "ten_khoa")));
            Next(reader);
            var semesters = Rows(reader, r => new Semester(Text(r, "ma_hk"), Text(r, "ten_hk"),
                DateOnly.FromDateTime(Date(r, "ngay_hoc_bd")), DateOnly.FromDateTime(Date(r, "ngay_hoc_kt"))));
            Next(reader);
            var rooms = Rows(reader, r => new Room(Text(r, "ma_phong"), Text(r, "toa_nha"), Number(r, "suc_chua"),
                Text(r, "loai_phong"), Bit(r, "trang_thai") ? SchedulingLabels.Ready : SchedulingLabels.Maintenance, Version(r)));
            Next(reader);
            var classes = Rows(reader, r => new CourseClass(Text(r, "ma_lhp"), Text(r, "ma_hp"), Text(r, "ma_hk"),
                Text(r, "ma_gv"), Number(r, "si_so_max"), Number(r, "si_so_hien_tai"),
                ClassStatus(Text(r, "trang_thai")), [], Version(r), Lifecycle: OptionalColumn(r,"lifecycle") ?? "REGISTRATION",
                ActualMeetings: LifecycleRules.ReadActualCalendar(OptionalColumn(r,"actual_meetings_json"))));
            Next(reader);
            var meetings = Rows(reader, r => (ClassId: Text(r, "ma_lhp"), Meeting: new ClassMeeting(Text(r, "ma_phong"),
                Weekday(Number(r, "thu")), Number(r, "tiet_bd"), Number(r, "tiet_kt"),
                DateOnly.FromDateTime(Date(r, "ngay_bd")), DateOnly.FromDateTime(Date(r, "ngay_kt")))));
            Next(reader);
            var registrations = Rows(reader, r => new DatabaseRegistration(Text(r, "ma_sv"), Text(r, "ma_lhp"),
                RegistrationStatus(Text(r, "trang_thai"))));
            if (reader.NextResult()) throw InvalidData();
            if (meetings.Any(item => !classes.Any(c => Same(c.Id, item.ClassId)))) throw InvalidData();
            var snapshot = new DatabaseSnapshot(identities[0], faculties, lecturers, courses, semesters, rooms,
                classes.Select(c => c with { HasRegistrationHistory = registrations.Any(r => Same(r.ClassId, c.Id)), Meetings = meetings.Where(m => Same(m.ClassId, c.Id))
                    .Select(m => m.Meeting).ToImmutableArray() }).ToImmutableArray(), registrations);
            Validate(snapshot);
            return snapshot;
        }
        catch (PersistenceException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or FormatException
            or OverflowException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw InvalidData();
        }
    }

    private static void Validate(DatabaseSnapshot value)
    {
        if (value.Semesters.IsEmpty)
            throw new PersistenceException("Chưa có học kỳ trong SQL. Vui lòng bổ sung học kỳ và khoảng ngày học trước khi mở ứng dụng.");
        if (!new[] { "DAO_TAO", "QUAN_TRI", "SINH_VIEN" }.Contains(value.Identity.Role)
            || value.Identity.Role == "SINH_VIEN" && string.IsNullOrWhiteSpace(value.Identity.StudentId)
            || Duplicates(value.Faculties.Select(f => f.Id))) throw InvalidData();
        var lecturerValidation = new LecturerStore([]);
        foreach (var lecturer in value.Lecturers)
            if (!lecturerValidation.TrySave(lecturer, true, out _)) throw InvalidData();
        var stores = value.CreateStores();
        foreach (var registration in value.Registrations)
            if (!value.Classes.Any(c => Same(c.Id, registration.ClassId))) throw InvalidData();
        if (Duplicates(value.Registrations.Where(r => r.Status == "DANG_KY")
            .Select(r => r.StudentId + "\u001f" + r.ClassId))) throw InvalidData();
        if (value.Identity.Role != "SINH_VIEN") foreach (var item in value.Classes)
        {
            var actual = value.Registrations.Count(r => Same(r.ClassId, item.Id) && r.Status == "DANG_KY");
            if (actual != item.Enrolled) throw InvalidData();
        }
        if (value.Identity.Role != "SINH_VIEN") foreach (var lecturer in value.Lecturers)
            if (lecturer.ActiveClasses != value.Classes.Count(c => Same(c.LecturerId, lecturer.Id)
                && LifecycleRules.BlocksResources(c))) throw InvalidData();
        _ = stores;
    }

    private static ImmutableArray<T> Rows<T>(DbDataReader reader, Func<DbDataReader, T> map)
    {
        var result = ImmutableArray.CreateBuilder<T>();
        while (reader.Read()) result.Add(map(reader));
        return result.ToImmutable();
    }
    private static string Version(DbDataReader reader) => Enumerable.Range(0, reader.FieldCount)
        .Any(i => reader.GetName(i) == "phien_ban") && reader["phien_ban"] is byte[] { Length: 8 } bytes
        ? Convert.ToHexString(bytes) : "";
    private static void Next(DbDataReader reader) { if (!reader.NextResult()) throw InvalidData(); }
    private static string? Optional(DbDataReader reader, string name) => reader[name] is DBNull ? null
        : Convert.ToString(reader[name], CultureInfo.InvariantCulture)?.Trim();
    private static string? OptionalColumn(DbDataReader reader, string name) => Enumerable.Range(0, reader.FieldCount)
        .Any(index => reader.GetName(index) == name) ? Optional(reader, name) : null;
    private static string Text(DbDataReader reader, string name) => Optional(reader, name) is { Length: > 0 } value
        ? value : throw InvalidData();
    private static int Number(DbDataReader reader, string name) => Convert.ToInt32(reader[name], CultureInfo.InvariantCulture);
    private static DateTime Date(DbDataReader reader, string name) => Convert.ToDateTime(reader[name], CultureInfo.InvariantCulture);
    private static bool Bit(DbDataReader reader, string name) => reader[name] switch {
        bool value => value, byte value when value <= 1 => value == 1,
        int value when value is 0 or 1 => value == 1, _ => throw InvalidData() };
    private static DayOfWeek Weekday(int value) => value is >= 2 and <= 8
        ? value == 8 ? DayOfWeek.Sunday : (DayOfWeek)(value - 1) : throw InvalidData();
    private static string ClassStatus(string value) => value switch {
        "DANG_MO" or "FULL" => SchedulingLabels.Open, "DONG" => SchedulingLabels.Closed,
        "HUY" => SchedulingLabels.Canceled, _ => throw InvalidData() };
    private static string RegistrationStatus(string value) => value is "DANG_KY" or "DA_HUY" ? value : throw InvalidData();
    private static bool Duplicates(IEnumerable<string> values) => values.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1);
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static PersistenceException InvalidData() => new("Dữ liệu SQL chưa hợp lệ hoặc chưa tương thích. Vui lòng kiểm tra script tích hợp và dữ liệu chung của nhóm.");
}
