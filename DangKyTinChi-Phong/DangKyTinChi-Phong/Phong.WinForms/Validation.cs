using System;
using System.Text.RegularExpressions;
namespace DangKyTinChi.Phong
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
   if(string.IsNullOrWhiteSpace(hp.Ten) || hp.Ten.Length>200)
    throw new ArgumentException("Tên học phần phải có 1–200 ký tự.");
   if(hp.Loai!="BAT_BUOC" && hp.Loai!="TU_CHON") throw new ArgumentException("Chọn loại học phần.");
   if(hp.Min<1 || hp.Max>30 || hp.Min>hp.TinChi || hp.TinChi>hp.Max)
    throw new ArgumentException("Phải có 1 ≤ tín chỉ tối thiểu ≤ số tín chỉ ≤ tín chỉ tối đa ≤ 30.");
  }
  public static void HocKy(HocKy hk)
  {
   Ma(hk.Ma);
   if(hk.NamHoc==null || !Regex.IsMatch(hk.NamHoc,"\\A[12][0-9]{3}-[12][0-9]{3}\\z") ||
     int.Parse(hk.NamHoc.Substring(5,4)) != int.Parse(hk.NamHoc.Substring(0,4))+1)
    throw new ArgumentException("Năm học phải là hai năm liên tiếp, ví dụ 2026-2027.");
   if(hk.Ky<1 || hk.Ky>3) throw new ArgumentException("Học kỳ là 1, 2 hoặc 3 (hè).");
   if(hk.BatDau>=hk.KetThuc) throw new ArgumentException("Thời gian bắt đầu phải trước thời gian kết thúc.");
   if(hk.HanHuy<hk.BatDau) throw new ArgumentException("Hạn hủy không được trước thời gian bắt đầu.");
  }
 }
}
