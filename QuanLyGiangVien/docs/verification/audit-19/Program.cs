using QuanLyGiangVien.Domain.Academics;
using System.Text.Json;
var findings=new List<object>();
var zero=new DegreeCurriculum("CT1","0000000000000001",3,true,[new("G0","Nhóm bổ sung",0)],[new("A","A",null,3,true)]);
try { DegreeAudit.Calculate(zero,[new("A",3,"DONE")],[]);findings.Add(new{caseName="ZeroCreditGroup",actual="No error"}); }
catch(ArgumentException e){ findings.Add(new{caseName="ZeroCreditGroup",expected="Published SQL curriculum remains readable",actual=e.Message}); }
var cap=new DegreeCurriculum("CT2","0000000000000001",6,true,[new("G1","Nhóm",3)],[new("A","A","G1",3,true),new("B","B","G1",3,true)]);
var capResult=DegreeAudit.Calculate(cap,[new("A",3,"DONE"),new("B",3,"DONE")],[]);
findings.Add(new{caseName="MinimumGroupUsedAsCapacity",expectedAllocatedDone=6,expectedSatisfied=true,actualAllocatedDone=capResult.AllocatedDone,actualSatisfied=capResult.Satisfied,rows=capResult.Courses});
var stages=new DegreeCurriculum("CT3","0000000000000001",6,true,[new("G1","Nhóm 1",3),new("G2","Nhóm 2",3)],[new("A","A","G1",3,true),new("B","B","G2",3,true)]);
var staged=DegreeAudit.Calculate(stages,[new("X",3,"DONE"),new("Y",3,"IN_PROGRESS")],[new("X","A"),new("X","B"),new("Y","A")]);
findings.Add(new{caseName="CrossStageRerouting",expectedDone=3,expectedInProgress=3,actualDone=staged.AllocatedDone,actualInProgress=staged.AllocatedInProgress,allocations=staged.Allocations});
Console.WriteLine(JsonSerializer.Serialize(findings,new JsonSerializerOptions{WriteIndented=true}));
