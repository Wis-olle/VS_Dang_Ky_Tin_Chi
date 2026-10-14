using QuanLyGiangVien.Data;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class DatabaseLaunchTests
{
    [Fact]
    public void NormalLaunchUsesLocalDatabase()
    {
        var options = DatabaseLaunch.Parse([], null, false);
        Assert.True(options.UseDatabase);
        Assert.Contains("DangKyTinChi_Minh", options.ConnectionString);
        Assert.Contains("Integrated Security=True", options.ConnectionString);
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
}
