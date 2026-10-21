using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class AccountAdministrationIntegrationTests
{
    [SqlIntegrationFact]
    public async Task AdministrationReadNeverReturnsCredentialsAndRejectsStudentRole()
    {
        var cs = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        using (var list = new SqlCommand("dbo.usp_TaiKhoan_DanhSach", connection, transaction) { CommandType = CommandType.StoredProcedure })
        using (var reader = await list.ExecuteReaderAsync())
        {
            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            Assert.Contains("phien_ban", columns);
            Assert.DoesNotContain(columns, column => column.Contains("hash") || column is "salt" or "verifier");
        }
        using (var impersonate = new SqlCommand("UPDATE TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ten_dang_nhap=ORIGINAL_LOGIN()", connection, transaction))
            await impersonate.ExecuteNonQueryAsync();
        using var denied = new SqlCommand("dbo.usp_TaiKhoan_DanhSach", connection, transaction) { CommandType = CommandType.StoredProcedure };
        var error = await Assert.ThrowsAsync<SqlException>(() => denied.ExecuteReaderAsync());
        Assert.Equal(52701, error.Number);
    }

    [SqlIntegrationFact]
    public async Task CreateStatusAndResetAreAuditedAndRevokeSessions()
    {
        await using var connection = await OpenAsync();
        using var transaction = connection.BeginTransaction();
        var sql = """
            DECLARE @account TABLE(ma_tk int,phien_ban varchar(18));
            INSERT @account EXEC dbo.usp_TaiKhoan_Luu @ten_dang_nhap=@username,@vai_tro='SINH_VIEN',@ma_sv='SV2024001',@ly_do=N'Integration create',@salt=0x01010101010101010101010101010101,@verifier=0x0101010101010101010101010101010101010101010101010101010101010101;
            DECLARE @id int=(SELECT ma_tk FROM @account),@version varchar(18)=(SELECT phien_ban FROM @account);
            INSERT APP_SESSION(token_hash,ma_tk,owner_name,expires) VALUES(CRYPT_GEN_RANDOM(32),@id,ORIGINAL_LOGIN(),DATEADD(HOUR,1,SYSUTCDATETIME()));
            INSERT APP_LOGIN_CHALLENGE(id,ma_tk,nonce,owner_name,expires) VALUES(NEWID(),@id,CRYPT_GEN_RANDOM(32),ORIGINAL_LOGIN(),DATEADD(MINUTE,1,SYSUTCDATETIME()));
            EXEC dbo.usp_TaiKhoan_Luu @ma_tk=@id,@ten_dang_nhap=@username,@vai_tro='SINH_VIEN',@ma_sv='SV2024001',@trang_thai=0,@phien_ban=@version,@ly_do=N'Integration lock';
            IF EXISTS(SELECT 1 FROM APP_SESSION WHERE ma_tk=@id) OR EXISTS(SELECT 1 FROM APP_LOGIN_CHALLENGE WHERE ma_tk=@id) THROW 59990,'Sessions were not revoked.',1;
            SELECT @version=CONVERT(varchar(18),CONVERT(binary(8),phien_ban),1) FROM TAI_KHOAN WHERE ma_tk=@id;
            EXEC dbo.usp_TaiKhoan_Reset @ma_tk=@id,@phien_ban=@version,@salt=0x02020202020202020202020202020202,@verifier=0x0202020202020202020202020202020202020202020202020202020202020202,@ly_do=N'Integration reset';
            SELECT COUNT(*) FROM ACCOUNT_AUDIT WHERE target=@id;
            """;
        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@username", "account-test-" + Guid.NewGuid().ToString("N"));
        using var reader = await command.ExecuteReaderAsync();
        while (reader.FieldCount != 1 || reader.GetName(0) != "") { if (!await reader.NextResultAsync()) throw new InvalidOperationException("Missing audit count."); }
        Assert.True(await reader.ReadAsync()); Assert.Equal(3, reader.GetInt32(0));
    }

    [SqlIntegrationTheory]
    [InlineData("STALE", 52704)]
    [InlineData("PRIVILEGE", 52701)]
    [InlineData("MAPPING", 52705)]
    [InlineData("SELF", 52705)]
    public async Task UnsafeAdministrationIsRejected(string scenario, int expected)
    {
        await using var connection = await OpenAsync();
        using var transaction = connection.BeginTransaction();
        using var command = new SqlCommand("""
            DECLARE @actor int=dbo.ufn_Campus_AccountId(),@id int,@version varchar(18),@name nvarchar(128)=N'negative-'+CONVERT(nvarchar(36),NEWID());
            INSERT TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai) VALUES(@name,'SINH_VIEN','SV2024001',1);
            SET @id=CONVERT(int,SCOPE_IDENTITY());
            INSERT APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@id,CRYPT_GEN_RANDOM(16),CRYPT_GEN_RANDOM(32),600000);
            SELECT @version=CONVERT(varchar(18),CONVERT(binary(8),phien_ban),1) FROM TAI_KHOAN WHERE ma_tk=@id;
            IF @scenario='STALE' UPDATE TAI_KHOAN SET trang_thai=0 WHERE ma_tk=@id;
            IF @scenario='PRIVILEGE' EXEC dbo.usp_TaiKhoan_Luu @ten_dang_nhap=N'cannot-elevate',@vai_tro='QUAN_TRI',@ly_do=N'Forbidden';
            IF @scenario='SELF' BEGIN
              SELECT @id=ma_tk,@version=CONVERT(varchar(18),CONVERT(binary(8),phien_ban),1),@name=ten_dang_nhap FROM TAI_KHOAN WHERE ma_tk=@actor;
              UPDATE TAI_KHOAN SET vai_tro='QUAN_TRI' WHERE ma_tk=@actor;
              SELECT @version=CONVERT(varchar(18),CONVERT(binary(8),phien_ban),1) FROM TAI_KHOAN WHERE ma_tk=@actor;
              EXEC dbo.usp_TaiKhoan_Luu @ma_tk=@id,@ten_dang_nhap=@name,@vai_tro='QUAN_TRI',@trang_thai=0,@phien_ban=@version,@ly_do=N'Cannot lock self';
            END;
            IF @scenario='MAPPING' EXEC dbo.usp_TaiKhoan_Luu @ma_tk=@id,@ten_dang_nhap=@name,@vai_tro='SINH_VIEN',@ma_sv='SV2024002',@phien_ban=@version,@ly_do=N'Cannot reassign';
            IF @scenario='STALE' EXEC dbo.usp_TaiKhoan_Luu @ma_tk=@id,@ten_dang_nhap=@name,@vai_tro='SINH_VIEN',@ma_sv='SV2024001',@phien_ban=@version,@ly_do=N'Stale';
            """, connection, transaction);
        command.Parameters.AddWithValue("@scenario", scenario);
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expected, error.Number);
    }

    [SqlIntegrationFact]
    public async Task OwnPasswordRequiresSingleUseProofAndRevokesAllSessions()
    {
        await using var connection = await OpenAsync();
        using var transaction = connection.BeginTransaction();
        using var command = new SqlCommand("""
            DECLARE @actor int=dbo.ufn_Campus_AccountId(),@key binary(32)=CRYPT_GEN_RANDOM(32),@nonce binary(32)=CRYPT_GEN_RANDOM(32),@challenge uniqueidentifier=NEWID();
            INSERT APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@actor,CRYPT_GEN_RANDOM(16),@key,600000);
            INSERT APP_SESSION(token_hash,ma_tk,owner_name,expires) VALUES(CRYPT_GEN_RANDOM(32),@actor,ORIGINAL_LOGIN(),DATEADD(HOUR,1,SYSUTCDATETIME()));
            INSERT APP_LOGIN_CHALLENGE(id,ma_tk,nonce,owner_name,expires) VALUES(@challenge,@actor,@nonce,ORIGINAL_LOGIN(),DATEADD(MINUTE,1,SYSUTCDATETIME()));
            DECLARE @proof varbinary(32)=HASHBYTES('SHA2_256',@nonce+@key);
            EXEC dbo.usp_TaiKhoan_DoiMatKhau @challenge=@challenge,@proof=@proof,@salt=0x02020202020202020202020202020202,@verifier=0x0202020202020202020202020202020202020202020202020202020202020202;
            IF EXISTS(SELECT 1 FROM APP_SESSION WHERE ma_tk=@actor) OR EXISTS(SELECT 1 FROM APP_LOGIN_CHALLENGE WHERE ma_tk=@actor) THROW 59990,'Sessions not revoked.',1;
            EXEC dbo.usp_TaiKhoan_DoiMatKhau @challenge=@challenge,@proof=@proof,@salt=0x02020202020202020202020202020202,@verifier=0x0202020202020202020202020202020202020202020202020202020202020202;
            """, connection, transaction);
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(52707, error.Number);
    }

    private static async Task<SqlConnection> OpenAsync()
    {
        var cs = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
        var connection = new SqlConnection(cs); await connection.OpenAsync(); return connection;
    }
}

public sealed class SqlIntegrationTheoryAttribute : TheoryAttribute
{
    public SqlIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION"))) Skip = "Requires disposable SQL fixture.";
    }
}
