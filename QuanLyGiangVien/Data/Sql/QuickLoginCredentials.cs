using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace QuanLyGiangVien.Data.Sql;

public enum QuickLoginStatus { Ready, MissingConfiguration, UnsupportedTarget, DifferentDatabase, InvalidConfiguration, CannotDecrypt, CannotRead }

public sealed record QuickLoginAccount(string Username, string Password)
{
    public override string ToString() => $"{Username} (password redacted)";
}

public sealed class QuickLoginCredentials
{
    private const int MaximumBytes = 64 * 1024;
    private readonly QuickLoginAccount student;
    private readonly QuickLoginAccount training;

    private QuickLoginCredentials(QuickLoginAccount student, QuickLoginAccount training)
        => (this.student, this.training) = (student, training);

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Campus", "quick-login.dat");
    public static string BundledPath => Path.Combine(AppContext.BaseDirectory, "quick-login.dat");

    // An explicit override stays authoritative, including deliberately missing test files.
    // Only an absent per-user file permits the locally bundled, still DPAPI-protected file.
    internal static string ResolvePath(string? requestedPath, string defaultPath, string bundledPath)
        => !string.IsNullOrWhiteSpace(requestedPath) ? requestedPath
            : File.Exists(defaultPath) ? defaultPath : bundledPath;

    // These credentials only prefill the normal password flow; SQL remains authoritative.
    // Never include decrypted content or exception details in an error shown by the login UI.
    public static QuickLoginCredentials? Load(string connectionString, string? path = null)
        => Load(connectionString, path, out _);

    public static QuickLoginCredentials? Load(string connectionString, string? path, out QuickLoginStatus status)
    {
        byte[]? plaintext = null;
        status = QuickLoginStatus.InvalidConfiguration;
        try
        {
            if (!OperatingSystem.IsWindows()) { status = QuickLoginStatus.UnsupportedTarget; return null; }
            var target = new SqlConnectionStringBuilder(connectionString);
            if (!IsLocalDefaultInstance(target.DataSource) || !ValidText(target.InitialCatalog, 1, 128)
                || !string.IsNullOrEmpty(target.AttachDBFilename) || target.UserInstance)
                { status = QuickLoginStatus.UnsupportedTarget; return null; }
            var resolved = ResolvePath(path, DefaultPath, BundledPath);
            if (!Path.IsPathFullyQualified(resolved)) return null;
            using var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > MaximumBytes) return null;
            var encrypted = new byte[(int)stream.Length];
            stream.ReadExactly(encrypted);
            if (stream.ReadByte() != -1) return null;
            plaintext = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes("Campus.QuickLogin.v1"), DataProtectionScope.CurrentUser);
            if (plaintext.Length > MaximumBytes) return null;
            using var document = JsonDocument.Parse(plaintext);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1) return null;
            if (!IsLocalDefaultInstance(Text(root, "server"))
                || !string.Equals(Text(root, "database"), target.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                { status = QuickLoginStatus.DifferentDatabase; return null; }
            var student = Account(root, "student");
            var training = Account(root, "training");
            if (student is null || training is null) return null;
            status = QuickLoginStatus.Ready;
            return new QuickLoginCredentials(student, training);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException
            or CryptographicException or JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            status = error switch {
                FileNotFoundException or DirectoryNotFoundException => QuickLoginStatus.MissingConfiguration,
                CryptographicException => QuickLoginStatus.CannotDecrypt,
                IOException or UnauthorizedAccessException or SecurityException => QuickLoginStatus.CannotRead,
                _ => QuickLoginStatus.InvalidConfiguration
            };
            return null;
        }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public QuickLoginAccount? For(LoginPortal portal) => portal switch {
        LoginPortal.Student => student, LoginPortal.Training => training, _ => null
    };

    private static bool IsLocalDefaultInstance(string? server) => server is "." or "(local)"
        || string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(server, Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    private static string? Text(JsonElement element, string key) => element.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static QuickLoginAccount? Account(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object) return null;
        var username = Text(value, "username");
        var password = Text(value, "password");
        return ValidText(username, 1, 128) && ValidText(password, 12, 256)
            && username == username!.Trim() ? new QuickLoginAccount(username!, password!) : null;
    }

    private static bool ValidText(string? value, int minimum, int maximum) => value is not null
        && value.Length >= minimum && value.Length <= maximum && !string.IsNullOrWhiteSpace(value)
        && !value.Any(char.IsControl);
}
