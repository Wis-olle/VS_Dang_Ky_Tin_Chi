using System.Security.Cryptography;
using QuanLyGiangVien.Data.Sql;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class PasswordLoginTests
{
    [Fact]
    public void ProofUsesPasswordSaltAndOneTimeServerNonce()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(32);
        var proof = PasswordProof.Create("Test-only-password", salt, nonce, 600_000);
        var key = Rfc2898DeriveBytes.Pbkdf2("Test-only-password", salt, 600_000, HashAlgorithmName.SHA256, 32);
        Assert.Equal(SHA256.HashData(nonce.Concat(key).ToArray()), proof);
        Assert.NotEqual(proof, PasswordProof.Create("Other-password", salt, nonce, 600_000));
        Assert.NotEqual(proof, PasswordProof.Create("Test-only-password", salt, RandomNumberGenerator.GetBytes(32), 600_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2_000_001)]
    public void UntrustedIterationCountsAreRejected(int iterations) =>
        Assert.Throws<ArgumentException>(() => PasswordProof.Create("x", new byte[16], new byte[32], iterations));

    [Fact]
    public void PortalsRejectWrongAndUnknownRoles()
    {
        Assert.True(PasswordLogin.Allows(LoginPortal.Student, "SINH_VIEN"));
        Assert.True(PasswordLogin.Allows(LoginPortal.Training, "DAO_TAO"));
        Assert.True(PasswordLogin.Allows(LoginPortal.Training, "QUAN_TRI"));
        Assert.False(PasswordLogin.Allows(LoginPortal.Student, "DAO_TAO"));
        Assert.False(PasswordLogin.Allows(LoginPortal.Training, "SINH_VIEN"));
        Assert.False(PasswordLogin.Allows(LoginPortal.Training, "Admin"));
    }
}
