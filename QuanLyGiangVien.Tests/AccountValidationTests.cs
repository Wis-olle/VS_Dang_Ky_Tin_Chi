using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;
public sealed class AccountValidationTests
{
    [Theory]
    [InlineData(0)] [InlineData(11)] [InlineData(257)]
    public void PasswordRejectsWrongLength(int length) => Assert.Throws<ArgumentException>(() => AccountValidation.Password(new string('a', length)));
    [Theory] [InlineData(12)] [InlineData(256)]
    public void PasswordAcceptsBoundaries(int length) => AccountValidation.Password(new string('a', length));
    [Fact]
    public void NewStudentDraftNormalizesWithoutMutating()
    {
        var source = new AccountDraft(null, " user ", "SINH_VIEN", "SV2024001", true, null, " Tạo mới ");
        var result = AccountValidation.Validate(source);
        Assert.Equal("user", result.Username); Assert.Equal("Tạo mới", result.Reason); Assert.Equal(" user ", source.Username);
    }
    [Theory]
    [InlineData("", "SINH_VIEN", "SV2024001", "reason")]
    [InlineData("user", "OWNER", null, "reason")]
    [InlineData("user", "SINH_VIEN", null, "reason")]
    [InlineData("user", "SINH_VIEN", "SV1", "")]
    public void InvalidDraftIsRejected(string name, string role, string? student, string reason) =>
        Assert.Throws<ArgumentException>(() => AccountValidation.Validate(new(null, name, role, student, true, null, reason)));
    [Fact] public void ExistingDraftNeedsVersion() => Assert.Throws<ArgumentException>(() => AccountValidation.Validate(new(1, "user", "SINH_VIEN", "SV1", true, null, "reason")));
}
