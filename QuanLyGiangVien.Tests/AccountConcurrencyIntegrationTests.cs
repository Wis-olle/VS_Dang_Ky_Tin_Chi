using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class AccountConcurrencyIntegrationTests
{
    [SqlIntegrationFact]
    public async Task ManagerRevokedWhileWaitingCannotResetAnotherAccount()
    {
        await using var fixture = await AccountFixture.CreateAsync();
        await using var manager = await OpenAsync();
        await UseSessionAsync(manager, fixture.ManagerToken);
        await using var transaction = (SqlTransaction)await fixture.Owner.BeginTransactionAsync();
        await LockAccountsAsync(fixture.Owner, transaction);
        using var reset = ResetCommand(manager, fixture.StudentId, fixture.StudentVersion, fixture.NewVerifier);
        var pending = reset.ExecuteNonQueryAsync();
        try
        {
            await WaitForAccountLockAsync(fixture.Owner, transaction, manager.ServerProcessId, pending);
            using var revoke = new SqlCommand("""
                UPDATE dbo.TAI_KHOAN SET trang_thai=0 WHERE ma_tk=@actor;
                DELETE dbo.APP_SESSION WHERE ma_tk=@actor;
                DELETE dbo.APP_LOGIN_CHALLENGE WHERE ma_tk=@actor;
                """, fixture.Owner, transaction);
            revoke.Parameters.Add("@actor", SqlDbType.Int).Value = fixture.ManagerId;
            await revoke.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            var error = await Assert.ThrowsAsync<SqlException>(() => pending);
            Assert.Equal(51001, error.Number);
            using var verify = new SqlCommand("""
                SELECT verifier FROM dbo.APP_CREDENTIAL WHERE ma_tk=@target;
                SELECT COUNT(*) FROM dbo.ACCOUNT_AUDIT WHERE target=@target;
                """, fixture.Owner);
            verify.Parameters.Add("@target", SqlDbType.Int).Value = fixture.StudentId;
            using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(fixture.OldVerifier, (byte[])reader[0]);
            Assert.True(await reader.NextResultAsync()); Assert.True(await reader.ReadAsync());
            Assert.Equal(0, reader.GetInt32(0));
        }
        finally
        {
            if (transaction.Connection != null) await transaction.RollbackAsync();
            await ObserveAsync(pending);
        }
    }

    [SqlIntegrationFact]
    public async Task LoginWaitsBeforeCredentialLocksAndResetInvalidatesOldProof()
    {
        await using var fixture = await AccountFixture.CreateAsync();
        await using var login = await OpenAsync();
        var oldChallenge = await BeginLoginAsync(login, fixture.StudentUsername);
        var oldProof = SHA256.HashData(oldChallenge.Nonce.Concat(fixture.OldVerifier).ToArray());
        using var complete = CompleteCommand(login, oldChallenge.Id, oldProof);
        await using var transaction = (SqlTransaction)await fixture.Owner.BeginTransactionAsync();
        await LockAccountsAsync(fixture.Owner, transaction);
        var pending = complete.ExecuteScalarAsync();
        try
        {
            await WaitForAccountLockAsync(fixture.Owner, transaction, login.ServerProcessId, pending);
            // This UPDATE would wait/deadlock if login held the credential lock before the shared first lock.
            using var reset = ResetCommand(fixture.Owner, fixture.StudentId, fixture.StudentVersion, fixture.NewVerifier);
            reset.Transaction = transaction; reset.CommandTimeout = 3;
            await reset.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            var rejected = await Assert.ThrowsAsync<SqlException>(async () => { await pending; });
            Assert.Equal(52401, rejected.Number);

            var newChallenge = await BeginLoginAsync(login, fixture.StudentUsername);
            var newProof = SHA256.HashData(newChallenge.Nonce.Concat(fixture.NewVerifier).ToArray());
            try
            {
                using var retry = CompleteCommand(login, newChallenge.Id, newProof);
                var token = Assert.IsType<byte[]>(await retry.ExecuteScalarAsync());
                try
                {
                    await UseSessionAsync(login, token);
                    using var who = new SqlCommand("SELECT dbo.ufn_Campus_AccountId();", login);
                    Assert.Equal(fixture.StudentId, Convert.ToInt32(await who.ExecuteScalarAsync()));
                }
                finally { CryptographicOperations.ZeroMemory(token); }
            }
            finally { CryptographicOperations.ZeroMemory(newProof); }
        }
        finally
        {
            if (transaction.Connection != null) await transaction.RollbackAsync();
            await ObserveAsync(pending); CryptographicOperations.ZeroMemory(oldProof);
        }
    }

    [SqlIntegrationFact]
    public async Task ApplicationRoleCanReadAdministrationContractButCannotReadPrivateTables()
    {
        await using var connection = await OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var name = "account_perm_" + Guid.NewGuid().ToString("N");
        using var create = new SqlCommand($"CREATE USER [{name}] WITHOUT LOGIN; ALTER ROLE app_minh ADD MEMBER [{name}]; EXECUTE AS USER = '{name}';", connection, transaction);
        await create.ExecuteNonQueryAsync();
        try
        {
            using (var list = new SqlCommand("dbo.usp_TaiKhoan_DanhSach", connection, transaction) { CommandType = CommandType.StoredProcedure })
            using (var reader = await list.ExecuteReaderAsync())
            {
                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                Assert.Contains("phien_ban", columns);
                Assert.DoesNotContain("verifier", columns); Assert.DoesNotContain("salt", columns);
            }
            foreach (var table in new[] { "APP_CREDENTIAL", "APP_SESSION", "ACCOUNT_AUDIT", "TAI_KHOAN" })
            {
                using var direct = new SqlCommand($"SELECT TOP (1) * FROM dbo.[{table}];", connection, transaction);
                var denied = await Assert.ThrowsAsync<SqlException>(() => direct.ExecuteReaderAsync());
                Assert.Equal(229, denied.Number);
            }
        }
        finally
        {
            using var revert = new SqlCommand("REVERT;", connection, transaction); await revert.ExecuteNonQueryAsync();
            if (transaction.Connection != null) await transaction.RollbackAsync();
        }
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION"));
        Assert.StartsWith("DangKyTinChi_Minh_Test_", builder.InitialCatalog, StringComparison.Ordinal);
        builder.Pooling = false;
        var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync(); return connection;
    }

    private static SqlCommand ResetCommand(SqlConnection connection, int target, string version, byte[] verifier)
    {
        var command = new SqlCommand("dbo.usp_TaiKhoan_Reset", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
        command.Parameters.Add("@ma_tk", SqlDbType.Int).Value = target;
        command.Parameters.Add("@phien_ban", SqlDbType.VarChar, 18).Value = version;
        command.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = RandomNumberGenerator.GetBytes(16);
        command.Parameters.Add("@verifier", SqlDbType.VarBinary, 32).Value = verifier;
        command.Parameters.Add("@ly_do", SqlDbType.NVarChar, 500).Value = "Isolated account concurrency regression";
        return command;
    }

    private static async Task LockAccountsAsync(SqlConnection owner, SqlTransaction transaction)
    {
        using var command = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Campus:Accounts',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000; SELECT @r;", owner, transaction);
        Assert.InRange(Convert.ToInt32(await command.ExecuteScalarAsync()), 0, 1);
    }

    private static async Task WaitForAccountLockAsync(SqlConnection owner, SqlTransaction transaction, int sessionId, Task pending)
    {
        using var command = new SqlCommand("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@spid AND resource_type='APPLICATION' AND request_status='WAIT';", owner, transaction);
        command.Parameters.Add("@spid", SqlDbType.Int).Value = sessionId;
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until)
        {
            Assert.False(pending.IsCompleted, "Operation completed without waiting for the account lock.");
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return;
            await Task.Delay(25);
        }
        Assert.Fail("Operation did not reach the shared account lock within five seconds.");
    }

    private static async Task UseSessionAsync(SqlConnection connection, byte[] token)
    {
        using var command = new SqlCommand("dbo.usp_Auth_UseSession", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = token; await command.ExecuteNonQueryAsync();
    }

    private static async Task<(Guid Id, byte[] Nonce)> BeginLoginAsync(SqlConnection connection, string username)
    {
        using var begin = new SqlCommand("dbo.usp_Auth_Begin", connection) { CommandType = CommandType.StoredProcedure };
        begin.Parameters.Add("@username", SqlDbType.NVarChar, 128).Value = username;
        using var reader = await begin.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        return (reader.GetGuid(0), (byte[])reader["nonce"]);
    }

    private static SqlCommand CompleteCommand(SqlConnection connection, Guid challenge, byte[] proof)
    {
        var command = new SqlCommand("dbo.usp_Auth_Complete", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
        command.Parameters.Add("@challenge", SqlDbType.UniqueIdentifier).Value = challenge;
        command.Parameters.Add("@proof", SqlDbType.VarBinary, 32).Value = proof;
        command.Parameters.Add("@portal", SqlDbType.VarChar, 10).Value = "STUDENT";
        return command;
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task; } catch (SqlException) { /* Expected SQL failures are asserted by the owning test. */ }
    }

    private sealed class AccountFixture : IAsyncDisposable
    {
        public SqlConnection Owner { get; }
        public int ManagerId { get; private set; }
        public int StudentId { get; private set; }
        public string StudentVersion { get; private set; } = "";
        public string StudentUsername { get; } = "student-race-" + Guid.NewGuid().ToString("N");
        public byte[] ManagerToken { get; } = RandomNumberGenerator.GetBytes(32);
        public byte[] OldVerifier { get; } = RandomNumberGenerator.GetBytes(32);
        public byte[] NewVerifier { get; } = RandomNumberGenerator.GetBytes(32);
        private AccountFixture(SqlConnection owner) => Owner = owner;

        public static async Task<AccountFixture> CreateAsync()
        {
            var fixture = new AccountFixture(await OpenAsync());
            try
            {
                using var command = new SqlCommand("""
                    SET XACT_ABORT ON;
                    BEGIN TRAN;
                    INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai) VALUES(@manager,'DAO_TAO',1);
                    DECLARE @managerId int=CONVERT(int,SCOPE_IDENTITY());
                    INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@managerId,CRYPT_GEN_RANDOM(16),CRYPT_GEN_RANDOM(32),600000);
                    INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires) VALUES(HASHBYTES('SHA2_256',@token),@managerId,ORIGINAL_LOGIN(),DATEADD(HOUR,1,SYSUTCDATETIME()));
                    INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai) VALUES(@student,'SINH_VIEN','SV2024001',1);
                    DECLARE @studentId int=CONVERT(int,SCOPE_IDENTITY());
                    INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@studentId,CRYPT_GEN_RANDOM(16),@old,600000);
                    COMMIT;
                    SELECT @managerId,@studentId,CONVERT(varchar(18),CONVERT(binary(8),phien_ban),1) FROM dbo.TAI_KHOAN WHERE ma_tk=@studentId;
                    """, fixture.Owner);
                command.Parameters.Add("@manager", SqlDbType.NVarChar, 128).Value = "manager-race-" + Guid.NewGuid().ToString("N");
                command.Parameters.Add("@student", SqlDbType.NVarChar, 128).Value = fixture.StudentUsername;
                command.Parameters.Add("@token", SqlDbType.VarBinary, 32).Value = fixture.ManagerToken;
                command.Parameters.Add("@old", SqlDbType.VarBinary, 32).Value = fixture.OldVerifier;
                using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
                fixture.ManagerId = reader.GetInt32(0); fixture.StudentId = reader.GetInt32(1); fixture.StudentVersion = reader.GetString(2);
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Owner.State == ConnectionState.Open && ManagerId != 0 && StudentId != 0)
                {
                    using var cleanup = new SqlCommand("""
                        DELETE dbo.ACCOUNT_AUDIT WHERE target IN (@manager,@student) OR actor IN (@manager,@student);
                        DELETE dbo.APP_LOGIN_CHALLENGE WHERE ma_tk IN (@manager,@student);
                        DELETE dbo.APP_SESSION WHERE ma_tk IN (@manager,@student);
                        DELETE dbo.APP_CREDENTIAL WHERE ma_tk IN (@manager,@student);
                        DELETE dbo.TAI_KHOAN WHERE ma_tk IN (@manager,@student);
                        """, Owner);
                    cleanup.Parameters.Add("@manager", SqlDbType.Int).Value = ManagerId;
                    cleanup.Parameters.Add("@student", SqlDbType.Int).Value = StudentId;
                    await cleanup.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ManagerToken); CryptographicOperations.ZeroMemory(OldVerifier); CryptographicOperations.ZeroMemory(NewVerifier);
                await Owner.DisposeAsync();
            }
        }
    }
}
