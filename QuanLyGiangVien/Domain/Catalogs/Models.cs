using System;
using System.Data;
namespace QuanLyGiangVien.Domain.Catalogs
{
 public sealed record HocPhan
 {
  public string Ma { get; init; } = "";
  public string Ten { get; init; } = "";
  public byte TinChi { get; init; }
  public string Loai { get; init; } = "BAT_BUOC";
  public byte Min { get; init; }
  public byte Max { get; init; }
  public string? MaKhoa { get; init; }
  public bool HoatDong { get; init; }
  public string? PhienBan { get; init; }
  public static HocPhan FromRow(DataRow r)
  {
   return new HocPhan { Ma=(string)r["ma_hp"], Ten=(string)r["ten_hp"], TinChi=Convert.ToByte(r["so_tin_chi"]),
    Loai=(string)r["loai_hp"], Min=Convert.ToByte(r["tin_chi_min"] is DBNull?r["so_tin_chi"]:r["tin_chi_min"]), Max=Convert.ToByte(r["tin_chi_max"] is DBNull?r["so_tin_chi"]:r["tin_chi_max"]),
    HoatDong=(bool)r["trang_thai"], MaKhoa=r["ma_khoa"] is DBNull?null:(string)r["ma_khoa"], PhienBan=Convert.ToHexString((byte[])r["phien_ban"]) };
  }
 }
 public sealed record HocKy
 {
  public string Ma { get; init; } = "";
  public string NamHoc { get; init; } = "";
  public byte Ky { get; init; }
  public DateTime BatDau { get; init; }
  public DateTime KetThuc { get; init; }
  public DateTime NgayHocBatDau { get; init; }
  public DateTime NgayHocKetThuc { get; init; }
  public DateTime HanHuy { get; init; }
  public string TrangThai { get; init; } = "DONG";
  public string? PhienBan { get; init; }
  public static HocKy FromRow(DataRow r)
  {
   return new HocKy { Ma=(string)r["ma_hk"], NamHoc=(string)r["nam_hoc"], Ky=Convert.ToByte(r["hoc_ky"]),
    BatDau=(DateTime)r["tg_bat_dau"], KetThuc=(DateTime)r["tg_ket_thuc"], HanHuy=(DateTime)r["han_huy_dk"],
    NgayHocBatDau=(DateTime)r["ngay_hoc_bd"], NgayHocKetThuc=(DateTime)r["ngay_hoc_kt"],
    TrangThai=(string)r["trang_thai"], PhienBan=Convert.ToHexString((byte[])r["phien_ban"]) };
  }
 }
}