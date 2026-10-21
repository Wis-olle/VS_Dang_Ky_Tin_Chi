using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class QuickLoginCredentialsTests : IDisposable
{
    private const string Connection = "Server=.;Database=DangKyTinChi_Minh;Integrated Security=true;TrustServerCertificate=true";
    private const string TestPassword = "Fixture-only-password-781!";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Campus-quick-login-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(directory, "credentials.dat");

    public QuickLoginCredentialsTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Missing_user_file_uses_the_encrypted_file_beside_the_application()
    {
        string userFile = Path.Combine(directory, "user.dat");
        Assert.Equal(FilePath, QuickLoginCredentials.ResolvePath(null, userFile, FilePath));
        Assert.Equal(FilePath, QuickLoginCredentials.ResolvePath(" ", userFile, FilePath));
    }

    [Fact]
    public void Existing_user_file_and_explicit_override_take_precedence()
    {
        Write();
        string bundledFile = Path.Combine(directory, "bundled.dat");
        Assert.Equal(FilePath, QuickLoginCredentials.ResolvePath(null, FilePath, bundledFile));
        string missingOverride = Path.Combine(directory, "missing-override.dat");
        Assert.Equal(missingOverride, QuickLoginCredentials.ResolvePath(missingOverride, FilePath, bundledFile));
    }

    [Fact]
    public void Missing_file_has_a_safe_specific_status()
    {
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath, out var status));
        Assert.Equal(QuickLoginStatus.MissingConfiguration, status);
    }

    [Fact]
    public void Wrong_database_and_corrupt_file_have_distinct_statuses()
    {
        Write(database: "DifferentDatabase");
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath, out var status));
        Assert.Equal(QuickLoginStatus.DifferentDatabase, status);
        File.WriteAllBytes(FilePath, [1, 2, 3]);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath, out status));
        Assert.Equal(QuickLoginStatus.CannotDecrypt, status);
    }

    [Fact]
    public void Valid_configuration_reports_ready_without_disclosing_secrets()
    {
        Write();
        Assert.NotNull(QuickLoginCredentials.Load(Connection, FilePath, out var status));
        Assert.Equal(QuickLoginStatus.Ready, status);
        Assert.DoesNotContain(TestPassword, status.ToString());
    }

    [Fact]
    public void Protected_pair_loads_for_matching_database_and_each_portal()
    {
        Write();
        var result = QuickLoginCredentials.Load(Connection, FilePath);
        Assert.NotNull(result);
        Assert.Equal("sv2024001", result.For(LoginPortal.Student)?.Username);
        Assert.Equal("daotao", result.For(LoginPortal.Training)?.Username);
        Assert.Equal(TestPassword, result.For(LoginPortal.Student)?.Password);
        Assert.Null(result.For((LoginPortal)999));
        Assert.DoesNotContain(TestPassword, result.For(LoginPortal.Student)!.ToString());
        Assert.DoesNotContain(TestPassword, result.ToString());
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("(local)")]
    public void Canonical_local_aliases_match(string server)
    {
        Write();
        Assert.NotNull(QuickLoginCredentials.Load(Connection.Replace("Server=.;", $"Server={server};"), FilePath));
    }

    [Fact]
    public void Computer_name_alias_matches()
    {
        Write(server: Environment.MachineName);
        Assert.NotNull(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Theory]
    [InlineData("OtherDatabase", ".")]
    [InlineData("DangKyTinChi_Minh", "remote.example")]
    [InlineData("DangKyTinChi_Minh", ".\\SQLEXPRESS")]
    public void Credentials_are_bound_to_database_and_local_default_instance(string database, string server)
    {
        Write(database: database, server: server);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Theory]
    [InlineData(0, "sv2024001", TestPassword)]
    [InlineData(2, "sv2024001", TestPassword)]
    [InlineData(1, "", TestPassword)]
    [InlineData(1, "sv\n2024001", TestPassword)]
    [InlineData(1, "sv2024001", "short")]
    [InlineData(1, "sv2024001", "password-with\n-control")]
    public void Invalid_payload_is_unavailable(int version, string username, string password)
    {
        Write(version, username, password);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Fact]
    public void Excessive_field_lengths_are_unavailable()
    {
        Write(username: new string('a', 129));
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        Write(password: new string('a', 257));
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(256)]
    public void Allowed_boundaries_load(int passwordLength)
    {
        Write(username: new string('a', 128), password: new string('b', passwordLength), database: "dangkyTINCHI_minh");
        Assert.NotNull(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"version\":\"1\"}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"server\":\".\",\"database\":\"DangKyTinChi_Minh\",\"student\":{},\"training\":{}}")]
    public void Unexpected_json_structure_fails_closed(string json)
    {
        Protect(json);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Fact]
    public void Whitespace_username_and_invalid_training_account_are_unavailable()
    {
        Write(username: " sv2024001");
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        Protect(JsonSerializer.Serialize(new {
            version = 1, server = ".", database = "DangKyTinChi_Minh",
            student = new { username = "sv2024001", password = TestPassword },
            training = new { username = "daotao", password = "short" }
        }));
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Fact]
    public void Both_accounts_are_required()
    {
        Protect("{\"version\":1,\"server\":\".\",\"database\":\"DangKyTinChi_Minh\",\"student\":null,\"training\":null}");
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Fact]
    public void Missing_directory_relative_path_and_invalid_connection_fail_closed()
    {
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        Write();
        Assert.Null(QuickLoginCredentials.Load(Connection, "credentials.dat"));
        Assert.Null(QuickLoginCredentials.Load(Connection, directory));
        Assert.Null(QuickLoginCredentials.Load("invalid connection", FilePath));
        Assert.Null(QuickLoginCredentials.Load(Connection.Replace("Server=.;", "Server=other;"), FilePath));
    }

    [Fact]
    public void Bad_ciphertext_plaintext_wrong_entropy_and_malformed_json_fail_closed()
    {
        System.IO.File.WriteAllBytes(FilePath, [1, 2, 3]);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        System.IO.File.WriteAllText(FilePath, "plaintext-password");
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        Protect("{}", "wrong-entropy");
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        Protect("not-json");
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Fact]
    public void Oversize_file_is_rejected()
    {
        System.IO.File.WriteAllBytes(FilePath, new byte[65537]);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Fact]
    public void Empty_and_locked_files_are_unavailable()
    {
        System.IO.File.WriteAllBytes(FilePath, []);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
        Write();
        using var locked = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Null(QuickLoginCredentials.Load(Connection, FilePath));
    }

    [Theory]
    [InlineData("Server=.;Integrated Security=true")]
    [InlineData("Server=.;Database=DangKyTinChi_Minh;AttachDbFilename=C:\\other.mdf")]
    [InlineData("Server=.;Database=DangKyTinChi_Minh;User Instance=true")]
    public void Alternate_database_selection_is_rejected(string connection)
    {
        Write();
        Assert.Null(QuickLoginCredentials.Load(connection, FilePath));
    }

    [Fact]
    public void Default_path_is_per_windows_user()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Campus", "quick-login.dat"), QuickLoginCredentials.DefaultPath);
    }

    private void Write(int version = 1, string username = "sv2024001", string password = TestPassword,
        string database = "DangKyTinChi_Minh", string server = ".") => Protect(JsonSerializer.Serialize(new {
            version, server, database, student = new { username, password }, training = new { username = "daotao", password = TestPassword }
        }));

    private void Protect(string json, string entropy = "Campus.QuickLogin.v1")
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI fixtures require Windows.");
        var plaintext = Encoding.UTF8.GetBytes(json);
        try { System.IO.File.WriteAllBytes(FilePath, ProtectedData.Protect(plaintext, Encoding.UTF8.GetBytes(entropy), DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(FilePath)) System.IO.File.Delete(FilePath);
        Directory.Delete(directory);
    }
}
