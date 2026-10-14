using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SqlIdentityTests
{
    [Fact]
    public void IdentityCommandUsesOriginalHueProcedureWithoutRoleOrStudentParameters()
    {
        using var command = SqlIdentityRepository.Command();
        Assert.Equal("dbo.usp_LayNguoiDung", command.CommandText);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Empty(command.Parameters);
    }
    [Theory]
    [InlineData("DAO_TAO", null)]
    [InlineData("SINH_VIEN", "SV01")]
    public void IdentityDoesNotRequireSchedulingCatalog(string role, string? student)
    {
        using var table = Table(); table.Rows.Add(1, student is null ? DBNull.Value : student, role, "Tên người dùng");
        using var reader = table.CreateDataReader();
        var identity = SqlIdentityRepository.Read(reader);
        Assert.Equal(role, identity.Role); Assert.Equal(student, identity.StudentId);
    }
    [Fact]
    public void StudentIdentityWithoutLinkedStudentIsRejected()
    {
        using var table = Table(); table.Rows.Add(1, DBNull.Value, "SINH_VIEN", "Tên");
        using var reader = table.CreateDataReader();
        Assert.Throws<PersistenceException>(() => SqlIdentityRepository.Read(reader));
    }
    [Fact]
    public void MissingAndDuplicateIdentitiesAreRejected()
    {
        using var table = Table(); using var empty = table.CreateDataReader();
        Assert.Throws<PersistenceException>(() => SqlIdentityRepository.Read(empty));
        table.Rows.Add(1, DBNull.Value, "DAO_TAO", "Tên"); table.Rows.Add(2, DBNull.Value, "DAO_TAO", "Tên");
        using var duplicate = table.CreateDataReader();
        Assert.Throws<PersistenceException>(() => SqlIdentityRepository.Read(duplicate));
    }
    private static DataTable Table()
    {
        var table = new DataTable(); table.Columns.Add("ma_tk", typeof(int)); table.Columns.Add("ma_sv");
        table.Columns.Add("vai_tro"); table.Columns.Add("ho_ten"); return table;
    }
}
