using System;
using System.Text.RegularExpressions;
namespace QuanLyGiangVien.Domain.Catalogs
{
 public static class Validation
 {
  public static void Ma(string ma)
  {
   if (string.IsNullOrWhiteSpace(ma) || !Regex.IsMatch(ma,"\\A[A-Za-z0-9_-]{1,20}\\z"))
    throw new ArgumentException("Mã phải có 1–20 ký tự: chữ, số, dấu gạch ngang hoặc gạch dưới.");
  }
  public static void HocPhan(HocPhan hp)
  {
   Ma(hp.Ma);
   if(hp.MaKhoa!=null)Ma(hp.MaKhoa);
   if(string.IsNullOrWhiteSpace(hp.Ten) || hp.Ten.Length>150)
    throw new ArgumentException("Tên học phần phải có 1–150 ký tự.");
   if(hp.Loai!="BAT_BUOC" && hp.Loai!="TU_CHON") throw new ArgumentException("Chọn loại học phần.");
   if(hp.TinChi>12 || hp.Min<1 || hp.Max>30 || hp.Min>hp.TinChi || hp.TinChi>hp.Max)
    throw new ArgumentException("Phải có 1 ≤ tín chỉ tối thiểu ≤ số tín chỉ ≤ tín chỉ tối đa ≤ 30.");
  }
  public static void HocKy(HocKy hk)
  {
   Ma(hk.Ma);
   if(hk.NamHoc==null || !Regex.IsMatch(hk.NamHoc,"\\A[12][0-9]{3}-[12][0-9]{3}\\z") ||
     int.Parse(hk.NamHoc.Substring(5,4)) != int.Parse(hk.NamHoc.Substring(0,4))+1)
    throw new ArgumentException("Năm học phải là hai năm liên tiếp, ví dụ 2026-2027.");
   if(hk.Ky<1 || hk.Ky>3) throw new ArgumentException("Học kỳ là 1, 2 hoặc 3 (hè).");
   if(hk.NgayHocBatDau<new DateTime(2000,1,1) || hk.NgayHocBatDau.Date>hk.NgayHocKetThuc.Date || (hk.NgayHocKetThuc.Date-hk.NgayHocBatDau.Date).TotalDays>366)
    throw new ArgumentException("Ngày học phải từ năm 2000, bắt đầu không sau kết thúc và không quá 366 ngày.");
   if(hk.BatDau>=hk.KetThuc) throw new ArgumentException("Thời gian bắt đầu phải trước thời gian kết thúc.");
   if(hk.HanHuy<hk.BatDau) throw new ArgumentException("Hạn hủy không được trước thời gian bắt đầu.");
  }
 }
}