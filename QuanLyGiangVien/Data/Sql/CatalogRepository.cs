using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Catalogs;
namespace QuanLyGiangVien.Data.Sql;
// Adapted from PhongRepository, Phong_Function@da04d8b; Huệ schema contract.
public sealed class CatalogRepository
{
 private readonly string connectionString; private readonly Action changed;
 public CatalogRepository(string connectionString,Action? changed=null)
 {
  var settings=new SqlConnectionStringBuilder(connectionString) {PersistSecurityInfo=false};
  if(string.IsNullOrWhiteSpace(settings.DataSource)||string.IsNullOrWhiteSpace(settings.InitialCatalog))throw new ArgumentException("Cấu hình kết nối danh mục chưa đầy đủ.");
  settings.ConnectTimeout=Math.Clamp(settings.ConnectTimeout,1,15);this.connectionString=settings.ConnectionString;this.changed=changed??(()=>{});
 }
 private async Task<DataTable> QueryAsync(string proc,params SqlParameter[] ps)
 {
  try {
   using var cn=new SqlConnection(connectionString);using var cmd=new SqlCommand("dbo.usp_Phong_"+proc,cn) {CommandType=CommandType.StoredProcedure,CommandTimeout=15};
   cmd.Parameters.AddRange(ps);await cn.OpenAsync().ConfigureAwait(false);using var reader=await cmd.ExecuteReaderAsync().ConfigureAwait(false);
   var result=new DataTable();result.Load(reader);return result;
  }catch(SqlException ex){throw new PersistenceException(Error(ex.Number));}
  catch(InvalidOperationException){throw new PersistenceException("Không thể đọc danh mục. Kiểm tra kết nối và tải lại dữ liệu.");}
 }
 private async Task ExecuteAsync(string proc,params SqlParameter[] ps)
 {
  try {
   using var cn=new SqlConnection(connectionString);using var cmd=new SqlCommand("dbo.usp_Phong_"+proc,cn) {CommandType=CommandType.StoredProcedure,CommandTimeout=15};
   cmd.Parameters.AddRange(ps);await cn.OpenAsync().ConfigureAwait(false);await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
  }catch(SqlException ex){throw new PersistenceException(Error(ex.Number));}
  catch(InvalidOperationException){throw new PersistenceException("Không thể lưu danh mục. Kiểm tra kết nối và tải lại dữ liệu.");}
  changed();
 }
 private static string Error(int number)=>number switch {
  52100=>"Thông tin học phần/học kỳ không hợp lệ. Kiểm tra trường bắt buộc và khoảng ngày.",
  52101=>"Dữ liệu đã thay đổi ở phiên khác hoặc không còn tồn tại. Hãy tải lại trước khi sửa.",
  52102=>"Học phần đang có lớp mở hoặc đăng ký; không thể ngừng sử dụng hoặc thay đổi dữ liệu ảnh hưởng đăng ký.",
  52103=>"Khoảng ngày học mới không chứa hết lịch lớp hiện có.",
  52104=>"Khoa đã chọn không còn hoạt động. Hãy tải lại danh mục khoa.",
  52106=>"Mã học phần/học kỳ đã tồn tại. Hãy tải lại danh sách.",
  52105=>"Học phần/học kỳ đang được sử dụng nên không thể xóa.",
  2812=>"Database chưa có module Phong tích hợp. Cần chạy migration 06_Catalogs.sql.",
  _=>SqlErrorMessages.ForNumber(number)};
 private static SqlParameter P(string name,SqlDbType type,object? value,int size=0)=>new(name,type,size){Value=value??DBNull.Value};
 private static SqlParameter Time(string name,DateTime dt)=>new(name,SqlDbType.DateTime2){Scale=0,Value=dt};
 private static SqlParameter Token(string? token)
 {
  if(token is not null&&(token.Length!=64||!token.All(Uri.IsHexDigit)))throw new ArgumentException("Phiên bản dữ liệu không hợp lệ; hãy tải lại.");
  return P("@phien_ban",SqlDbType.Binary,token is null?null:Convert.FromHexString(token),32);
 }
 public Task<DataTable> FacultiesAsync()=>QueryAsync("Khoa_DanhSach");
 public Task<DataTable> HocPhanAsync(string query,string? type,bool? active)=>QueryAsync("HocPhan_DanhSach",P("@tu_khoa",SqlDbType.NVarChar,query,200),P("@loai_hp",SqlDbType.VarChar,type,20),P("@trang_thai",SqlDbType.Bit,active));
 public Task SaveHocPhanAsync(HocPhan hp,bool isNew)
 {
  Validation.HocPhan(hp);
  return ExecuteAsync("HocPhan_Luu",P("@la_moi",SqlDbType.Bit,isNew),P("@ma_hp",SqlDbType.VarChar,hp.Ma,20),P("@ten_hp",SqlDbType.NVarChar,hp.Ten,150),P("@so_tin_chi",SqlDbType.TinyInt,hp.TinChi),P("@loai_hp",SqlDbType.VarChar,hp.Loai,20),P("@tin_chi_min",SqlDbType.TinyInt,hp.Min),P("@tin_chi_max",SqlDbType.TinyInt,hp.Max),P("@trang_thai",SqlDbType.Bit,hp.HoatDong),P("@ma_khoa",SqlDbType.VarChar,hp.MaKhoa,20),Token(hp.PhienBan));
 }
 public Task<DataTable> HocKyAsync(string query)=>QueryAsync("HocKy_DanhSach",P("@tu_khoa",SqlDbType.NVarChar,query,200));
 public Task SaveHocKyAsync(HocKy hk,bool isNew)
 {
  Validation.HocKy(hk);
  return ExecuteAsync("HocKy_Luu",P("@la_moi",SqlDbType.Bit,isNew),P("@ma_hk",SqlDbType.VarChar,hk.Ma,20),P("@nam_hoc",SqlDbType.VarChar,hk.NamHoc,9),P("@hoc_ky",SqlDbType.TinyInt,hk.Ky),Time("@tg_bat_dau",hk.BatDau),Time("@tg_ket_thuc",hk.KetThuc),Time("@han_huy_dk",hk.HanHuy),P("@ngay_hoc_bd",SqlDbType.Date,hk.NgayHocBatDau.Date),P("@ngay_hoc_kt",SqlDbType.Date,hk.NgayHocKetThuc.Date),Token(hk.PhienBan));
 }
 public Task SetHocKyStatusAsync(HocKy hk,string status)
 {
  if(status is not ("DANG_MO" or "DONG"))throw new ArgumentException("Trạng thái học kỳ không hợp lệ.");
  return ExecuteAsync("HocKy_DoiTrangThai",P("@ma_hk",SqlDbType.VarChar,hk.Ma,20),P("@trang_thai",SqlDbType.VarChar,status,12),Token(hk.PhienBan));
 }
 public Task DeleteHocPhanAsync(HocPhan hp)=>ExecuteAsync("HocPhan_Xoa",P("@ma_hp",SqlDbType.VarChar,hp.Ma,20),Token(hp.PhienBan));
 public Task DeleteHocKyAsync(HocKy hk)=>ExecuteAsync("HocKy_Xoa",P("@ma_hk",SqlDbType.VarChar,hk.Ma,20),Token(hk.PhienBan));
}
