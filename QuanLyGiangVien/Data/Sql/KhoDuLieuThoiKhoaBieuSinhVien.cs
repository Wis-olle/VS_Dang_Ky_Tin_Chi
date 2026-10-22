using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data.Sql;

public sealed record StudentTimetableData(DatabaseIdentity Identity, ImmutableArray<Semester> Semesters,
    ImmutableArray<TeachingLecturer> Teachers, ImmutableArray<Course> Courses, ImmutableArray<Room> Rooms,
    ImmutableArray<CourseClass> Classes, ImmutableArray<DatabaseRegistration> Registrations)
{
    public ScheduleStore CreateStore() => new(TeachingCatalog.ForTimetable(Courses, Semesters, Teachers), Rooms, Classes);
}

/// <summary>Only the signed-in student's display data; this model has no personal lecturer fields or write repository.</summary>
public sealed class StudentTimetableRepository(string connectionString)
{
    // Đọc thời khóa biểu từ database
    public async Task<StudentTimetableData> LoadAsync(CancellationToken token = default)
    {
        try
        {
            await using var cn = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(cn, token).ConfigureAwait(false);
            using var cmd = new SqlCommand("dbo.usp_SinhVien_ThoiKhoaBieu", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
            await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            return Read(reader);
        }
        catch (SqlException error) { throw new PersistenceException(SqlErrorMessages.ForNumber(error.Number)); }
        catch (InvalidOperationException) { throw Invalid(); }
    }

    public static StudentTimetableData Read(DbDataReader r)
    {
        try
        {
            var users = Rows(r, x => new DatabaseIdentity(Text(x,"ma_tk"), Text(x,"ma_sv"), Text(x,"vai_tro"),
                Text(x,"ho_ten"), Text(x,"ma_lop"), Text(x,"ten_nganh")));
            if (users.Length != 1 || users[0].Role != "SINH_VIEN" || string.IsNullOrWhiteSpace(users[0].StudentId)) throw Invalid();
            Next(r);
            var terms = Rows(r, x => new Semester(Text(x,"ma_hk"), Text(x,"ten_hk"), Date(x,"ngay_hoc_bd"), Date(x,"ngay_hoc_kt")));
            Next(r);
            var teachers = Rows(r, x => new TeachingLecturer(Text(x,"ma_gv"), Text(x,"ho_ten"),
                Convert.ToBoolean(x["trang_thai"]) ? "Đang giảng dạy" : "Tạm nghỉ"));
            Next(r);
            var courses = Rows(r, x => new Course(Text(x,"ma_hp"), Text(x,"ten_hp"), Number(x,"so_tin_chi"), Text(x,"ten_khoa")));
            Next(r);
            var rooms = Rows(r, x => new Room(Text(x,"ma_phong"), Text(x,"toa_nha"), Number(x,"suc_chua"), Text(x,"loai_phong"),
                Convert.ToBoolean(x["trang_thai"]) ? SchedulingLabels.Ready : SchedulingLabels.Maintenance));
            Next(r);
            var classes = Rows(r, x => new CourseClass(Text(x,"ma_lhp"), Text(x,"ma_hp"), Text(x,"ma_hk"), Text(x,"ma_gv"),
                Number(x,"si_so_max"), Number(x,"si_so_hien_tai"), State(Text(x,"trang_thai")), [], Lifecycle: OptionalColumn(x,"lifecycle") ?? "REGISTRATION",
                ActualMeetings: LifecycleRules.ReadActualCalendar(OptionalColumn(x,"actual_meetings_json"))));
            Next(r);
            var meetings = Rows(r, x => (Id: Text(x,"ma_lhp"), Meeting: new ClassMeeting(Text(x,"ma_phong"),
                (DayOfWeek)(Number(x,"thu") == 8 ? 0 : Number(x,"thu") - 1), Number(x,"tiet_bd"), Number(x,"tiet_kt"), Date(x,"ngay_bd"), Date(x,"ngay_kt"))));
            Next(r);
            var registrations = Rows(r, x => new DatabaseRegistration(Text(x,"ma_sv"), Text(x,"ma_lhp"), Text(x,"trang_thai")));
            if (r.NextResult() || terms.IsEmpty || registrations.Any(x => x.StudentId != users[0].StudentId || x.Status is not ("DANG_KY" or "DA_HUY"))
                || classes.Any(x => !registrations.Any(d => d.ClassId == x.Id))
                || registrations.Any(x => !classes.Any(c => c.Id == x.ClassId))
                || teachers.Any(x => !classes.Any(c => c.LecturerId == x.Id || (!c.ActualMeetings.IsDefault && c.ActualMeetings.Any(m=>m.LecturerId==x.Id))))
                || meetings.Any(x => !classes.Any(c => c.Id == x.Id))) throw Invalid();
            var result = new StudentTimetableData(users[0], terms, teachers, courses, rooms,
                classes.Select(c => c with { Meetings = meetings.Where(x => x.Id == c.Id).Select(x => x.Meeting).ToImmutableArray() }).ToImmutableArray(), registrations);
            _ = result.CreateStore();
            return result;
        }
        catch (PersistenceException) { throw; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or InvalidCastException or IndexOutOfRangeException or OverflowException) { throw Invalid(); }
    }

    private static ImmutableArray<T> Rows<T>(DbDataReader reader, Func<DbDataReader,T> map)
    { var list = ImmutableArray.CreateBuilder<T>(); while (reader.Read()) list.Add(map(reader)); return list.ToImmutable(); }
    private static void Next(DbDataReader reader) { if (!reader.NextResult()) throw Invalid(); }
    private static string? OptionalColumn(DbDataReader r,string key) => Enumerable.Range(0,r.FieldCount).Any(i=>r.GetName(i)==key) ? Convert.ToString(r[key]) : null;
    private static string Text(DbDataReader r, string key) => Convert.ToString(r[key])?.Trim() ?? "";
    private static int Number(DbDataReader r, string key) => Convert.ToInt32(r[key]);
    private static DateOnly Date(DbDataReader r, string key) => DateOnly.FromDateTime(Convert.ToDateTime(r[key]));
    private static string State(string state) => state switch {
        "DANG_MO" or "FULL" => SchedulingLabels.Open, "DONG" => SchedulingLabels.Closed, "HUY" => SchedulingLabels.Canceled, _ => throw Invalid()
    };
    private static PersistenceException Invalid() => new("Không thể tải thời khóa biểu. Kiểm tra dữ liệu và bản cập nhật Campus.");
}
