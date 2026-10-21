using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class SchedulingIntegrityIntegrationTests
{
    [SqlIntegrationFact]
    public async Task ResourceVersionsRejectStaleSaveAndDeleteAndReturnFreshTokens()
    {
        var connection = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var repository = new SqlMinhRepository(connection);
        var snapshot = await repository.LoadAsync();
        const string gv = "GV9896", roomId = "AUDIT9896", classId = "LHP9896";
        try
        {
            var lecturer = repository.SaveLecturer(new(gv, "Nguyễn Kiểm Thử", new(1988, 1, 1), "Nam",
                "audit9896@example.edu.vn", "0999999896", snapshot.Faculties[0].Name, "Thạc sĩ", "Đang giảng dạy", "portrait-01", 0), true);
            Assert.Equal(16, lecturer.Version.Length);
            var updatedLecturer = repository.SaveLecturer(lecturer with { Phone = "0999999897" }, false);
            Assert.NotEqual(lecturer.Version, updatedLecturer.Version);
            Assert.Throws<PersistenceException>(() => repository.SaveLecturer(lecturer with { Name = "Nguyễn Hồ Sơ Cũ" }, false));
            Assert.Throws<PersistenceException>(() => repository.DeleteLecturer(gv, lecturer.Version));

            var room = repository.SaveRoom(new(roomId, "Tòa kiểm thử", 501, SchedulingLabels.Hall, SchedulingLabels.Ready), true);
            var updatedRoom = repository.SaveRoom(room with { Capacity = 1000 }, false);
            Assert.NotEqual(room.Version, updatedRoom.Version);
            Assert.Throws<PersistenceException>(() => repository.SaveRoom(room with { Building = "Dữ liệu cũ" }, false));
            Assert.Throws<PersistenceException>(() => repository.DeleteRoom(roomId, room.Version));

            var item = repository.SaveClass(new(classId, snapshot.Courses[0].Id, snapshot.Semesters[0].Id,
                gv, 501, 0, SchedulingLabels.Open, []), true);
            var updated = repository.SaveClass(item with { Capacity = 1000 }, false);
            Assert.NotEqual(item.Version, updated.Version);
            Assert.Throws<PersistenceException>(() => repository.SaveClass(item with { Capacity = 600 }, false));
            Assert.Throws<PersistenceException>(() => repository.DeleteClass(classId, item.Version));

            var reloaded = await repository.LoadAsync();
            Assert.Equal(1000, reloaded.CreateStores().Scheduling.Rooms.Single(r => r.Id == roomId).Capacity);
            Assert.Equal(1000, reloaded.Classes.Single(c => c.Id == classId).Capacity);
            Assert.Equal("0999999897", reloaded.Lecturers.Single(g => g.Id == gv).Phone);
        }
        finally { await Cleanup(connection, classId, roomId, gv); }
    }

    [SqlIntegrationFact]
    public async Task CanceledHistoryProtectsClassAndCourseMetadataInSqlAndDomain()
    {
        var connection = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var repository = new SqlMinhRepository(connection);
        var snapshot = await repository.LoadAsync();
        const string classId = "LHP9895";
        var course = snapshot.Courses[0];
        try
        {
            var item = repository.SaveClass(new(classId, course.Id, snapshot.Semesters[0].Id,
                snapshot.Lecturers.First(g => g.Status == "Đang giảng dạy").Id, 30, 0, SchedulingLabels.Open, []), true);
            using var db = new SqlConnection(connection);
            await db.OpenAsync();
            using var insert = new SqlCommand("INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai,ngay_huy) SELECT TOP(1) ma_sv,@id,GETDATE(),'DA_HUY',GETDATE() FROM dbo.SINH_VIEN ORDER BY ma_sv;", db);
            insert.Parameters.AddWithValue("@id", classId);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            // Inserting canceled history does not update the class rowversion; history protection is independent of stale version checks.
            var otherCourse = snapshot.Courses.First(c => c.Id != course.Id);
            var failure = Assert.Throws<PersistenceException>(() => repository.SaveClass(item with { CourseId = otherCourse.Id }, false));
            Assert.Contains("lịch sử", failure.Message);
            Assert.Throws<PersistenceException>(() => repository.SaveClass(item with { SemesterId = snapshot.Semesters.First(t => t.Id != item.SemesterId).Id }, false));
            Assert.Throws<PersistenceException>(() => repository.SaveClass(item with { LecturerId = snapshot.Lecturers.First(g => g.Id != item.LecturerId && g.Status == "Đang giảng dạy").Id }, false));
            var term = snapshot.Semesters.Single(t => t.Id == item.SemesterId);
            Assert.Throws<PersistenceException>(() => repository.SaveClass(item with { Meetings = [new(snapshot.Rooms.First(r => r.Status == SchedulingLabels.Ready && r.Capacity >= 30).Id,
                DayOfWeek.Monday, 1, 3, term.StartDate, term.EndDate)] }, false));
            var loaded = await repository.LoadAsync();
            var historic = loaded.Classes.Single(c => c.Id == classId);
            Assert.True(historic.HasRegistrationHistory);
            Assert.False(loaded.CreateStores().Scheduling.TryDeleteClass(classId, out _));
            Assert.Throws<PersistenceException>(() => repository.DeleteClass(classId, historic.Version));
            foreach (var changeCredits in new[] { false, true })
            {
                using var edit = new SqlCommand("DECLARE @version binary(32),@name nvarchar(150),@tc tinyint,@type varchar(20),@min tinyint,@max tinyint,@faculty varchar(20),@active bit; SELECT @version=phien_ban,@name=ten_hp,@tc=so_tin_chi,@type=loai_hp,@min=ISNULL(tin_chi_min,so_tin_chi),@max=ISNULL(tin_chi_max,so_tin_chi),@faculty=ma_khoa,@active=trang_thai FROM dbo.vw_Phong_HocPhan WHERE ma_hp=@id; IF @credits=1 BEGIN SET @tc=CASE WHEN @tc=12 THEN 11 ELSE @tc+1 END; SET @min=@tc;SET @max=@tc; END ELSE SET @name=N'Lịch sử không được đổi'; EXEC dbo.usp_Phong_HocPhan_Luu @la_moi=0,@ma_hp=@id,@ten_hp=@name,@so_tin_chi=@tc,@loai_hp=@type,@tin_chi_min=@min,@tin_chi_max=@max,@trang_thai=@active,@ma_khoa=@faculty,@phien_ban=@version;", db);
                edit.Parameters.AddWithValue("@id", course.Id);
                edit.Parameters.AddWithValue("@credits", changeCredits);
                var catalogError = await Assert.ThrowsAsync<SqlException>(() => edit.ExecuteNonQueryAsync());
                Assert.Equal(52102, catalogError.Number);
            }
            Assert.Equal(course.Name, (await repository.LoadAsync()).Courses.Single(c => c.Id == course.Id).Name);
        }
        finally { await Cleanup(connection, classId, null, null); }
    }

    private static async Task Cleanup(string connection, string classId, string? roomId, string? lecturerId)
    {
        using var db = new SqlConnection(connection);
        await db.OpenAsync();
        using var command = new SqlCommand("DELETE a FROM dbo.SEAT_ALLOCATION a JOIN dbo.DANG_KY d ON d.ma_dk=a.registration_id WHERE d.ma_lhp=@class; DELETE dbo.DANG_KY WHERE ma_lhp=@class; DELETE dbo.LICH_HOC WHERE ma_lhp=@class; DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp=@class; DELETE dbo.PHONG_HOC WHERE ma_phong=@room; DELETE dbo.GIANG_VIEN WHERE ma_gv=@gv;", db);
        command.Parameters.AddWithValue("@class", classId);
        command.Parameters.AddWithValue("@room", (object?)roomId ?? DBNull.Value);
        command.Parameters.AddWithValue("@gv", (object?)lecturerId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }
}
