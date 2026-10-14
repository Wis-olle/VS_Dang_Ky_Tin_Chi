using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class RegistrationValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901")]
    [InlineData("HK\n01")]
    [InlineData("học-kỳ")]
    public void InvalidCodesAreRejectedBeforeOpeningSql(string? value) =>
        Assert.Throws<ArgumentException>(() => RegistrationValidation.RequiredCode(value));

    [Theory]
    [InlineData(" HK-2026_1 ", "HK-2026_1")]
    [InlineData("SV0001", "SV0001")]
    public void ValidCanonicalCodesAreTrimmed(string value, string expected) =>
        Assert.Equal(expected, RegistrationValidation.RequiredCode(value));

    [Fact]
    public void KeywordLengthIsValidatedWithoutRewritingLiteralSearchText()
    {
        Assert.Equal("%_ môn", RegistrationValidation.Keyword(" %_ môn "));
        Assert.Equal("", RegistrationValidation.Keyword(null));
        Assert.Throws<ArgumentException>(() => RegistrationValidation.Keyword(new string('a', 101)));
    }

    [Fact]
    public void ThresholdAndRegistrationIdentifiersFailFast()
    {
        Assert.Null(RegistrationValidation.Threshold(null));
        Assert.Equal(0, RegistrationValidation.Threshold(0));
        Assert.Throws<ArgumentException>(() => RegistrationValidation.Threshold(-1));
        Assert.Throws<ArgumentException>(() => RegistrationValidation.RegistrationId(0));
        Assert.Throws<ArgumentException>(() => RegistrationValidation.RegistrationId(-1));
        Assert.Equal(long.MaxValue, RegistrationValidation.RegistrationId(long.MaxValue));
    }

    [Fact]
    public void DeadlineUsesServerTimeAndExclusiveEnds()
    {
        var start = new DateTime(2026, 10, 15, 8, 0, 0);
        var deadline = new RegistrationDeadline(start, start.AddHours(1), start.AddMinutes(30), start, "DANG_MO");
        Assert.True(deadline.CanRegister(TimeSpan.Zero));
        Assert.True(deadline.CanCancel(TimeSpan.Zero));
        Assert.False(deadline.CanCancel(TimeSpan.FromMinutes(30)));
        Assert.True(deadline.CanRegister(TimeSpan.FromMinutes(30)));
        Assert.False(deadline.CanRegister(TimeSpan.FromHours(1)));
        Assert.False((deadline with { Status = "DA_DONG" }).CanRegister(TimeSpan.Zero));
        Assert.False((deadline with { ServerTime = start.AddSeconds(-1) }).CanRegister(TimeSpan.Zero));
        Assert.False((deadline with { Status = "DONG" }).CanCancel(TimeSpan.Zero));
        Assert.False((deadline with { ServerTime = start.AddSeconds(-1) }).CanCancel(TimeSpan.Zero));
        Assert.Equal(start, deadline.Now(TimeSpan.FromMinutes(-1)));
    }

    [Theory]
    [InlineData("SINH_VIEN", true, false)]
    [InlineData("DAO_TAO", false, true)]
    [InlineData("QUAN_TRI", false, true)]
    [InlineData("unknown", false, false)]
    public void UiRoleProjectionFailsClosedForUnknownRoles(string role, bool student, bool management)
    {
        var user = new RegistrationUser("1", null, role, "Test");
        Assert.Equal(student, user.LaSinhVien);
        Assert.Equal(management, user.LaDaoTao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    [InlineData("Server=localhost")]
    [InlineData("Database=Example")]
    public void IncompleteConnectionConfigurationFailsWithoutConnecting(string connection) =>
        Assert.Throws<QuanLyGiangVien.Domain.PersistenceException>(() => new RegistrationRepository(connection));

    [Fact]
    public void LookupCommandsKeepAllUserInputInTypedParameters()
    {
        using var command = RegistrationCommands.Lookup("HK01", "'; drop table DANG_KY;--", true);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Equal("dbo.usp_TraCuuLopDangMo", command.CommandText);
        Assert.Equal(SqlDbType.NVarChar, command.Parameters["@tu_khoa"].SqlDbType);
        Assert.Equal(100, command.Parameters["@tu_khoa"].Size);
        Assert.Equal("'; drop table DANG_KY;--", command.Parameters["@tu_khoa"].Value);
        Assert.Equal(true, command.Parameters["@chi_con_cho"].Value);
        Assert.DoesNotContain("drop", command.CommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StudentResultsDoNotSendSpoofedStudentIdentity()
    {
        using var command = RegistrationCommands.Results("HK01", null, false);
        Assert.Equal(DBNull.Value, command.Parameters["@ma_sv"].Value);
        Assert.DoesNotContain(command.Parameters.Cast<Microsoft.Data.SqlClient.SqlParameter>(),
            p => p.ParameterName.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WritesOnlyCallOriginalHueProceduresAndUseCanonicalIdentifiers()
    {
        using var register = RegistrationCommands.Register("LHP01");
        using var cancel = RegistrationCommands.Cancel(1234567890123);
        Assert.Equal("dbo.usp_DangKyHocPhan", register.CommandText);
        Assert.Single(register.Parameters.Cast<Microsoft.Data.SqlClient.SqlParameter>());
        Assert.Equal(SqlDbType.BigInt, cancel.Parameters["@ma_dk"].SqlDbType);
        Assert.Equal(1234567890123L, cancel.Parameters["@ma_dk"].Value);
        Assert.Equal("dbo.usp_HuyDangKy", cancel.CommandText);
    }

    [Theory]
    [InlineData(null, "\"\"")]
    [InlineData("", "\"\"")]
    [InlineData("simple", "\"simple\"")]
    [InlineData("Nguyễn, \"An\"", "\"Nguyễn, \"\"An\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("  +SUM(1)", "\"'  +SUM(1)\"")]
    [InlineData("\t@cmd", "\"'\t@cmd\"")]
    public void CsvEscapesQuotesAndPreventsSpreadsheetFormulaExecution(string? input, string expected) =>
        Assert.Equal(expected, RegistrationCsv.Escape(input));

    [Theory]
    [InlineData(51001, "Tài khoản")]
    [InlineData(51002, "Vai trò")]
    [InlineData(51003, "Hồ sơ")]
    [InlineData(51004, "Học kỳ")]
    [InlineData(51005, "sinh viên")]
    [InlineData(51006, "bận")]
    [InlineData(51007, "Lớp học phần")]
    [InlineData(51008, "học kỳ")]
    [InlineData(51009, "hạn")]
    [InlineData(51010, "không nhận")]
    [InlineData(51011, "Học phần")]
    [InlineData(51012, "lịch học")]
    [InlineData(51013, "đã đăng ký")]
    [InlineData(51014, "sĩ số")]
    [InlineData(51016, "trùng lịch")]
    [InlineData(51017, "không thuộc")]
    [InlineData(51018, "hạn hủy")]
    [InlineData(51019, "mã sinh viên")]
    [InlineData(51020, "phòng đào tạo")]
    [InlineData(51021, "Ngưỡng")]
    [InlineData(229, "quyền")]
    [InlineData(18456, "quyền")]
    [InlineData(-2, "tải lại")]
    [InlineData(99999, "SQL")]
    public void ErrorMessagesUseNumberWhitelistWithoutRawServerText(int number, string expected) =>
        Assert.Contains(expected, RegistrationErrorMessages.ForNumber(number), StringComparison.OrdinalIgnoreCase);
}
