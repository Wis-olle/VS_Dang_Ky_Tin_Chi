namespace QuanLyGiangVien.Data;

/// <summary>Explicit demo mode is isolated from persistent data and native UI fixtures.</summary>
public sealed class DatabaseLaunch
{
    public bool UseDatabase { get; }
    public string ConnectionString { get; }

    private DatabaseLaunch(bool useDatabase, string connectionString)
    {
        UseDatabase = useDatabase;
        ConnectionString = connectionString;
    }

    public static DatabaseLaunch Parse(IEnumerable<string> args, string? configuredConnection, bool nativeUiCheck)
    {
        var flags = args.ToHashSet(StringComparer.Ordinal);
        if (flags.Contains("--demo") && flags.Contains("--database"))
            throw new ArgumentException("Chỉ chọn một chế độ: --demo hoặc --database.");
        bool useDatabase = !flags.Contains("--demo") && (!nativeUiCheck || flags.Contains("--database"));
        // The certificate exception applies only to this local development instance.
        const string local = "Server=localhost;Database=DangKyTinChi_Minh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5";
        return new(useDatabase, string.IsNullOrWhiteSpace(configuredConnection) ? local : configuredConnection.Trim());
    }
}
