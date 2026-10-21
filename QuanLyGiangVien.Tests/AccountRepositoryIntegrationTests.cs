using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class AccountRepositoryIntegrationTests
{
    [SqlIntegrationFact]
    public async Task RealRepositoryCreatesResetsChangesOwnPasswordAndRejectsOldCredentials()
    {
        string cs = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog);
        var username = "repository-" + Guid.NewGuid().ToString("N");
        var first = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var second = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var third = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        int? id = null;
        var repository = new AccountRepository(cs); var login = new PasswordLogin(cs);
        try
        {
            await repository.SaveAsync(new(null, username, "SINH_VIEN", "SV2024001", true, null, "Create integration account"), first);
            using var rows = await repository.ListAsync();
            var account = rows.AsEnumerable().Single(row => (string)row["ten_dang_nhap"] == username);
            id = (int)account["ma_tk"]; var version = (string)account["phien_ban"];
            Assert.Matches("^0x[A-F0-9]{16}$", version);
            var duplicate = await Assert.ThrowsAsync<PersistenceException>(() => repository.SaveAsync(new(null, username, "SINH_VIEN", "SV2024001", true, null, "Duplicate"), first));
            Assert.Contains("tồn tại", duplicate.Message);
            await repository.SaveAsync(new(id, username, "SINH_VIEN", "SV2024001", false, version, "Temporary lock"));
            var stale = await Assert.ThrowsAsync<PersistenceException>(() => repository.SaveAsync(new(id, username, "SINH_VIEN", "SV2024001", true, version, "Stale editor")));
            Assert.Equal(PersistenceFailure.DataConflict, stale.Kind);
            using var lockedRows = await repository.ListAsync();
            version = (string)lockedRows.AsEnumerable().Single(row => (int)row["ma_tk"] == id)["phien_ban"];
            await repository.SaveAsync(new(id, username, "SINH_VIEN", "SV2024001", true, version, "Unlock"));
            using var fresh = await repository.ListAsync();
            version = (string)fresh.AsEnumerable().Single(row => (int)row["ma_tk"] == id)["phien_ban"];
            await repository.ResetAsync(id.Value, version, second, "Reset integration credential");
            using (var audit = await repository.AuditAsync()) Assert.Contains(audit.AsEnumerable(), row => (int)row["target"] == id && (string)row["action"] == "RESET");
            await Assert.ThrowsAsync<PersistenceException>(() => login.SignInAsync(username, first, LoginPortal.Student));
            await login.SignInAsync(username, second, LoginPortal.Student);
            await Assert.ThrowsAsync<PersistenceException>(() => repository.ListAsync());
            await Assert.ThrowsAsync<PersistenceException>(() => repository.ChangeOwnPasswordAsync(username, first, third));
            await repository.ChangeOwnPasswordAsync(username, second, third);
            Assert.True(AppSqlConnection.IsBlocked(cs));
            await login.SignOutAsync();
            await Assert.ThrowsAsync<PersistenceException>(() => login.SignInAsync(username, second, LoginPortal.Student));
            await login.SignInAsync(username, third, LoginPortal.Student);
            using var user = await new RegistrationRepository(cs).NguoiDungAsync();
            Assert.Equal("SV2024001", user.Rows[0]["ma_sv"]);
        }
        finally
        {
            await login.SignOutAsync();
            await using var connection = new SqlConnection(cs); await connection.OpenAsync();
            using var cleanup = new SqlCommand("""
                DECLARE @id int=(SELECT ma_tk FROM TAI_KHOAN WHERE ten_dang_nhap=@name);
                DELETE APP_LOGIN_CHALLENGE WHERE ma_tk=@id; DELETE APP_SESSION WHERE ma_tk=@id;
                DELETE APP_CREDENTIAL WHERE ma_tk=@id; DELETE ACCOUNT_AUDIT WHERE target=@id OR actor=@id;
                DELETE TAI_KHOAN WHERE ma_tk=@id;
                """, connection);
            cleanup.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = username;
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
