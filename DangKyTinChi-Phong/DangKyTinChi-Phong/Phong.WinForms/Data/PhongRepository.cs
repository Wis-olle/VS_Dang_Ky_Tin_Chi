using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
namespace DangKyTinChi.Phong.Data
{
 public sealed class PhongRepository
 {
  private readonly string connectionString;
  public PhongRepository(string connectionString) { this.connectionString=connectionString; }
  public async Task TestAsync()
  {
   using(var cn=new SqlConnection(connectionString))
   using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.usp_HocPhan_Luu',N'P') IS NOT NULL AND OBJECT_ID(N'dbo.usp_HocKy_Luu',N'P') IS NOT NULL AND OBJECT_ID(N'dbo.usp_HocKy_DoiTrangThai',N'P') IS NOT NULL AND OBJECT_ID(N'dbo.usp_HocPhan_DanhSach',N'P') IS NOT NULL AND OBJECT_ID(N'dbo.usp_HocKy_DanhSach',N'P') IS NOT NULL THEN 1 ELSE 0 END",cn))
   {
    await cn.OpenAsync().ConfigureAwait(false);
    if(Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false))!=1)
     throw new InvalidOperationException("Database chưa có đủ module Phong. Chạy 01 và 02 trong thư mục database trước.");
   }
  }
  private async Task<DataTable> QueryAsync(string proc,params SqlParameter[] ps)
  {
   using(var cn=new SqlConnection(connectionString))
   using(var cmd=new SqlCommand(proc,cn) { CommandType=CommandType.StoredProcedure,CommandTimeout=15 })
   {
    cmd.Parameters.AddRange(ps); await cn.OpenAsync().ConfigureAwait(false);
    using(var reader=await cmd.ExecuteReaderAsync().ConfigureAwait(false))
    { var dt=new DataTable(); dt.Load(reader); return dt; }
   }
  }
  private async Task ExecuteAsync(string proc,params SqlParameter[] ps)
  {
   using(var cn=new SqlConnection(connectionString))
   using(var cmd=new SqlCommand(proc,cn) { CommandType=CommandType.StoredProcedure,CommandTimeout=15 })
   { cmd.Parameters.AddRange(ps); await cn.OpenAsync().ConfigureAwait(false); await cmd.ExecuteNonQueryAsync().ConfigureAwait(false); }
  }
  private static SqlParameter P(string name,SqlDbType type,object value,int size=0)
  {
   var p=new SqlParameter(name,type) {Value=value??DBNull.Value}; if(size>0)p.Size=size; return p;
  }
  private static SqlParameter Time(string name,DateTime dt)
  { return new SqlParameter(name,SqlDbType.DateTime2) { Scale=0,Value=dt }; }
  public Task<DataTable> HocPhanAsync(string query,string type,bool? active)
  { return QueryAsync("dbo.usp_HocPhan_DanhSach",P("@tu_khoa",SqlDbType.NVarChar,query,200),P("@loai_hp",SqlDbType.VarChar,type,20),P("@trang_thai",SqlDbType.Bit,active)); }
  public Task SaveHocPhanAsync(HocPhan hp,bool isNew)
  {
   Validation.HocPhan(hp);
   return ExecuteAsync("dbo.usp_HocPhan_Luu",P("@la_moi",SqlDbType.Bit,isNew),P("@ma_hp",SqlDbType.VarChar,hp.Ma,20),P("@ten_hp",SqlDbType.NVarChar,hp.Ten,200),
    P("@so_tin_chi",SqlDbType.TinyInt,hp.TinChi),P("@loai_hp",SqlDbType.VarChar,hp.Loai,20),P("@tin_chi_min",SqlDbType.TinyInt,hp.Min),
    P("@tin_chi_max",SqlDbType.TinyInt,hp.Max),P("@trang_thai",SqlDbType.Bit,hp.HoatDong),P("@phien_ban",SqlDbType.Binary,hp.PhienBan,32));
  }
  public Task<DataTable> HocKyAsync(string query)
  { return QueryAsync("dbo.usp_HocKy_DanhSach",P("@tu_khoa",SqlDbType.NVarChar,query,200)); }
  public Task SaveHocKyAsync(HocKy hk,bool isNew)
  {
   Validation.HocKy(hk);
   return ExecuteAsync("dbo.usp_HocKy_Luu",P("@la_moi",SqlDbType.Bit,isNew),P("@ma_hk",SqlDbType.VarChar,hk.Ma,20),P("@nam_hoc",SqlDbType.VarChar,hk.NamHoc,9),
    P("@hoc_ky",SqlDbType.TinyInt,hk.Ky),Time("@tg_bat_dau",hk.BatDau),Time("@tg_ket_thuc",hk.KetThuc),Time("@han_huy_dk",hk.HanHuy),P("@phien_ban",SqlDbType.Binary,hk.PhienBan,32));
  }
  public Task SetHocKyStatusAsync(HocKy hk,string status)
  { return ExecuteAsync("dbo.usp_HocKy_DoiTrangThai",P("@ma_hk",SqlDbType.VarChar,hk.Ma,20),P("@trang_thai",SqlDbType.VarChar,status,10),P("@phien_ban",SqlDbType.Binary,hk.PhienBan,32)); }
 }
}
