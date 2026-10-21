using System.Reflection;
using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SessionFailureTests
{
    [Fact]
    public void AuthSessionErrorHasItsOwnNamespaceAndIsNotCatalogDeleteError()
    {
        Assert.Contains("Phiên", SqlErrorMessages.ForNumber(52405));
        var catalogError = typeof(CatalogRepository).GetMethod("Error", BindingFlags.Static | BindingFlags.NonPublic)!;
        var expired = (string)catalogError.Invoke(null, [52405])!;
        var inUse = (string)catalogError.Invoke(null, [52105])!;
        Assert.Contains("Phiên", expired);
        Assert.DoesNotContain("không thể xóa", expired);
        Assert.Contains("không thể xóa", inUse);
    }
}
