using QuanLyGiangVien.Domain.Academics;
using QuanLyGiangVien.Data.Sql;
using Xunit;
namespace QuanLyGiangVien.Tests;
public sealed class AcademicTests
{
 [Theory] [InlineData("bad code")] [InlineData("")] [InlineData("CÓ_DẤU")]
 public void RejectsInvalidIdentifiers(string id) => Assert.Throws<ArgumentException>(() => AcademicValidation.Code(id));
 [Theory] [InlineData("PROGRAM")] [InlineData("POLICY")] [InlineData("RESULT")] [InlineData("HOLD")]
 public void ChangesUseParameterizedJson(string kind)
 {
  using var command=AcademicRepository.SaveCommand(new(kind,"A1",null,"{\"name\":\"O'Brien\"}"));
  Assert.Equal("dbo.usp_HocVu_Save",command.CommandText);
  Assert.Equal("{\"name\":\"O'Brien\"}",command.Parameters["@payload"].Value);
 }
 [Theory] [InlineData("{}")] [InlineData("[]")] [InlineData("not json")]
 public void RejectsInvalidChange(string json) => Assert.Throws<ArgumentException>(() => AcademicValidation.Change(new("UNKNOWN","X",null,json)));
 [Fact] public void RejectsUnboundedBatch() => Assert.Throws<ArgumentException>(()=>AcademicValidation.Classes(Enumerable.Range(0,31).Select(x=>"L"+x).ToArray()));
 [Fact] public void NormalizesBatchWithoutMutatingInput()
 { string[] source=[" l1 ","L2"]; Assert.Equal(new[]{"L1","L2"},AcademicValidation.Classes(source)); Assert.Equal(" l1 ",source[0]); }
 [Fact] public void DetectsPrerequisiteCyclesButAllowsCorequisiteCycles()
 {
  var rules=new[]{new AcademicEdge("A","B","PRE"),new AcademicEdge("B","A","PRIOR")};
  Assert.False(AcademicValidation.Acyclic(rules));
  Assert.True(AcademicValidation.Acyclic(rules.Select(x=>x with {Kind="CO"})));
 }
}
