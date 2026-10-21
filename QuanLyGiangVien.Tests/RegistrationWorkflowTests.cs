using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;
using Xunit;

namespace QuanLyGiangVien.Tests;
public sealed class RegistrationWorkflowTests
{
    [Fact] public void ClassesAreCanonicalAndBounded()
    {
        Assert.Equal("[\"A\",\"B\"]", RegistrationWorkflowValidation.ClassesJson(new[]{" B ","A"}));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowValidation.ClassesJson(new[]{"A","a"}));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowValidation.ClassesJson(Enumerable.Range(0,31).Select(i=>"L"+i)));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowValidation.ClassesJson(new[]{"';--"}));
    }
    [Fact] public void RemovalIdentifiersArePositiveUniqueAndBounded()
    {
        Assert.Equal("[1,2]",RegistrationWorkflowValidation.RemovalsJson(new long[]{2,1}));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowValidation.RemovalsJson(new long[]{1,1}));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowValidation.RemovalsJson(new long[]{0}));
    }
    [Fact] public void SubmissionUsesTypedParametersAndRequiresANonemptyRequest()
    {
        var id=Guid.NewGuid();
        using var command=RegistrationWorkflowCommands.Submit(id,"HK1",new[]{"L1"},Array.Empty<long>(),null);
        Assert.Equal(CommandType.StoredProcedure,command.CommandType);
        Assert.Equal(SqlDbType.UniqueIdentifier,command.Parameters["@request_id"].SqlDbType);
        Assert.Equal(id,command.Parameters["@request_id"].Value);
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowCommands.Submit(Guid.Empty,"HK1",new[]{"L1"},Array.Empty<long>(),null));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowCommands.Submit(id,"HK1",Array.Empty<string>(),Array.Empty<long>(),null));
    }
    [Fact] public void VersionsAreExactlyEightBytesAndCopied()
    {
        Assert.Null(RegistrationWorkflowValidation.Version(null));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowValidation.Version(new byte[7]));
        var source=new byte[8];var copy=RegistrationWorkflowValidation.Version(source)!;source[0]=1;Assert.Equal(0,copy[0]);
    }
    [Fact] public void CartCommandsCarryOptimisticVersion()
    {
        using var command=RegistrationWorkflowCommands.SaveCart("HK1",new[]{"L1"},new byte[8]);
        Assert.Equal(SqlDbType.Binary,command.Parameters["@version"].SqlDbType);
        Assert.Equal(8,command.Parameters["@version"].Size);
        Assert.Equal("[\"L1\"]",command.Parameters["@class_json"].Value);
    }
    [Fact] public void CombinationRequiresExplicitDistinctComponentRoles()
    {
        var roles=new Dictionary<string,string>{{"L1","LT"},{"L2","TH"}};
        using var command=RegistrationWorkflowCommands.SaveCombination("PAIR","HK1","IT101",new[]{"L1","L2"},null,roles);
        Assert.Equal("{\"L1\":\"LT\",\"L2\":\"TH\"}",command.Parameters["@roles_json"].Value);
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowCommands.SaveCombination("PAIR","HK1","IT101",new[]{"L1","L2"},null,null));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowCommands.SaveCombination("PAIR","HK1","IT101",new[]{"L1","L2"},null,new Dictionary<string,string>{{"L1","LT"},{"L2","LT"}}));
        Assert.Throws<ArgumentException>(()=>RegistrationWorkflowCommands.SaveCombination("PAIR","HK1","IT101",new[]{"L1","L2"},null,new Dictionary<string,string>{{"L1","LT"},{"L3","TH"}}));
    }
}
