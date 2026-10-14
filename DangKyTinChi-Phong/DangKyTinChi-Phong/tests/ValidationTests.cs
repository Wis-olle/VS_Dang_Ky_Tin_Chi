using System;
using DangKyTinChi.Phong;
internal static class ValidationTests
{
 private static int passed;
 private static void Test(string name,Action action,bool invalid=false)
 {
  bool rejected=false;
  try {action();}catch(ArgumentException){rejected=true;}
  if(rejected!=invalid)throw new Exception("FAIL: "+name);
  passed++;Console.WriteLine("PASS: "+name);
 }
 private static HocPhan Hp(){return new HocPhan{Ma="SEC202",Ten="Kiểm thử web",TinChi=3,Min=3,Max=3,Loai="TU_CHON",HoatDong=true};}
 private static HocKy Hk(){return new HocKy{Ma="HK1_2026",NamHoc="2026-2027",Ky=1,BatDau=new DateTime(2026,10,14,8,0,0),KetThuc=new DateTime(2026,10,28,23,59,59),HanHuy=new DateTime(2026,11,4,23,59,59)};}
 public static int Main()
 {
  Test("Mã hợp lệ",()=>Validation.Ma("HP_01-A"));
  Test("Mã dài 20",()=>Validation.Ma(new string('A',20)));
  Test("Mã rỗng",()=>Validation.Ma(""),true);
  Test("Mã có khoảng trắng",()=>Validation.Ma("HP 01"),true);
  Test("Mã có ký tự SQL",()=>Validation.Ma("HP';DROP"),true);
  Test("Mã quá dài",()=>Validation.Ma(new string('A',21)),true);
  Test("Mã chứa xuống dòng",()=>Validation.Ma("HP\n"),true);
  Test("Học phần tín chỉ cố định",()=>Validation.HocPhan(Hp()));
  Test("Học phần tín chỉ trong khoảng",()=>{var h=Hp();h.Min=1;h.Max=5;Validation.HocPhan(h);});
  Test("Thiếu tên",()=>{var h=Hp();h.Ten=" ";Validation.HocPhan(h);},true);
  Test("Tên dài hơn 200",()=>{var h=Hp();h.Ten=new string('a',201);Validation.HocPhan(h);},true);
  Test("Loại không tồn tại",()=>{var h=Hp();h.Loai="OTHER";Validation.HocPhan(h);},true);
  Test("Min lớn hơn số tín chỉ",()=>{var h=Hp();h.Min=4;Validation.HocPhan(h);},true);
  Test("Max nhỏ hơn số tín chỉ",()=>{var h=Hp();h.Max=2;Validation.HocPhan(h);},true);
  Test("Tín chỉ 0",()=>{var h=Hp();h.TinChi=0;h.Min=0;Validation.HocPhan(h);},true);
  Test("Max vượt 30",()=>{var h=Hp();h.Max=31;Validation.HocPhan(h);},true);
  Test("Học kỳ bình thường",()=>Validation.HocKy(Hk()));
  Test("Học kỳ hè",()=>{var h=Hk();h.Ky=3;Validation.HocKy(h);});
  Test("Năm học không liên tiếp",()=>{var h=Hk();h.NamHoc="2026-2028";Validation.HocKy(h);},true);
  Test("Năm học sai định dạng",()=>{var h=Hk();h.NamHoc="26-27";Validation.HocKy(h);},true);
  Test("Năm học có xuống dòng",()=>{var h=Hk();h.NamHoc="2026-2027\n";Validation.HocKy(h);},true);
  Test("Năm học null",()=>{var h=Hk();h.NamHoc=null;Validation.HocKy(h);},true);
  Test("Học kỳ 4",()=>{var h=Hk();h.Ky=4;Validation.HocKy(h);},true);
  Test("Bắt đầu bằng kết thúc",()=>{var h=Hk();h.KetThuc=h.BatDau;Validation.HocKy(h);},true);
  Test("Kết thúc trước bắt đầu",()=>{var h=Hk();h.KetThuc=h.BatDau.AddSeconds(-1);Validation.HocKy(h);},true);
  Test("Hạn hủy trước bắt đầu",()=>{var h=Hk();h.HanHuy=h.BatDau.AddSeconds(-1);Validation.HocKy(h);},true);
  Test("Hạn hủy bằng bắt đầu",()=>{var h=Hk();h.HanHuy=h.BatDau;Validation.HocKy(h);});
  Test("Hạn hủy sau kết thúc",()=>{var h=Hk();h.HanHuy=h.KetThuc.AddDays(5);Validation.HocKy(h);});
  Console.WriteLine(passed+"/"+passed+" tests passed.");return 0;
 }
}
