using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class TimetableDirectoryRepositoryTests
{
    private static DataTable Students()
    {
        var table = new DataTable();
        foreach (var name in new[] { "ma_sv", "ho_ten", "ma_lop", "ten_lop" }) table.Columns.Add(name);
        table.Rows.Add("SV1", "An", "D1", "Đại học 1");
        table.Rows.Add("SV2", "Bình", "D2", "Đại học 2");
        return table;
    }

    [Fact]
    public async Task MapsOnlyTimetableIdentityFieldsAndDisposesThePrivateTable()
    {
        var table = Students();
        table.Columns.Add("email");
        table.Rows[0]["email"] = "private@example.test";
        var disposed = false;
        table.Disposed += (_, _) => disposed = true;
        var repository = new TimetableDirectoryRepository(() => Task.FromResult(table));
        var result = await repository.LoadStudentsAsync();
        Assert.Equal(2, result.Length);
        Assert.Equal("SV1", result[0].Id);
        Assert.Equal("An", result[0].Name);
        Assert.Equal("D1", result[0].ClassId);
        Assert.Equal("Đại học 1", result[0].ClassName);
        Assert.True(disposed);
    }

    [Fact]
    public async Task PreCanceledRequestDoesNotStartSqlRead()
    {
        var calls = 0;
        var repository = new TimetableDirectoryRepository(() => { calls++; return Task.FromResult(Students()); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.LoadStudentsAsync(new CancellationToken(true)));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CancellationDuringLegacySqlReadDisposesTableBeforeDiscardingResult()
    {
        using var cancellation = new CancellationTokenSource();
        var table = Students();
        var disposed = false;
        table.Disposed += (_, _) => disposed = true;
        var repository = new TimetableDirectoryRepository(() => { cancellation.Cancel(); return Task.FromResult(table); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.LoadStudentsAsync(cancellation.Token));
        Assert.True(disposed);
    }

    [Theory]
    [InlineData("ma_sv")]
    [InlineData("ho_ten")]
    [InlineData("ma_lop")]
    [InlineData("ten_lop")]
    public async Task MalformedSqlResponseFailsWithoutReturningPartialDirectory(string missing)
    {
        var table = Students();
        table.Columns.Remove(missing);
        var disposed = false;
        table.Disposed += (_, _) => disposed = true;
        var repository = new TimetableDirectoryRepository(() => Task.FromResult(table));
        await Assert.ThrowsAsync<PersistenceException>(() => repository.LoadStudentsAsync());
        Assert.True(disposed);
    }

    [Fact]
    public async Task BlankStudentIdentityAndDuplicateIdentityAreRejected()
    {
        using var blank = Students();
        blank.Rows[0]["ma_sv"] = DBNull.Value;
        await Assert.ThrowsAsync<PersistenceException>(() => new TimetableDirectoryRepository(() => Task.FromResult(blank)).LoadStudentsAsync());
        using var duplicate = Students();
        duplicate.Rows[1]["ma_sv"] = "sv1";
        await Assert.ThrowsAsync<PersistenceException>(() => new TimetableDirectoryRepository(() => Task.FromResult(duplicate)).LoadStudentsAsync());
    }

    [Fact]
    public async Task EmptyDirectoryIsValidAndPersistenceFailurePropagates()
    {
        var empty = Students();
        empty.Rows.Clear();
        Assert.Empty(await new TimetableDirectoryRepository(() => Task.FromResult(empty)).LoadStudentsAsync());
        var error = new PersistenceException("Không tải được danh sách.");
        Assert.Same(error, await Assert.ThrowsAsync<PersistenceException>(() =>
            new TimetableDirectoryRepository(() => Task.FromException<DataTable>(error)).LoadStudentsAsync()));
    }
}
