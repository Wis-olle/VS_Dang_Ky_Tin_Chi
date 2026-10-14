using System;
using System.Windows.Forms;
using DangKyTinChi.Phong.Forms;
namespace DangKyTinChi.Phong
{
 internal static class Program
 {
  [STAThread]
  private static void Main()
  {
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   Application.Run(new MainForm());
  }
 }
}
