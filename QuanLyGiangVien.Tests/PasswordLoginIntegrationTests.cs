using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class PasswordLoginIntegrationTests
{
    [PasswordLoginFact]
    public async Task SharedDatabaseEnforcesPortalsSessionsAndStudentManagement()
    {
        var connectionString = Environment.GetEnvironmentVariable("CAMPUS_LOGIN_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connectionString).InitialCatalog, StringComparison.Ordinal);
        var studentPassword = Environment.GetEnvironmentVariable("DEMO_STUDENT_PASSWORD")!;
        var trainingPassword = Environment.GetEnvironmentVariable("DEMO_TRAINING_PASSWORD")!;
        var auth = new PasswordLogin(connectionString);
        try
        {
            await Assert.ThrowsAsync<PersistenceException>(() => auth.SignInAsync("nonexistent-account", "invalid", LoginPortal.Student));
            await Assert.ThrowsAsync<PersistenceException>(() => auth.SignInAsync("sv2024001", studentPassword, LoginPortal.Training));
            await auth.SignInAsync("sv2024001", studentPassword, LoginPortal.Student);
            var identity = await new SqlIdentityRepository(connectionString).LoadAsync();
            Assert.Equal("SINH_VIEN", identity.Role); Assert.Equal("SV2024001", identity.StudentId);
            var snapshot = await new StudentTimetableRepository(connectionString).LoadAsync();
            Assert.Equal(identity.AccountId, snapshot.Identity.AccountId);
            Assert.All(snapshot.Registrations, row => Assert.Equal(identity.StudentId, row.StudentId));
            await Assert.ThrowsAsync<PersistenceException>(() => new StudentRepository(connectionString).ListAsync("", true));
            await Assert.ThrowsAsync<PersistenceException>(() => new CatalogRepository(connectionString).FacultiesAsync());
            var rawToken = AppSqlConnection.TakeSession(connectionString)!;
            AppSqlConnection.SetSession(connectionString, rawToken);
            await auth.SignOutAsync();
            AppSqlConnection.SetSession(connectionString, rawToken);
            await Assert.ThrowsAsync<PersistenceException>(() => new SqlIdentityRepository(connectionString).LoadAsync());
            AppSqlConnection.TakeSession(connectionString);
            CryptographicOperations.ZeroMemory(rawToken);

            await Assert.ThrowsAsync<PersistenceException>(() => auth.SignInAsync("daotao", trainingPassword, LoginPortal.Student));
            await auth.SignInAsync("daotao", trainingPassword, LoginPortal.Training);
            Assert.Equal("DAO_TAO", (await new SqlIdentityRepository(connectionString).LoadAsync()).Role);
            using var students = await new StudentRepository(connectionString).ListAsync("SV2024001", true);
            Assert.Single(students.Rows.Cast<DataRow>());
            // Exercise actual save/update/status procedures inside one rolled-back transaction.
            await using var connection = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(connection);
            using var transaction = connection.BeginTransaction();
            var source = StudentRepository.FromRow(students.Rows[0]);
            var created = source with { Code = "SV_AUTH_TEST", Name = "Sinh viên kiểm thử", Email = null, Phone = null, BirthDate = null, Gender = null, Address = null };
            try
            {
                using var save = StudentRepository.SaveCommand(created, true); save.Connection = connection; save.Transaction = transaction;
                await save.ExecuteNonQueryAsync();
                using var getVersion = new SqlCommand("SELECT phien_ban FROM dbo.vw_SinhVien_PhienBan WHERE ma_sv='SV_AUTH_TEST'", connection, transaction);
                var saved = created with { Version = Convert.ToHexString((byte[])(await getVersion.ExecuteScalarAsync())!) };
                using var update = StudentRepository.SaveCommand(saved with { Name = "Đã sửa", Active = false }, false);
                update.Connection = connection; update.Transaction = transaction; await update.ExecuteNonQueryAsync();
                using var verify = new SqlCommand("SELECT ho_ten,trang_thai FROM dbo.SINH_VIEN WHERE ma_sv='SV_AUTH_TEST'", connection, transaction);
                using var reader = await verify.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync()); Assert.Equal("Đã sửa", reader.GetString(0)); Assert.False(reader.GetBoolean(1));
            }
            finally { transaction.Rollback(); }
        }
        finally { await auth.SignOutAsync(); }
    }

    [PasswordLoginFact]
    public async Task ServerChallengesCannotBeReplayedAndArbitraryTokensCannotAuthenticate()
    {
        var connectionString = Environment.GetEnvironmentVariable("CAMPUS_LOGIN_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connectionString).InitialCatalog, StringComparison.Ordinal);
        var password = Environment.GetEnvironmentVariable("DEMO_STUDENT_PASSWORD")!;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var begin = new SqlCommand("dbo.usp_Auth_Begin", connection) { CommandType = CommandType.StoredProcedure };
        begin.Parameters.Add("@username", SqlDbType.NVarChar, 128).Value = "sv2024001";
        Guid challenge; byte[] proof;
        using (var reader = await begin.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync()); challenge = reader.GetGuid(0);
            proof = PasswordProof.Create(password, (byte[])reader[1], (byte[])reader[2], reader.GetInt32(3));
        }
        using var complete = new SqlCommand("dbo.usp_Auth_Complete", connection) { CommandType = CommandType.StoredProcedure };
        complete.Parameters.Add("@challenge", SqlDbType.UniqueIdentifier).Value = challenge;
        complete.Parameters.Add("@proof", SqlDbType.VarBinary, 32).Value = proof;
        complete.Parameters.Add("@portal", SqlDbType.VarChar, 10).Value = "STUDENT";
        var token = (byte[])(await complete.ExecuteScalarAsync())!;
        try
        {
            var replay = await Assert.ThrowsAsync<SqlException>(() => complete.ExecuteScalarAsync());
            Assert.Equal(52401, replay.Number);
            using var fake = new SqlCommand("dbo.usp_Auth_UseSession", connection) { CommandType = CommandType.StoredProcedure };
            fake.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = RandomNumberGenerator.GetBytes(32);
            var rejected = await Assert.ThrowsAsync<SqlException>(() => fake.ExecuteNonQueryAsync()); Assert.Equal(52405, rejected.Number);
        }
        finally
        {
            using var logout = new SqlCommand("dbo.usp_Auth_Logout", connection) { CommandType = CommandType.StoredProcedure };
            logout.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = token; await logout.ExecuteNonQueryAsync();
            CryptographicOperations.ZeroMemory(token); CryptographicOperations.ZeroMemory(proof);
        }
    }
}

public sealed class PasswordLoginFactAttribute : FactAttribute
{
    public PasswordLoginFactAttribute()
    {
        if (new[] { "CAMPUS_LOGIN_TEST_CONNECTION", "DEMO_STUDENT_PASSWORD", "DEMO_TRAINING_PASSWORD" }.Any(key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))))
            Skip = "Provide the local shared demo database and its explicitly seeded test credentials.";
    }
}
