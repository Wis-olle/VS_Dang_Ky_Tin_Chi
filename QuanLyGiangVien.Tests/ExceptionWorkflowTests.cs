using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class ExceptionWorkflowTests
{
    private static ExceptionApplication Valid() => new("MAX_CREDITS", "HK1-2627", null, null, null, "Xin học vượt theo kế hoạch");

    [Theory]
    [InlineData("OTHER")]
    [InlineData("")]
    [InlineData("DROP TABLE")]
    public void RejectsUnknownExceptionTypes(string kind) =>
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Application(Valid() with { Kind = kind }));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void SubmissionRequiresReason(string reason) =>
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Application(Valid() with { Reason = reason }));

    [Fact]
    public void RejectsOverlongReason() => Assert.Throws<ArgumentException>(() =>
        ExceptionWorkflowValidation.Application(Valid() with { Reason = new string('x', 1001) }));

    [Fact]
    public void CreditRequestCannotBecomeWildcardPrerequisiteGrant() => Assert.Throws<ArgumentException>(() =>
        ExceptionWorkflowValidation.Application(Valid() with { RuleCode = "GROUP_1" }));

    [Fact]
    public void RequirementRequestMustNameCourseAndRule()
    {
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Application(Valid() with { Kind = "REQUIREMENT" }));
        ExceptionWorkflowValidation.Application(Valid() with { Kind = "REQUIREMENT", Course = "IT101", RuleCode = "GROUP_1" });
    }

    [Theory]
    [InlineData("LATE_REGISTER")]
    [InlineData("WITHDRAW")]
    public void OperationalStudentExceptionsRequireExactClass(string kind)
    {
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Application(Valid() with { Kind = kind }));
        ExceptionWorkflowValidation.Application(Valid() with { Kind = kind, ClassId = "LHP0001" });
    }

    [Fact]
    public void SubmitCommandUsesParameters()
    {
        using var cmd = ExceptionWorkflowRepository.SubmitCommand(Valid() with { Reason = "Lý do O'Brien" });
        Assert.Equal("dbo.usp_DK3_ExceptionSubmit", cmd.CommandText);
        Assert.Equal(CommandType.StoredProcedure, cmd.CommandType);
        Assert.Equal("Lý do O'Brien", cmd.Parameters["@reason"].Value);
        Assert.DoesNotContain("O'Brien", cmd.CommandText);
    }

    [Fact]
    public void ApprovalRequiresBoundedExpiryUsesAndCreditLimit()
    {
        var decision = new ExceptionDecision(1, "0102030405060708", "APPROVE", "Đồng ý theo hồ sơ", DateTime.UtcNow.AddDays(7), 1, 30);
        ExceptionWorkflowValidation.Decision(decision);
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Decision(decision with { ExpiresUtc = null }));
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Decision(decision with { Uses = 0 }));
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Decision(decision with { Uses = 101 }));
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Decision(decision with { MaxCredits = 1001 }));
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("FFFFFFFFFFFFFFFF00")]
    public void DecisionsRequireExactRowVersion(string version) => Assert.Throws<ArgumentException>(() =>
        ExceptionWorkflowValidation.Decision(new(1, version, "REJECT", "Không đủ hồ sơ")));

    [Fact]
    public void RejectAndRevokeDoNotIssueGrantFields()
    {
        ExceptionWorkflowValidation.Decision(new(1, "0102030405060708", "REVOKE", "Thu hồi"));
        Assert.Throws<ArgumentException>(() => ExceptionWorkflowValidation.Decision(new(1, "0102030405060708", "REJECT", "Từ chối", Uses: 1)));
    }

    [Fact]
    public void DecisionCommandPreservesBinaryVersionAndServerChecksExpiry()
    {
        using var cmd = ExceptionWorkflowRepository.DecideCommand(new(2, "0102030405060708", "APPROVE", "Đồng ý", new DateTime(2030,1,1,0,0,0,DateTimeKind.Utc), 2, 24));
        Assert.Equal(SqlDbType.Binary, cmd.Parameters["@version"].SqlDbType);
        Assert.Equal(8, cmd.Parameters["@version"].Size);
        Assert.Equal(new byte[] {1,2,3,4,5,6,7,8}, cmd.Parameters["@version"].Value);
    }
}
