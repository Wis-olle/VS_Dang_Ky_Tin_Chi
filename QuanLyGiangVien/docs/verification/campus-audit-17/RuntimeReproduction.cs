using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;

const string cs="Server=.;Database=DangKyTinChi_Minh_Test_Audit20261016_Auth;Integrated Security=true;TrustServerCertificate=true";
await using var cn=new SqlConnection(cs); await cn.OpenAsync();
var token=RandomNumberGenerator.GetBytes(32);
using(var setup=new SqlCommand("IF DB_NAME()<>'DangKyTinChi_Minh_Test_Audit20261016_Auth' THROW 52999,'Audit only',1; INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires) SELECT HASHBYTES('SHA2_256',@token),ma_tk,ORIGINAL_LOGIN(),DATEADD(MINUTE,5,SYSUTCDATETIME()) FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='sv2024001';",cn))
{ setup.Parameters.AddWithValue("@token",token); await setup.ExecuteNonQueryAsync(); }
var set=typeof(AppSqlConnection).GetMethod("SetSession",BindingFlags.Static|BindingFlags.NonPublic)!;
var take=typeof(AppSqlConnection).GetMethod("TakeSession",BindingFlags.Static|BindingFlags.NonPublic)!;
set.Invoke(null,[cs,token]);
try {
 var snap=await new SqlMinhRepository(cs).LoadAsync();
 using var total=new SqlCommand("SELECT COUNT(*) FROM dbo.GIANG_VIEN",cn);
 var all=(int)(await total.ExecuteScalarAsync())!;
 var relevant=snap.Classes.Select(x=>x.LecturerId).Distinct().Count();
 Console.WriteLine($"STUDENT_SNAPSHOT role={snap.Identity.Role}; lecturersReturned={snap.Lecturers.Length}; lecturersInDatabase={all}; lecturersInStudentClasses={relevant}; personalFieldsInDTO=BirthDate,Gender,Email,Phone");
 if(snap.Identity.Role!="SINH_VIEN"||snap.Lecturers.Length!=all||all<=relevant)throw new Exception("PII reproduction inconclusive");
 using(var expire=new SqlCommand("UPDATE dbo.APP_SESSION SET expires=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE token_hash=HASHBYTES('SHA2_256',@token)",cn))
 {expire.Parameters.AddWithValue("@token",token);await expire.ExecuteNonQueryAsync();}
 try { using var data=await new CatalogRepository(cs).FacultiesAsync(); throw new Exception("Expected expired session rejection"); }
 catch(PersistenceException ex) {
  Console.WriteLine("EXPIRED_CATALOG_MESSAGE="+ex.Message);
  if(!ex.Message.Contains("không thể xóa"))throw;
 }
} finally {
 take.Invoke(null,[cs]);
 using var remove=new SqlCommand("DELETE dbo.APP_SESSION WHERE token_hash=HASHBYTES('SHA2_256',@token)",cn);
 remove.Parameters.AddWithValue("@token",token);await remove.ExecuteNonQueryAsync();CryptographicOperations.ZeroMemory(token);
}
// Windows test identity is mapped to Training by the isolated bootstrap.
var baseline=await new SqlMinhRepository(cs).LoadAsync();
Console.WriteLine("ROOM_BASELINE_ROLE="+baseline.Identity.Role);
using(var room=new SqlCommand("EXEC dbo.usp_Minh_LuuPhong @ma_phong='AUDIT501',@toa_nha=N'Audit',@suc_chua=501,@loai_phong=N'Lý thuyết',@trang_thai=1,@la_moi=1",cn)) await room.ExecuteNonQueryAsync();
try {
 try { await new SqlMinhRepository(cs).LoadAsync(); throw new Exception("Expected room contract rejection"); }
 catch(PersistenceException ex) {Console.WriteLine("SQL_ACCEPTS_501_BUT_SNAPSHOT_REJECTS="+ex.Message);}
 using(var shrink=new SqlCommand("UPDATE dbo.PHONG_HOC SET suc_chua=500 WHERE ma_phong='AUDIT501'",cn))await shrink.ExecuteNonQueryAsync();
 var recovered=await new SqlMinhRepository(cs).LoadAsync();
 if(!recovered.Rooms.Any(x=>x.Id=="AUDIT501"&&x.Capacity==500))throw new Exception("500 control case failed");
 Console.WriteLine("SAME_ROOM_AT_500_SNAPSHOT_SUCCEEDS=true");
} finally {
 using var delete=new SqlCommand("DELETE dbo.PHONG_HOC WHERE ma_phong='AUDIT501'",cn);await delete.ExecuteNonQueryAsync();
}
