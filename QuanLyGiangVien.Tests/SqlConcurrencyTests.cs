using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SqlConcurrencyTests
{
    [SqlConcurrencyFact]
    public async Task HueRegistrationCommitsBeforeMinhCanChangeTheClass()
    {
        var connection = Environment.GetEnvironmentVariable("MINH_DB_CONCURRENCY_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connection).InitialCatalog, StringComparison.Ordinal);
        using var hue = new SqlConnection(connection);
        using var minh = new SqlConnection(connection);
        using var observer = new SqlConnection(connection);
        await hue.OpenAsync(); await minh.OpenAsync(); await observer.OpenAsync();
        using var session = new SqlCommand("SELECT @@SPID;", hue);
        int hueSession = (short)(await session.ExecuteScalarAsync())!;
        using var readVersion = new SqlCommand("SELECT phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='TEST_CONCURRENT';", observer);
        var originalVersion = Convert.ToHexString((byte[])(await readVersion.ExecuteScalarAsync())!);
        using var registration = new SqlCommand("EXEC dbo.usp_DangKyHocPhan @ma_lhp='TEST_CONCURRENT';", hue) { CommandTimeout = 30 };
        var hueTask = registration.ExecuteNonQueryAsync();
        try
        {
            // Observe the actual server WAITFOR rather than buffered client messages.
            using var waiting = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@id AND wait_type=N'WAITFOR';", observer);
            waiting.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = hueSession;
            var observationClock = Stopwatch.StartNew();
            bool paused = false;
            while (observationClock.Elapsed < TimeSpan.FromSeconds(5))
            {
                if ((int)(await waiting.ExecuteScalarAsync())! == 1) { paused = true; break; }
                await Task.Delay(50);
            }
            Assert.True(paused, "Huệ must be paused inside its registration transaction before Minh starts");
            var mappingClock = Stopwatch.StartNew();
            using var mapping = new SqlCommand("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO' WHERE ten_dang_nhap=ORIGINAL_LOGIN();", minh);
            await mapping.ExecuteNonQueryAsync();
            var mappingWait = mappingClock.ElapsedMilliseconds;
            var item = new CourseClass("TEST_CONCURRENT", "IT102", "HK1-2627", "GV0009", 60, 0, SchedulingLabels.Open,
                ImmutableArray.Create(new ClassMeeting("C101", DayOfWeek.Sunday, 10, 12, new(2026, 9, 7), new(2026, 12, 27))), originalVersion);
            using var save = SqlMinhCommands.SaveClass(item, false);
            save.Connection = minh;
            var saveClock = Stopwatch.StartNew();
            var failure = await Assert.ThrowsAsync<SqlException>(() => save.ExecuteScalarAsync());
            var procedureWait = saveClock.ElapsedMilliseconds;
            await hueTask;
            Assert.Equal(52046, failure.Number);
            Assert.True(procedureWait >= 1000, $"Class procedure must wait for the registration; mapping={mappingWait}ms, procedure={procedureWait}ms");
            using var verify = new SqlCommand("SELECT COUNT(*) FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_lhp='TEST_CONCURRENT' AND d.trang_thai='DANG_KY' AND l.ma_hp='IT101';", minh);
            Assert.Equal(1, (int)(await verify.ExecuteScalarAsync())!);
            var output = Environment.GetEnvironmentVariable("MINH_DB_CONCURRENCY_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
                File.WriteAllText(output, JsonSerializer.Serialize(new { passed = true, mappingWaitMs = mappingWait, procedureWaitMs = procedureWait, expectedError = failure.Number }));
        }
        finally
        {
            await hueTask;
            using var cleanup = new SqlCommand("DROP TRIGGER IF EXISTS dbo.tr_Minh_Test_PauseHue;", minh);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}

public sealed class SqlConcurrencyFactAttribute : FactAttribute
{
    public SqlConcurrencyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINH_DB_CONCURRENCY_CONNECTION")))
            Skip = "Requires a disposable Test database prepared with Concurrency.Setup.sql.";
    }
}
