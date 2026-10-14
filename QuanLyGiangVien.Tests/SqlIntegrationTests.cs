using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class SqlIntegrationTests
{
    [SqlIntegrationFact]
    public async Task RepositoryCrudPersistsAcrossReloadsAndRejectedWriteKeepsStoreSnapshot()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        var configuration = new SqlConnectionStringBuilder(connectionString);
        Assert.StartsWith("DangKyTinChi_Minh_Test_", configuration.InitialCatalog, StringComparison.Ordinal);
        var repository = new SqlMinhRepository(connectionString);
        var initial = await repository.LoadAsync();
        Assert.Contains(initial.Identity.Role, new[] { "DAO_TAO", "QUAN_TRI" });
        const string lecturerId = "GV9901", roomId = "ZDBTEST", classId = "LHP9901";
        Assert.DoesNotContain(initial.Lecturers, v => v.Id == lecturerId);
        Assert.DoesNotContain(initial.Rooms, v => v.Id == roomId);
        Assert.DoesNotContain(initial.Classes, v => v.Id == classId);
        bool lecturerAdded = false, roomAdded = false, classAdded = false;
        try
        {
            var stores = initial.CreateStores();
            var lecturer = new Lecturer(lecturerId, "Nguyễn Kiểm Thử", new DateTime(1988, 1, 1), "Nam",
                "minh-db-integration9901@example.edu.vn", "0999999001", initial.Faculties[0].Name,
                "Thạc sĩ", "Đang giảng dạy", "portrait-01", 0);
            Assert.True(stores.Lecturers.TrySave(lecturer, true, out var lecturerErrors), string.Join("; ", lecturerErrors.Values));
            lecturerAdded = true;
            var room = new Room(roomId, "Tòa kiểm thử", 50, SchedulingLabels.Theory, SchedulingLabels.Ready);
            Assert.True(stores.Scheduling.TrySaveRoom(room, true, out var roomErrors), string.Join("; ", roomErrors.Values));
            roomAdded = true;
            var item = new CourseClass(classId, initial.Courses[0].Id, initial.Semesters[0].Id,
                lecturerId, 40, 0, SchedulingLabels.Open, []);
            Assert.True(stores.Scheduling.TrySaveClass(item, true, out var classErrors), string.Join("; ", classErrors.Values));
            classAdded = true;
            var persisted = await new SqlMinhRepository(connectionString).LoadAsync();
            Assert.Contains(persisted.Lecturers, v => v.Id == lecturerId);
            Assert.Contains(persisted.Rooms, v => v.Id == roomId);
            Assert.Contains(persisted.Classes, v => v.Id == classId);

            // A concurrent insert passes the old in-memory validation but must fail on SQL.
            var stale = initial.CreateStores().Lecturers;
            var prior = stale.Items;
            Assert.False(stale.TrySave(lecturer, true, out var conflict));
            Assert.Contains("_global", conflict.Keys);
            Assert.Same(prior, stale.Items);
            Assert.Single((await repository.LoadAsync()).Lecturers.Where(v => v.Id == lecturerId));

            Assert.True(stores.Scheduling.TrySaveRoom(room with { Building = "Tòa đã sửa" }, false, out _));
            Assert.Equal("Tòa đã sửa", (await repository.LoadAsync()).Rooms.Single(v => v.Id == roomId).Building);
            Assert.True(stores.Scheduling.TryDeleteClass(classId, out _));
            classAdded = false;
            Assert.True(stores.Scheduling.TryDeleteRoom(roomId, out _));
            roomAdded = false;
            // Assignment counters reflect the canonical snapshot before deleting the lecturer.
            var fresh = (await repository.LoadAsync()).CreateStores();
            Assert.True(fresh.Lecturers.TryDelete(lecturerId, out _));
            lecturerAdded = false;
            var final = await repository.LoadAsync();
            Assert.DoesNotContain(final.Classes, v => v.Id == classId);
            Assert.DoesNotContain(final.Rooms, v => v.Id == roomId);
            Assert.DoesNotContain(final.Lecturers, v => v.Id == lecturerId);
        }
        finally
        {
            if (classAdded) repository.DeleteClass(classId);
            if (roomAdded) repository.DeleteRoom(roomId);
            if (lecturerAdded) repository.DeleteLecturer(lecturerId);
        }
    }
}

public sealed class SqlIntegrationFactAttribute : FactAttribute
{
    public SqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")))
            Skip = "Set MINH_DB_TEST_CONNECTION to a disposable DangKyTinChi_Minh_Test_* database initialized with the SQL scripts.";
    }
}
