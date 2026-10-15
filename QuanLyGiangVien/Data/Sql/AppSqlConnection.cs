using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace QuanLyGiangVien.Data.Sql;

/// <summary>Installs the opaque, SQL-issued session on every pooled connection.</summary>
public static class AppSqlConnection
{
    private static readonly object gate = new();
    private static string? source, catalog;
    private static byte[]? session;

    internal static void SetSession(string connectionString, byte[] token)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        lock (gate)
        {
            if (session != null) CryptographicOperations.ZeroMemory(session);
            source = settings.DataSource; catalog = settings.InitialCatalog; session = token.ToArray();
        }
    }
    internal static byte[]? TakeSession(string connectionString)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        lock (gate)
        {
            if (!Matches(settings)) return null;
            var result = session; session = null; source = catalog = null; return result;
        }
    }
    private static bool Matches(SqlConnectionStringBuilder settings) =>
        string.Equals(settings.DataSource, source, StringComparison.OrdinalIgnoreCase)
        && string.Equals(settings.InitialCatalog, catalog, StringComparison.OrdinalIgnoreCase);

    public static async Task OpenAsync(SqlConnection connection, CancellationToken token = default)
    {
        await connection.OpenAsync(token).ConfigureAwait(false);
        await PrepareAsync(connection, token).ConfigureAwait(false);
    }
    public static void Open(SqlConnection connection)
    {
        connection.Open();
        PrepareAsync(connection).GetAwaiter().GetResult();
    }
    private static async Task PrepareAsync(SqlConnection connection, CancellationToken token = default)
    {
        byte[]? current;
        lock (gate) current = Matches(new SqlConnectionStringBuilder(connection.ConnectionString)) ? session?.ToArray() : null;
        // Legacy SQL/Windows fixtures continue to use their own authenticated identity.
        if (current == null) return;
        try
        {
            using var command = new SqlCommand("dbo.usp_Auth_UseSession", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = current;
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(current); }
    }
}
