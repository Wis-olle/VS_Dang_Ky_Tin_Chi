using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace QuanLyGiangVien.Data;

/// <summary>Read-only resolution of an explicitly selected, retained synthetic showcase.</summary>
public sealed record SelectedShowcaseLaunch(string ConnectionString, string QuickLoginFile)
{
    private const string DatabasePrefix = "DangKyTinChi_Minh_Test_Showcase_";
    private const string Recovery = "Không thể mở bản trình diễn đã chọn vì dữ liệu hoặc đường dẫn chưa hợp lệ. "
        + "Chạy scripts/Initialize-ShowcaseDemo.ps1 -FullData để tạo và chọn bộ demo đầy đủ rồi mở Mo-BanTrinhDien.cmd, "
        + "hoặc chọn profile CampusDuLieuLamViec của QuanLyGiangVien. "
        + "Ứng dụng chưa kết nối và không tự chuyển sang database chính.";
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static void EnsureCompatible(IEnumerable<string> args, IEnumerable<string> activeEnvironmentKeys)
    {
        if (args == null || activeEnvironmentKeys == null
            || args.Contains("--demo", StringComparer.OrdinalIgnoreCase)
            || activeEnvironmentKeys.Any(key => key?.EndsWith("UI_CHECK", StringComparison.OrdinalIgnoreCase) == true))
            throw new InvalidOperationException("Không thể mở bản trình diễn cùng chế độ demo bộ nhớ hoặc bộ kiểm tra UI có thể ghi dữ liệu. " + Recovery);
    }

    public static SelectedShowcaseLaunch Load(string searchDirectory)
    {
        try { return Read(searchDirectory); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or JsonException or InvalidOperationException or SecurityException or KeyNotFoundException)
        { throw new InvalidOperationException(Recovery); }
    }

    private static SelectedShowcaseLaunch Read(string searchDirectory)
    {
        Require(!string.IsNullOrWhiteSpace(searchDirectory) && Directory.Exists(searchDirectory));
        string repository = FindRepository(Path.GetFullPath(searchDirectory));
        string showcase = Path.Combine(repository, ".tmp", "showcase-20");
        string pointer = Path.Combine(showcase, "current-manifest-path.txt");
        string manifest = ReadSmall(pointer, 4096).Trim();
        Require(Path.IsPathFullyQualified(manifest));
        manifest = Path.GetFullPath(manifest);
        string? run = Path.GetDirectoryName(manifest);
        Require(run != null && SamePath(Path.GetDirectoryName(run) ?? "", showcase));
        string uuid = Path.GetFileName(run!);
        Require(Regex.IsMatch(uuid, "\\A[a-f0-9]{32}\\z", RegexOptions.CultureInvariant));
        Require(SamePath(manifest, Path.Combine(run!, "manifest.json")));
        using var document = JsonDocument.Parse(ReadSmall(manifest, 65536), new JsonDocumentOptions { MaxDepth = 16 });
        var data = document.RootElement;
        Require(data.ValueKind == JsonValueKind.Object);
        var properties = data.EnumerateObject().ToArray();
        Require(properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length);
        Require(String(data, "database") == DatabasePrefix + uuid);
        Require(Flag(data, "passed") && Flag(data, "synthetic") && Flag(data, "fullData")
            && Flag(data, "keepDatabase") && !Flag(data, "cleaned"));
        string server = String(data, "server");
        Require(server == "." || server.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || server.Equals("(local)", StringComparison.OrdinalIgnoreCase));
        Require(ExactManifestPath(String(data, "output"), run!));
        string quick = Path.Combine(run!, "quick-login.dat");
        Require(ExactManifestPath(String(data, "quickLoginFile"), quick));
        RegularFile(quick);
        Require(new FileInfo(quick).Length > 0);
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = DatabasePrefix + uuid, IntegratedSecurity = true,
            Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = true, ConnectTimeout = 5
        };
        return new(connection.ConnectionString, quick);
    }

    private static string FindRepository(string search)
    {
        for (var directory = new DirectoryInfo(search); directory != null; directory = directory.Parent)
        {
            string marker = Path.Combine(directory.FullName, "QuanLyGiangVien", "QuanLyGiangVien.csproj");
            if (!File.Exists(marker)) continue;
            RegularFile(marker); return directory.FullName;
        }
        throw new InvalidOperationException();
    }

    private static string ReadSmall(string path, long maximum)
    {
        RegularFile(path);
        Require(new FileInfo(path).Length <= maximum);
        return File.ReadAllText(path);
    }

    private static void RegularFile(string path)
    {
        Require(File.Exists(path));
        Require((File.GetAttributes(path) & FileAttributes.Directory) == 0);
        RejectReparseChain(path);
    }

    private static void RejectReparseChain(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0);
    }

    private static bool ExactManifestPath(string value, string expected)
    {
        if (!Path.IsPathFullyQualified(value) || !SamePath(Path.GetFullPath(value), expected)) return false;
        RejectReparseChain(expected); return true;
    }
    private static bool SamePath(string first, string second) => first.Equals(second, PathComparison);
    private static string String(JsonElement data, string name)
    {
        var value = data.GetProperty(name); Require(value.ValueKind == JsonValueKind.String);
        string? text = value.GetString(); Require(!string.IsNullOrWhiteSpace(text)); return text!;
    }
    private static bool Flag(JsonElement data, string name)
    {
        var value = data.GetProperty(name);
        Require(value.ValueKind is JsonValueKind.True or JsonValueKind.False); return value.GetBoolean();
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException(); }
    public override string ToString() => nameof(SelectedShowcaseLaunch);
}
