using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Data.Sql;

/// <summary>Installs the opaque, SQL-issued session on every pooled connection.</summary>
public static class AppSqlConnection
{
    private static readonly object gate = new();
    private static string? source, catalog;
    private static byte[]? session;
    private static bool expired;
    private static long generation;
    public static event Action? SessionExpired;
    public static event Action? SessionRetryBlocked;
    internal static bool IsBlocked(string connectionString)
    {
        lock (gate) return Matches(new SqlConnectionStringBuilder(connectionString)) && expired;
    }

    internal static void SetSession(string connectionString, byte[] token)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        lock (gate)
        {
            if (session != null) CryptographicOperations.ZeroMemory(session);
            source = settings.DataSource; catalog = settings.InitialCatalog; session = token.ToArray();
            expired = false; generation++;
        }
    }
    internal static byte[]? TakeSession(string connectionString)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        lock (gate)
        {
            if (!Matches(settings)) return null;
            var result = session; session = null; source = catalog = null; expired = false; generation++; return result;
        }
    }
    private static bool Matches(SqlConnectionStringBuilder settings) =>
        string.Equals(settings.DataSource, source, StringComparison.OrdinalIgnoreCase)
        && string.Equals(settings.InitialCatalog, catalog, StringComparison.OrdinalIgnoreCase);

    public static async Task OpenAsync(SqlConnection connection, CancellationToken token = default)
    {
        ThrowIfExpired(connection.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await PrepareAsync(connection, token).ConfigureAwait(false);
    }
    public static void Open(SqlConnection connection)
    {
        ThrowIfExpired(connection.ConnectionString);
        connection.Open();
        PrepareAsync(connection).GetAwaiter().GetResult();
    }
    private static async Task PrepareAsync(SqlConnection connection, CancellationToken token = default)
    {
        byte[]? current; long version;
        lock (gate) { current = Matches(new SqlConnectionStringBuilder(connection.ConnectionString)) ? session?.ToArray() : null; version = generation; }
        // Legacy SQL/Windows fixtures continue to use their own authenticated identity.
        if (current == null) return;
        try
        {
            using var command = new SqlCommand("dbo.usp_Auth_UseSession", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = current;
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        catch (SqlException error) when (error.Number is 52405 or 52105)
        {
            // 52105 is accepted only here for pre-migration Auth, never for a catalog operation.
            bool notify = false;
            lock (gate)
            {
                if (version == generation && Matches(new SqlConnectionStringBuilder(connection.ConnectionString)))
                { notify = !expired; expired = true; }
            }
            if (notify) SessionExpired?.Invoke();
            throw Expired();
        }
        finally { CryptographicOperations.ZeroMemory(current); }
    }

    private static void ThrowIfExpired(string connectionString)
    {
        bool blocked;
        lock (gate) blocked = Matches(new SqlConnectionStringBuilder(connectionString)) && expired;
        if (!blocked) return;
        SessionRetryBlocked?.Invoke();
        throw Expired();
    }
    private static PersistenceException Expired() => new("Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục; bản nháp của bạn được giữ nguyên.", PersistenceFailure.SessionExpired);

    internal static void BlockSession(string connectionString)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        lock (gate) { source = settings.DataSource; catalog = settings.InitialCatalog; expired = true; generation++; }
    }
}
