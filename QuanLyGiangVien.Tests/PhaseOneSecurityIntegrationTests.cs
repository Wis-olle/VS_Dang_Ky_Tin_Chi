using System.Data;
using System.Security.Cryptography;
using System.Transactions;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class PhaseOneSecurityIntegrationTests
{
    [SqlIntegrationFact]
    public async Task StudentTimetableReturnsOnlyRelatedTeachersAndNoPersonalFields()
    {
        await using var fixture = await SessionFixture.CreateAsync("SINH_VIEN");
        AppSqlConnection.SetSession(fixture.ConnectionString, fixture.Token);
        var data = await new StudentTimetableRepository(fixture.ConnectionString).LoadAsync();
        Assert.Equal(fixture.StudentId, data.Identity.StudentId);
        Assert.All(data.Registrations, row => Assert.Equal(fixture.StudentId, row.StudentId));
        var relatedTeachers = data.Classes.Select(row => row.LecturerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(relatedTeachers.Order(), data.Teachers.Select(row => row.Id).Order());
        Assert.All(data.Teachers, teacher => Assert.Equal(new[] { "Id", "Name", "Status" },
            teacher.GetType().GetProperties().Select(property => property.Name).Order().ToArray()));
        var registeredClasses = data.Registrations.Select(row => row.ClassId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(data.Classes, row => Assert.Contains(row.Id, registeredClasses));

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await AppSqlConnection.OpenAsync(connection);
        using var command = new SqlCommand("dbo.usp_SinhVien_ThoiKhoaBieu", connection) { CommandType = CommandType.StoredProcedure };
        using (var reader = await command.ExecuteReaderAsync())
        {
            bool foundTeachers = false;
            do
            {
                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                Assert.DoesNotContain(columns, column => new[] { "ngay_sinh", "gioi_tinh", "email", "sdt", "avatar_key", "hoc_vi" }.Contains(column, StringComparer.OrdinalIgnoreCase));
                if (columns.Contains("ma_gv") && columns.Contains("ho_ten"))
                {
                    foundTeachers = true;
                    Assert.Equal(new[] { "ho_ten", "ma_gv", "trang_thai" }, columns.Order().ToArray());
                    var ids = new List<string>();
                    while (await reader.ReadAsync()) ids.Add(reader.GetString(reader.GetOrdinal("ma_gv")));
                    Assert.Equal(relatedTeachers.Order(), ids.Order());
                }
            } while (await reader.NextResultAsync());
            Assert.True(foundTeachers);
        }
        using var adminSnapshot = new SqlCommand("dbo.usp_Minh_DocDuLieu", connection) { CommandType = CommandType.StoredProcedure };
        await Assert.ThrowsAsync<SqlException>(() => adminSnapshot.ExecuteReaderAsync());
    }

    [SqlIntegrationFact]
    public async Task ExpiredCatalogSessionIsTypedNotifiesOnceAndNeverFallsBackOnRetry()
    {
        await using var fixture = await SessionFixture.CreateAsync("DAO_TAO");
        AppSqlConnection.SetSession(fixture.ConnectionString, fixture.Token);
        var repository = new CatalogRepository(fixture.ConnectionString);
        using (await repository.FacultiesAsync()) { }
        int notifications = 0;
        void Expired() => Interlocked.Increment(ref notifications);
        AppSqlConnection.SessionExpired += Expired;
        try
        {
            await fixture.ExpireAsync();
            var first = await Assert.ThrowsAsync<PersistenceException>(() => repository.HocKyAsync(""));
            Assert.Equal(PersistenceFailure.SessionExpired, first.Kind);
            Assert.Contains("đăng nhập", first.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("không thể xóa", first.Message);
            var retry = await Assert.ThrowsAsync<PersistenceException>(() => repository.FacultiesAsync());
            Assert.Equal(PersistenceFailure.SessionExpired, retry.Kind);
            Assert.Equal(1, notifications);
        }
        finally { AppSqlConnection.SessionExpired -= Expired; }
    }

    [SqlIntegrationFact]
    public async Task SessionTokenBelongsToItsSqlOwnerAndCannotAuthenticateAnotherOwner()
    {
        await using var fixture = await SessionFixture.CreateAsync("SINH_VIEN", "different-owner-" + Guid.NewGuid().ToString("N"));
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand("dbo.usp_Auth_UseSession", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = fixture.Token;
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(52405, error.Number);
    }

    [SqlIntegrationFact]
    public async Task SchemaHealthRejectsMissingVersionMarkerAndRollbackRestoresIt()
    {
        var connectionString = ConnectionString();
        await using (var initial = new SqlConnection(connectionString))
        {
            await initial.OpenAsync();
            await CampusSchema.ValidateAsync(initial);
        }
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var removeMarker = new SqlCommand("DELETE dbo.CAMPUS_SCHEMA_VERSION WHERE id=1", connection);
            Assert.Equal(1, await removeMarker.ExecuteNonQueryAsync());
            var error = await Assert.ThrowsAsync<PersistenceException>(() => CampusSchema.ValidateAsync(connection));
            Assert.Equal(PersistenceFailure.SchemaMismatch, error.Kind);
            // No Complete(): the deployment marker is restored when this scope ends.
        }
        await using var restored = new SqlConnection(connectionString);
        await restored.OpenAsync();
        await CampusSchema.ValidateAsync(restored);
    }

    private static string ConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connectionString).InitialCatalog, StringComparison.Ordinal);
        return connectionString;
    }

    private sealed class SessionFixture(string connectionString, int accountId, string? studentId, byte[] token) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public string? StudentId { get; } = studentId;
        public byte[] Token { get; } = token;

        public static async Task<SessionFixture> CreateAsync(string role, string? owner = null)
        {
            var connectionString = PhaseOneSecurityIntegrationTests.ConnectionString();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            const string sql = """
                DECLARE @sv varchar(20)=NULL;
                IF @role='SINH_VIEN'
                 SELECT TOP(1) @sv=s.ma_sv FROM dbo.SINH_VIEN s
                 WHERE s.trang_thai=1 ORDER BY (SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_sv=s.ma_sv AND d.trang_thai='DANG_KY') DESC,s.ma_sv;
                INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai,ma_sv) VALUES(@username,@role,1,@sv);
                DECLARE @id int=CONVERT(int,SCOPE_IDENTITY());
                INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires)
                 VALUES(HASHBYTES('SHA2_256',@token),@id,COALESCE(@owner,ORIGINAL_LOGIN()),DATEADD(HOUR,1,SYSUTCDATETIME()));
                SELECT @id,@sv;
                """;
            var token = RandomNumberGenerator.GetBytes(32);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@role", SqlDbType.VarChar, 20).Value = role;
            command.Parameters.Add("@username", SqlDbType.NVarChar, 128).Value = "phase-one-test-" + Guid.NewGuid().ToString("N");
            command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = token;
            command.Parameters.Add("@owner", SqlDbType.NVarChar, 128).Value = owner is null ? DBNull.Value : owner;
            using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new(connectionString, reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1), token);
        }

        public async Task ExpireAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("UPDATE dbo.APP_SESSION SET expires=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE ma_tk=@id", connection);
            command.Parameters.Add("@id", SqlDbType.Int).Value = accountId;
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        public async ValueTask DisposeAsync()
        {
            var removed = AppSqlConnection.TakeSession(ConnectionString);
            if (removed is not null) CryptographicOperations.ZeroMemory(removed);
            CryptographicOperations.ZeroMemory(Token);
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("DELETE dbo.APP_SESSION WHERE ma_tk=@id; DELETE dbo.TAI_KHOAN WHERE ma_tk=@id;", connection);
            command.Parameters.Add("@id", SqlDbType.Int).Value = accountId;
            await command.ExecuteNonQueryAsync();
        }
    }
}
