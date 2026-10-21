using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class StudentsIntegrationTests
{
    [SqlIntegrationFact]
    public async Task StaleProfileAndStatusCannotReactivateOrOverwriteAnotherEditorsChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connectionString).InitialCatalog, StringComparison.Ordinal);
        const string code = "SV_VERSION_TEST";
        var repository = new StudentRepository(connectionString);
        using var sourceTable = await repository.ListAsync("", true);
        var source = StudentRepository.FromRow(sourceTable.Rows[0]);
        var created = source with { Code = code, Name = "Sinh viên kiểm thử phiên bản", Email = null, Phone = null };
        bool added = false;
        try
        {
            await repository.SaveAsync(created, true); added = true;
            var editorA = await ReadAsync(repository, code);
            var editorB = await ReadAsync(repository, code);
            Assert.Equal(editorA.Version, editorB.Version);
            Assert.Equal(64, editorA.Version!.Length);

            await repository.SetStatusAsync(editorB, false);
            var draft = editorA with { Phone = "0901234567" };
            var rejectedSave = await Assert.ThrowsAsync<PersistenceException>(() => repository.SaveAsync(draft, false));
            Assert.Contains("đã thay đổi", rejectedSave.Message);
            Assert.Equal(PersistenceFailure.DataConflict, rejectedSave.Kind);
            var rejectedStatus = await Assert.ThrowsAsync<PersistenceException>(() => repository.SetStatusAsync(editorA, true));
            Assert.Contains("đã thay đổi", rejectedStatus.Message);
            Assert.Equal(PersistenceFailure.DataConflict, rejectedStatus.Kind);

            var current = await ReadAsync(repository, code);
            Assert.False(current.Active);
            Assert.Null(current.Phone);
            Assert.Equal("0901234567", draft.Phone);
            Assert.True(draft.Active);
            Assert.NotEqual(editorA.Version, current.Version);

            // The database rejects a caller that bypasses the client token check.
            await using (var connection = new SqlConnection(connectionString))
            {
                await AppSqlConnection.OpenAsync(connection);
                using var noVersion = StudentRepository.SaveCommand(current with { Active = true }, false);
                noVersion.Parameters["@phien_ban"].Value = DBNull.Value;
                noVersion.Connection = connection;
                var rejected = await Assert.ThrowsAsync<SqlException>(() => noVersion.ExecuteNonQueryAsync());
                Assert.Equal(52304, rejected.Number);
            }

            await repository.SaveAsync(current with { Name = "Đã tải lại và sửa" }, false);
            var refreshed = await ReadAsync(repository, code);
            Assert.False(refreshed.Active);
            Assert.Equal("Đã tải lại và sửa", refreshed.Name);
            await repository.SetStatusAsync(refreshed, true);
            Assert.True((await ReadAsync(repository, code)).Active);
        }
        finally
        {
            if (added)
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var cleanup = new SqlCommand("DELETE dbo.SINH_VIEN WHERE ma_sv=@code", connection);
                cleanup.Parameters.Add("@code", SqlDbType.VarChar, 20).Value = code;
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<StudentRecord> ReadAsync(StudentRepository repository, string code)
    {
        using var table = await repository.ListAsync(code, true);
        Assert.Single(table.Rows.Cast<DataRow>());
        return StudentRepository.FromRow(table.Rows[0]);
    }
}
