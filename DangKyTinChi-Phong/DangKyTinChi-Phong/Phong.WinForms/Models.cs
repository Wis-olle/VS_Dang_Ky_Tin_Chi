using System;
using System.Data;
namespace DangKyTinChi.Phong
{
 public sealed class HocPhan
 {
  public string Ma { get; set; }
  public string Ten { get; set; }
  public byte TinChi { get; set; }
  public string Loai { get; set; }
  public byte Min { get; set; }
  public byte Max { get; set; }
  public bool HoatDong { get; set; }
  public byte[] PhienBan { get; set; }
  public static HocPhan FromRow(DataRow r)
  {
   return new HocPhan { Ma=(string)r["ma_hp"], Ten=(string)r["ten_hp"], TinChi=Convert.ToByte(r["so_tin_chi"]),
    Loai=(string)r["loai_hp"], Min=Convert.ToByte(r["tin_chi_min"]), Max=Convert.ToByte(r["tin_chi_max"]),
    HoatDong=(bool)r["trang_thai"], PhienBan=(byte[])r["phien_ban"] };
  }
 }
 public sealed class HocKy
 {
  public string Ma { get; set; }
  public string NamHoc { get; set; }
  public byte Ky { get; set; }
  public DateTime BatDau { get; set; }
  public DateTime KetThuc { get; set; }
  public DateTime HanHuy { get; set; }
  public string TrangThai { get; set; }
  public byte[] PhienBan { get; set; }
  public static HocKy FromRow(DataRow r)
  {
   return new HocKy { Ma=(string)r["ma_hk"], NamHoc=(string)r["nam_hoc"], Ky=Convert.ToByte(r["hoc_ky"]),
    BatDau=(DateTime)r["tg_bat_dau"], KetThuc=(DateTime)r["tg_ket_thuc"], HanHuy=(DateTime)r["han_huy_dk"],
    TrangThai=(string)r["trang_thai"], PhienBan=(byte[])r["phien_ban"] };
  }
 }
}
