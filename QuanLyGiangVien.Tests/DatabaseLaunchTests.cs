using QuanLyGiangVien.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class DatabaseLaunchTests
{
    [Fact]
    public void MainVisualStudioProfileOpensLoginUsingSql()
    {
        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "launchSettings.json")));
        var profile = settings.RootElement.GetProperty("profiles").GetProperty("QuanLyGiangVien");
        var args = profile.GetProperty("commandLineArgs").GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain("--start-lecturers", args);
        Assert.Contains("--database", args);
        Assert.Contains("--showcase", args);
        Assert.DoesNotContain("--demo", args);
        Assert.False(profile.TryGetProperty("environmentVariables", out _));
    }

    [Fact]
    public void WorkingDataProfileKeepsExplicitMainDatabase()
    {
        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "launchSettings.json")));
        var profile = settings.RootElement.GetProperty("profiles").GetProperty("CampusDuLieuLamViec");
        Assert.Equal("--database", profile.GetProperty("commandLineArgs").GetString());
        Assert.Equal("DangKyTinChi_Minh", new SqlConnectionStringBuilder(profile.GetProperty("environmentVariables")
            .GetProperty("MINH_DB_CONNECTION").GetString()).InitialCatalog);
    }

    [Fact]
    public void NormalLaunchUsesLocalDatabase()
    {
        var options = DatabaseLaunch.Parse([], null, false);
        Assert.True(options.UseDatabase);
        Assert.Contains("DangKyTinChi_Minh", options.ConnectionString);
        Assert.Contains("Integrated Security=True", options.ConnectionString);
        Assert.Equal(".", new SqlConnectionStringBuilder(options.ConnectionString).DataSource);
    }

    [Fact]
    public void DemoAndNativeHarnessDoNotTouchDatabase()
    {
        Assert.False(DatabaseLaunch.Parse(["--demo"], null, false).UseDatabase);
        Assert.False(DatabaseLaunch.Parse([], null, true).UseDatabase);
    }

    [Fact]
    public void ExplicitDatabaseOverridesHarnessDefault()
    {
        Assert.True(DatabaseLaunch.Parse(["--database"], null, true).UseDatabase);
    }

    [Fact]
    public void ConflictingModesFailClearly()
    {
        Assert.Throws<ArgumentException>(() => DatabaseLaunch.Parse(["--demo", "--database"], null, false));
    }

    [Fact]
    public void ConnectionIsTakenFromEnvironmentWithoutBeingLogged()
    {
        const string connection = "Server=localhost;Database=Other;Integrated Security=True";
        var options = DatabaseLaunch.Parse([], connection, false);
        Assert.Equal(connection, options.ConnectionString);
        Assert.DoesNotContain(connection, options.ToString());
    }

    [Fact]
    public void ExplicitInstanceIsNeverReplacedByLocalDefault()
    {
        const string connection = "Server=.\\SQLEXPRESS;Database=Other;Integrated Security=True";
        Assert.Equal(connection, DatabaseLaunch.Parse([], connection, false).ConnectionString);
    }
}
