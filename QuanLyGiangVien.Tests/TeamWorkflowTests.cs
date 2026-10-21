using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Catalogs;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

/// <summary>One real Phong → Minh → Huệ → Minh flow in a fresh disposable database.</summary>
public sealed class TeamWorkflowTests
{
    private const string CourseId = "WF9801", SemesterId = "WF-HK3", ClassId = "LHP9801";
    private const string StudentId = "SV2024002", RejectedCourseId = "WF_FORBIDDEN";

    [TeamWorkflowFact]
    public async Task SharedCatalogClassRegistrationAndTimetablePersistThroughCancellation()
    {
        var connection = Environment.GetEnvironmentVariable("TEAM_WORKFLOW_CONNECTION")!;
        var settings = new SqlConnectionStringBuilder(connection);
        Assert.StartsWith("DangKyTinChi_Minh_Test_", settings.InitialCatalog, StringComparison.Ordinal);
        var minh = new SqlMinhRepository(connection);
        var initial = await minh.LoadAsync();
        Assert.Contains(initial.Identity.Role, new[] { "DAO_TAO", "QUAN_TRI" });
        Assert.DoesNotContain(initial.Classes, value => value.Id == ClassId);
        var catalogs = new CatalogRepository(connection);
        var hue = new RegistrationRepository(connection);
        var serverNow = await ServerVietnamTimeAsync(connection);
        var teachingStart = DateOnly.FromDateTime(serverNow);
        var teachingEnd = teachingStart.AddDays(21);
        var sunday = teachingStart.AddDays(((int)DayOfWeek.Sunday - (int)teachingStart.DayOfWeek + 7) % 7);
        long registrationId;

        try
        {
            // Phong creates the real shared catalog and opens a server-time registration period.
            using var facultyRows = await catalogs.FacultiesAsync();
            var faculty = facultyRows.AsEnumerable().First(row => Convert.ToBoolean(row["trang_thai"]));
            var course = new HocPhan { Ma = CourseId, Ten = "Học phần kiểm thử liên phân hệ", TinChi = 3,
                Min = 3, Max = 3, HoatDong = true, MaKhoa = Convert.ToString(faculty["ma_khoa"]) };
            await catalogs.SaveHocPhanAsync(course, true);
            await catalogs.SaveHocKyAsync(new HocKy {
                Ma = SemesterId, NamHoc = "2897-2898", Ky = 3,
                BatDau = serverNow.AddDays(-1), KetThuc = serverNow.AddDays(1), HanHuy = serverNow.AddDays(1),
                NgayHocBatDau = teachingStart.ToDateTime(TimeOnly.MinValue),
                NgayHocKetThuc = teachingEnd.ToDateTime(TimeOnly.MinValue)
            }, true);
            using (var semesters = await catalogs.HocKyAsync(SemesterId))
            {
                var semester = HocKy.FromRow(semesters.AsEnumerable().Single(row => (string)row["ma_hk"] == SemesterId));
                Assert.Equal("DONG", semester.TrangThai);
                await catalogs.SetHocKyStatusAsync(semester, "DANG_MO");
            }

            // Minh reads Phong's new records and saves an actual scheduled class through its store.
            var beforeClass = await minh.LoadAsync();
            Assert.Contains(beforeClass.Courses, value => value.Id == CourseId && value.Credits == 3);
            Assert.Contains(beforeClass.Semesters, value => value.Id == SemesterId && value.StartDate == teachingStart);
            var scheduling = beforeClass.CreateStores().Scheduling;
            var item = new CourseClass(ClassId, CourseId, SemesterId, "GV0009", 20, 0, SchedulingLabels.Open,
                [new("C101", DayOfWeek.Sunday, 10, 12, teachingStart, teachingEnd)]);
            Assert.True(scheduling.TrySaveClass(item, true, out var errors), string.Join("; ", errors.Values));
            Assert.Equal(0, (await minh.LoadAsync()).Classes.Single(value => value.Id == ClassId).Enrolled);

            // This changes only the test database mapping of the actual ORIGINAL_LOGIN().
            await SetSqlIdentityAsync(connection, "SINH_VIEN", StudentId);
            using (var user = await hue.NguoiDungAsync())
            {
                Assert.Equal(StudentId, user.Rows[0]["ma_sv"]);
                Assert.Equal("SINH_VIEN", user.Rows[0]["vai_tro"]);
            }
            using (var deadline = await hue.ThoiHanAsync(SemesterId))
            {
                Assert.True(Convert.ToBoolean(deadline.Rows[0]["cho_dang_ky"]));
                Assert.True(Convert.ToBoolean(deadline.Rows[0]["cho_huy"]));
            }
            using (var available = await hue.TraCuuAsync(SemesterId, "", false))
                Assert.Contains(available.AsEnumerable(), row => (string)row["ma_lhp"] == ClassId);
            using (var created = await hue.DangKyAsync(ClassId))
                registrationId = Convert.ToInt64(created.Rows[0]["ma_dk"]);
            Assert.True(registrationId > 0);

            // Minh's SQL snapshot and timetable consume Huệ's persisted registration immediately.
            var enrolled = await new StudentTimetableRepository(connection).LoadAsync();
            Assert.Equal(StudentId, enrolled.Identity.StudentId);
            Assert.Equal(1, enrolled.Classes.Single(value => value.Id == ClassId).Enrolled);
            var timetable = Timetable(enrolled, sunday);
            Assert.Equal(ClassId, Assert.Single(timetable.Classes).Id);
            Assert.Equal(3, timetable.TotalCredits);
            var occurrence = Assert.Single(timetable.Occurrences);
            Assert.Equal(sunday, occurrence.Date);
            Assert.Equal("C101", occurrence.RoomId);
            Assert.Equal(10, occurrence.Meeting.StartPeriod);
            Assert.Equal(12, occurrence.Meeting.EndPeriod);

            // A valid client request from the real student still fails the server role guard.
            var forbidden = await Assert.ThrowsAsync<PersistenceException>(() => catalogs.SaveHocPhanAsync(
                course with { Ma = RejectedCourseId, Ten = "Sinh viên không được tạo học phần" }, true));
            Assert.Contains("quyền", forbidden.Message, StringComparison.OrdinalIgnoreCase);

            using (var canceled = await hue.HuyAsync(registrationId))
                Assert.Equal(registrationId, Convert.ToInt64(canceled.Rows[0]["ma_dk"]));
            var afterCancellation = await new StudentTimetableRepository(connection).LoadAsync();
            Assert.Equal(0, afterCancellation.Classes.Single(value => value.Id == ClassId).Enrolled);
            var empty = Timetable(afterCancellation, sunday);
            Assert.Empty(empty.Classes);
            Assert.Empty(empty.Occurrences);
            Assert.Equal(0, empty.TotalCredits);
            using var history = await hue.KetQuaAsync(SemesterId, null, true);
            var retained = Assert.Single(history.AsEnumerable());
            Assert.Equal(registrationId, Convert.ToInt64(retained["ma_dk"]));
            Assert.Equal("DA_HUY", retained["trang_thai"]);
            Assert.False(retained.IsNull("ngay_huy"));
        }
        finally
        {
            await SetSqlIdentityAsync(connection, initial.Identity.Role, initial.Identity.StudentId);
        }

        // Keep the class and canceled registration in this disposable fixture; do not erase history.
        var management = await minh.LoadAsync();
        Assert.Equal(initial.Identity.Role, management.Identity.Role);
        Assert.Equal(0, management.Classes.Single(value => value.Id == ClassId).Enrolled);
        Assert.Contains(management.Registrations, value => value.StudentId == StudentId &&
            value.ClassId == ClassId && value.Status == "DA_HUY");
        using var rejected = await catalogs.HocPhanAsync(RejectedCourseId, null, null);
        Assert.Empty(rejected.AsEnumerable());
    }

