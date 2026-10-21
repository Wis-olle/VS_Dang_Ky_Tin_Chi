using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Data.Sql;
using Xunit;
namespace QuanLyGiangVien.Tests;

public sealed class SeatAllocationTests
{
    [Theory] [InlineData("unknown")] [InlineData("")]
    public void RejectsUnknownConfigurationKind(string kind) => Assert.Throws<ArgumentException>(() =>
        SeatAllocationValidation.Change(new(kind,"X",null,"{}")));
    [Theory] [InlineData("[]")] [InlineData("null")] [InlineData("bad")]
    public void ConfigurationRequiresBoundedObject(string json) => Assert.Throws<ArgumentException>(() =>
        SeatAllocationValidation.Change(new("ROUND","R1",null,json)));
    [Fact] public void ConfigurationIsParameterizedAndVersioned()
    {
        using var c=SeatAllocationRepository.SaveCommand(new("GROUP","G1","0102030405060708","{\"name\":\"O'Brien\"}"));
        Assert.Equal("dbo.usp_Seat_Save",c.CommandText);Assert.DoesNotContain("O'Brien",c.CommandText);
        Assert.Equal("{\"name\":\"O'Brien\"}",c.Parameters["@payload"].Value);
        Assert.Equal(8,c.Parameters["@version"].Size);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void WaitlistIdsMustBePositive(long id) => Assert.Throws<ArgumentException>(()=>SeatAllocationValidation.Entry(id));
    [Fact] public void QueueCanonicalizationPreservesInputAndRejectsDuplicates()
    {
        string[] input=["LHP0002","LHP0001"];
        Assert.Equal("[\"LHP0001\",\"LHP0002\"]",SeatAllocationValidation.Classes(input));
        Assert.Equal("LHP0002",input[0]);
        Assert.Throws<ArgumentException>(()=>SeatAllocationValidation.Classes(["LHP0001","LHP0001"]));
    }
    [Fact] public void WorkerBatchHasHardLimit()
    {
        Assert.Equal(100,SeatAllocationValidation.Batch(100));
        Assert.Throws<ArgumentException>(()=>SeatAllocationValidation.Batch(101));
    }
    [Fact] public void OfferClaimIdentityIsStableAcrossRetriesAndRestarts()
    {
        var first=SeatAllocationValidation.ClaimRequest("4",17,"010203040506070a");
        Assert.Equal(first,SeatAllocationValidation.ClaimRequest("4",17,"010203040506070A"));
        Assert.NotEqual(first,SeatAllocationValidation.ClaimRequest("5",17,"010203040506070A"));
        Assert.NotEqual(first,SeatAllocationValidation.ClaimRequest("4",18,"010203040506070A"));
    }
    [Fact] public void OfferClaimRejectsMissingOrInvalidVersion()
    {
        Assert.Throws<ArgumentException>(()=>SeatAllocationValidation.ClaimRequest("4",17,"bad"));
        Assert.Throws<ArgumentException>(()=>SeatAllocationValidation.ClaimRequest("4",0,"010203040506070A"));
    }
}
