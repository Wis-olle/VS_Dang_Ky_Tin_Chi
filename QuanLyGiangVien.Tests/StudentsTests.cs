using System.Data;
using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;

public class StudentsTests
{
    private static StudentRecord Student => new("SV2024001", "Sinh viên mẫu", null, null, null, null, null, "CNTT", "CNTT01", true);
    [Fact]
    public void SaveUsesStoredProcedureAndTypedNullableParameters()
    {
        using var command = StudentRepository.SaveCommand(Student, true);
        Assert.Equal("dbo.usp_SinhVien_Luu", command.CommandText);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Equal(SqlDbType.Date, command.Parameters["@ngay_sinh"].SqlDbType);
        Assert.Equal(DBNull.Value, command.Parameters["@ngay_sinh"].Value);
        Assert.Equal(DBNull.Value, command.Parameters["@email"].Value);
        Assert.Equal(SqlDbType.VarChar, command.Parameters["@ma_sv"].SqlDbType);
        Assert.Equal(20, command.Parameters["@ma_sv"].Size);
        Assert.Equal(true, command.Parameters["@la_moi"].Value);
    }
    [Fact]
    public void ReadAllowsAllNullableMainSchemaFields()
    {
        var table = new DataTable();
        foreach (var field in new[] { "ma_sv", "ho_ten", "ngay_sinh", "gioi_tinh", "email", "sdt", "dia_chi", "ma_nganh", "ma_lop", "trang_thai" }) table.Columns.Add(field, field == "ngay_sinh" ? typeof(DateTime) : field == "trang_thai" ? typeof(bool) : typeof(string));
        table.Rows.Add("SV2024001", "Sinh viên mẫu", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, "CNTT", "CNTT01", true);
        Assert.Equal(Student, StudentRepository.FromRow(table.Rows[0]));
    }
    [Theory]
    [InlineData("", "Sinh viên", null)]
    [InlineData("SV 1", "Sinh viên", null)]
    [InlineData("SV01", "", null)]
    [InlineData("SV01", "Sinh viên", "email-khong-hop-le")]
    public void InvalidInputRejectedBeforeOpeningConnection(string code, string name, string? email) =>
        Assert.Throws<ArgumentException>(() => StudentRepository.Validate(Student with { Code = code, Name = name, Email = email }));
    [Fact]
    public void FutureBirthDateRejected() => Assert.Throws<ArgumentException>(() => StudentRepository.Validate(Student with { BirthDate = DateTime.Today.AddDays(1) }));
}
