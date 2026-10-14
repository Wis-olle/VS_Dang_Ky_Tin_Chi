using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class RegistrationIntegrationTests
{
    [SqlIntegrationFact]
    public async Task OriginalHueReadsShareMinhClassesCountsAndResults()
    {
        var connection = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        var snapshot = await new SqlMinhRepository(connection).LoadAsync();
        var registration = new RegistrationRepository(connection);
        using var user = await registration.NguoiDungAsync();
        Assert.Equal(snapshot.Identity.AccountId, Convert.ToString(user.Rows[0]["ma_tk"]));
        using var terms = await registration.HocKyAsync();
        Assert.Equal(snapshot.Semesters.Length, terms.Rows.Count);
        Assert.True(terms.Columns.Contains("ngay_hoc_bd") && terms.Columns.Contains("ngay_hoc_kt"),
            "Hue semester choices must expose the same teaching dates as the timetable.");
        foreach (DataRow term in terms.Rows)
        {
            var calendarTerm = snapshot.Semesters.Single(value => value.Id == (string)term["ma_hk"]);
            Assert.Equal(calendarTerm.Name, (string)term["ten_hk"]);
            Assert.Equal(calendarTerm.StartDate, DateOnly.FromDateTime((DateTime)term["ngay_hoc_bd"]));
            Assert.Equal(calendarTerm.EndDate, DateOnly.FromDateTime((DateTime)term["ngay_hoc_kt"]));
        }
        foreach (var semester in snapshot.Semesters)
        {
            using var deadline = await registration.ThoiHanAsync(semester.Id);
            Assert.Single(deadline.Rows.Cast<DataRow>());
            Assert.IsType<DateTime>(deadline.Rows[0]["gio_may_chu"]);
            using var stats = await registration.ThongKeAsync(semester.Id, null);
            foreach (DataRow row in stats.Rows)
            {
                var item = snapshot.Classes.Single(value => value.Id == Convert.ToString(row["ma_lhp"]));
                Assert.Equal(item.Enrolled, Convert.ToInt32(row["si_so_hien_tai"]));
                using var roster = await registration.DanhSachAsync(item.Id);
                Assert.Equal(item.Enrolled, roster.Rows.Count);
            }
            using var lookup = await registration.TraCuuAsync(semester.Id, "", false);
            Assert.NotEmpty(lookup.Rows.Cast<DataRow>());
            Assert.All(lookup.Rows.Cast<DataRow>(), row => Assert.Contains(Convert.ToString(row["trang_thai"]), new[] { "DANG_MO", "FULL" }));
            using var literalSearch = await registration.TraCuuAsync(semester.Id, "%_", false);
            Assert.Empty(literalSearch.Rows.Cast<DataRow>());
        }
        var first = snapshot.Registrations.First();
        var termId = snapshot.Classes.Single(value => value.Id == first.ClassId).SemesterId;
        using var results = await registration.KetQuaAsync(termId, first.StudentId, true);
        Assert.Contains(results.Rows.Cast<DataRow>(), row => Convert.ToString(row["ma_lhp"]) == first.ClassId);
    }

    [SqlIntegrationFact]
    public async Task ManagementLoginCannotRegisterOrCancelThroughRepository()
    {
        var connection = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        var repository = new RegistrationRepository(connection);
        using var user = await repository.NguoiDungAsync();
        Assert.Contains(Convert.ToString(user.Rows[0]["vai_tro"]), new[] { "DAO_TAO", "QUAN_TRI" });
        var create = await Assert.ThrowsAsync<PersistenceException>(() => repository.DangKyAsync("LHP0001"));
        Assert.Contains("sinh viên", create.Message);
        var cancel = await Assert.ThrowsAsync<PersistenceException>(() => repository.HuyAsync(1));
        Assert.Contains("sinh viên", cancel.Message);
    }
}
