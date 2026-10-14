using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class SemesterAlignmentIntegrationTests
{
    private static string Connection
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
            Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(value).InitialCatalog, StringComparison.Ordinal);
            return value;
        }
    }

    [SqlIntegrationFact]
    public async Task DefaultSemesterUsesServerTeachingDatesAndFallsBackToUpcomingThenRecentPast()
    {
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cases = new[] {
                (Start1: -5, End1: 5, Start2: -20, End2: 20, Expected: "HK1-2627"),
                (Start1: 0, End1: 0, Start2: 1, End2: 30, Expected: "HK1-2627"),
                (Start1: 5, End1: 10, Start2: 20, End2: 30, Expected: "HK1-2627"),
                (Start1: -20, End1: -10, Start2: -5, End2: -1, Expected: "HK2-2627")
            };
            foreach (var item in cases)
            {
                using var update = new SqlCommand("""
                    DECLARE @today date=CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME()));
                    UPDATE dbo.HOC_KY SET ngay_hoc_bd=DATEADD(DAY,CASE WHEN ma_hk='HK1-2627' THEN @start1 ELSE @start2 END,@today),
                    ngay_hoc_kt=DATEADD(DAY,CASE WHEN ma_hk='HK1-2627' THEN @end1 ELSE @end2 END,@today)
                    WHERE ma_hk IN ('HK1-2627','HK2-2627');
                    """, connection, transaction);
                update.Parameters.Add("@start1", SqlDbType.Int).Value = item.Start1;
                update.Parameters.Add("@end1", SqlDbType.Int).Value = item.End1;
                update.Parameters.Add("@start2", SqlDbType.Int).Value = item.Start2;
                update.Parameters.Add("@end2", SqlDbType.Int).Value = item.End2;
                Assert.Equal(2, await update.ExecuteNonQueryAsync());
                using var query = new SqlCommand("dbo.usp_LayHocKy", connection, transaction) { CommandType = CommandType.StoredProcedure };
                await using var reader = await query.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(item.Expected, reader["ma_hk"]);
            }
        }
        finally { transaction.Rollback(); }
    }

    [SqlIntegrationFact]
    public async Task HueActiveResultsAndMinhTimetableContainTheSameClassesAndCreditsInEverySemester()
    {
        var snapshot = await new SqlMinhRepository(Connection).LoadAsync();
        var hue = new RegistrationRepository(Connection);
        var store = snapshot.CreateStores().Scheduling;
        var registrations = snapshot.Registrations.Select(value =>
            new StudentRegistration(value.StudentId, value.ClassId, value.Status == "DA_HUY")).ToArray();
        foreach (var student in new[] { "SV2024001", "SV2024002" })
        foreach (var term in snapshot.Semesters)
        {
            using var results = await hue.KetQuaAsync(term.Id, student, false);
            var calendar = StudentTimetable.Create(store, registrations, student, term.Id, term.StartDate);
            Assert.Equal(results.AsEnumerable().Select(row => (string)row["ma_lhp"]).Order(),
                calendar.Classes.Select(value => value.Id).Order());
            Assert.Equal(results.AsEnumerable().Sum(row => Convert.ToInt32(row["so_tin_chi"])), calendar.TotalCredits);
        }
    }
}
