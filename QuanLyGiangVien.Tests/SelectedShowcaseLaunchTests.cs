using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SelectedShowcaseLaunchTests
{
    [Theory]
    [InlineData("--demo")]
    [InlineData("--DEMO")]
    public void RejectsInMemoryModeForSelectedShowcase(string argument)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SelectedShowcaseLaunch.EnsureCompatible(["--showcase", argument], []));
        Assert.Contains("Mo-BanTrinhDien", error.Message);
    }

    [Theory]
    [InlineData("CAMPUS_FULL_DEMO_UI_CHECK")]
    [InlineData("campus_full_demo_ui_check")]
    [InlineData("Other_Ui_ChEcK")]
    public void RejectsActiveNativeHarnessKeysRegardlessOfCasing(string key)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SelectedShowcaseLaunch.EnsureCompatible(["--showcase", "--database"], [key]));
        Assert.DoesNotContain(key, error.Message); Assert.Contains("Mo-BanTrinhDien", error.Message);
    }

    [Fact]
    public void AllowsReadOnlyShowcaseSmokeAndOrdinaryProcessKeys()
    {
        SelectedShowcaseLaunch.EnsureCompatible(["--showcase", "--database"],
            ["CAMPUS_SELECTED_SHOWCASE_VERIFY", "CAMPUS_SELECTED_SHOWCASE_DATABASE", "PATH"]);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("localhost")]
    [InlineData("(local)")]
    public void LoadsExactRetainedFullDemoWithoutExecutable(string server)
    {
        using var fixture = new Fixture(); fixture.Write("server", server);
        var launch = SelectedShowcaseLaunch.Load(fixture.Search);
        var connection = new SqlConnectionStringBuilder(launch.ConnectionString);
        Assert.Equal(server, connection.DataSource); Assert.Equal(fixture.Database, connection.InitialCatalog);
        Assert.True(connection.IntegratedSecurity); Assert.True(connection.TrustServerCertificate);
        Assert.Equal(5, connection.ConnectTimeout); Assert.Equal(fixture.Quick, launch.QuickLoginFile);
        Assert.DoesNotContain(launch.ConnectionString, launch.ToString());
    }

    [Fact]
    public void WalkingParentsIsReadOnlyAndDoesNotChangeProcessConfiguration()
    {
        using var fixture = new Fixture();
        var connection = Environment.GetEnvironmentVariable("MINH_DB_CONNECTION");
        var quick = Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE");
        var before = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, p => (Text: File.ReadAllText(p), Written: File.GetLastWriteTimeUtc(p)));
        SelectedShowcaseLaunch.Load(fixture.Search);
        Assert.Equal(before.Count, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Length);
        foreach (var entry in before) { Assert.Equal(entry.Value.Text, File.ReadAllText(entry.Key)); Assert.Equal(entry.Value.Written, File.GetLastWriteTimeUtc(entry.Key)); }
        Assert.Equal(connection, Environment.GetEnvironmentVariable("MINH_DB_CONNECTION"));
        Assert.Equal(quick, Environment.GetEnvironmentVariable("CAMPUS_QUICK_LOGIN_FILE"));
    }

    [Theory]
    [InlineData("passed", false)]
    [InlineData("synthetic", false)]
    [InlineData("fullData", false)]
    [InlineData("keepDatabase", false)]
    [InlineData("cleaned", true)]
    public void RejectsIncompleteOrRemovedDemo(string property, bool value)
    { using var fixture = new Fixture(); fixture.Write(property, value); Reject(fixture.Search); }

    [Theory]
    [InlineData("server", "remote.example.test;Password=secret-marker")]
    [InlineData("server", @".\SQLEXPRESS")]
    [InlineData("server", "tcp:localhost")]
    [InlineData("database", "DangKyTinChi_Minh")]
    [InlineData("database", "DangKyTinChi_Minh_Test_Showcase_FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [InlineData("database", "DangKyTinChi_Minh_Test_Showcase_00000000000000000000000000000000")]
    [InlineData("passed", "true")]
    public void RejectsUntrustedManifestValuesWithoutDisclosingThem(string property, string value)
    { using var fixture = new Fixture(); fixture.Write(property, value); Reject(fixture.Search); }

    [Theory]
    [InlineData("output")]
    [InlineData("quickLoginFile")]
    public void RejectsPathsOutsideExactRunFolder(string property)
    { using var fixture = new Fixture(); fixture.Write(property, Path.Combine(fixture.Root, "secret-marker")); Reject(fixture.Search); }

    [Theory]
    [InlineData("")]
    [InlineData("not-json-secret-marker")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"passed\":true,\"passed\":false}")]
    public void RejectsMalformedOrMissingManifestFields(string json)
    { using var fixture = new Fixture(); File.WriteAllText(fixture.Manifest, json); Reject(fixture.Search); }

    [Theory]
    [InlineData("")]
    [InlineData("relative/manifest.json")]
    [InlineData("secret-marker")]
    public void RejectsEmptyOrRelativePointers(string pointer)
    { using var fixture = new Fixture(); File.WriteAllText(fixture.Pointer, pointer); Reject(fixture.Search); }

    [Theory]
    [InlineData("pointer")]
    [InlineData("manifest")]
    [InlineData("quick")]
    [InlineData("marker")]
    public void RejectsMissingRequiredFiles(string file)
    {
        using var fixture = new Fixture();
        File.Delete(file switch { "pointer" => fixture.Pointer, "manifest" => fixture.Manifest, "quick" => fixture.Quick, _ => fixture.Marker });
        Reject(fixture.Search);
    }

    [Fact]
    public void RejectsPointerOutsideShowcaseNamespaceBeforeReadingManifest()
    {
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "secret-marker.json"); File.WriteAllText(outside, "sensitive-content-marker");
        File.WriteAllText(fixture.Pointer, outside); Reject(fixture.Search);
    }

    [Fact]
    public void RejectsWrongQuickFileNameEvenInsideRunFolder()
    { using var fixture = new Fixture(); fixture.Write("quickLoginFile", fixture.Manifest); Reject(fixture.Search); }

    [Fact]
    public void RejectsInvalidSearchDirectory()
    { Reject(""); Reject("\0"); }

    [Fact]
    public void RejectsSymbolicQuickFileWhenPlatformAllowsCreatingLinks()
    {
        using var fixture = new Fixture();
        var target = Path.Combine(fixture.Root, "secret-marker.dat"); File.WriteAllText(target, "sensitive-content-marker");
        File.Delete(fixture.Quick);
        try { File.CreateSymbolicLink(fixture.Quick, target); }
        catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException or IOException) { return; }
        Reject(fixture.Search);
    }

    private static void Reject(string search)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SelectedShowcaseLaunch.Load(search));
        Assert.Contains("Mo-BanTrinhDien", error.Message); Assert.Contains("QuanLyGiangVien", error.Message);
        Assert.DoesNotContain("secret-marker", error.Message); Assert.DoesNotContain("sensitive-content-marker", error.Message);
        Assert.DoesNotContain("Password", error.Message); Assert.Null(error.InnerException);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "SelectedShowcaseLaunchTests", Guid.NewGuid().ToString("N"));
        public string Search => Path.Combine(Root, "QuanLyGiangVien", "bin", "Debug");
        public string Marker => Path.Combine(Root, "QuanLyGiangVien", "QuanLyGiangVien.csproj");
        public string Pointer => Path.Combine(Root, ".tmp", "showcase-20", "current-manifest-path.txt");
        public string Run => Path.Combine(Root, ".tmp", "showcase-20", "0123456789abcdef0123456789abcdef");
        public string Manifest => Path.Combine(Run, "manifest.json");
        public string Quick => Path.Combine(Run, "quick-login.dat");
        public string Database => "DangKyTinChi_Minh_Test_Showcase_0123456789abcdef0123456789abcdef";
        public Fixture()
        {
            Directory.CreateDirectory(Search); Directory.CreateDirectory(Run);
            File.WriteAllText(Marker, "<Project />"); File.WriteAllText(Quick, "opaque-dpapi-fixture");
            File.WriteAllText(Pointer, Manifest); Write();
        }
        public void Write(string? property = null, object? value = null)
        {
            var fields = new Dictionary<string, object> { ["database"] = Database, ["server"] = ".", ["passed"] = true,
                ["synthetic"] = true, ["fullData"] = true, ["keepDatabase"] = true, ["cleaned"] = false,
                ["output"] = Run, ["quickLoginFile"] = Quick };
            var replacement = fields.ToDictionary(p => p.Key, p => p.Key == property ? value : p.Value);
            File.WriteAllText(Manifest, JsonSerializer.Serialize(replacement));
        }
        public void Dispose()
        {
            var prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SelectedShowcaseLaunchTests")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(Root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test fixture path.");
            Directory.Delete(Root, true);
        }
    }
}