    private static StudentTimetableSnapshot Timetable(StudentTimetableData snapshot, DateOnly sunday) =>
        StudentTimetable.Create(snapshot.CreateStore(), snapshot.Registrations.Select(value =>
            new StudentRegistration(value.StudentId, value.ClassId, value.Status == "DA_HUY")), StudentId, SemesterId, sunday);

    private static async Task<DateTime> ServerVietnamTimeAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT CONVERT(datetime2(0),DATEADD(HOUR,7,SYSUTCDATETIME()));", connection);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetSqlIdentityAsync(string connectionString, string role, string? studentId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("UPDATE dbo.TAI_KHOAN SET vai_tro=@role,ma_sv=@student WHERE ten_dang_nhap=ORIGINAL_LOGIN();", connection);
        command.Parameters.Add("@role", SqlDbType.VarChar, 20).Value = role;
        command.Parameters.Add("@student", SqlDbType.VarChar, 20).Value = (object?)studentId ?? DBNull.Value;
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}

public sealed class TeamWorkflowFactAttribute : FactAttribute
{
    public TeamWorkflowFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEAM_WORKFLOW_CONNECTION")))
            Skip = "Set TEAM_WORKFLOW_CONNECTION to a fresh disposable DangKyTinChi_Minh_Test_* database with all module migrations and demo seed.";
    }
}
