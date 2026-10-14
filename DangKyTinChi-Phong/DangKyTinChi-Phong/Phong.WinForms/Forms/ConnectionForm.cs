using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using DangKyTinChi.Phong.Data;
namespace DangKyTinChi.Phong.Forms
{
 internal sealed class ConnectionForm : Form
 {
  private readonly TextBox server=new TextBox(),database=new TextBox();
  private readonly CheckBox encrypt=new CheckBox {Text="Mã hóa kết nối (server có chứng chỉ hợp lệ)",AutoSize=true};
  private readonly Label result=Ui.Hint("Dùng Windows Authentication; không lưu mật khẩu.");
  private bool tested;
  public ConnectionForm()
  {
   Text="Kết nối SQL Server";Font=new Font("Segoe UI",10);ClientSize=new Size(490,385);FormBorderStyle=FormBorderStyle.FixedDialog;
   MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   var p=Ui.Editor("Thiết lập database");Controls.Add(p);Ui.Field(p,"Server (ví dụ .\\SQLEXPRESS)",server);Ui.Field(p,"Database",database);Ui.Add(p,encrypt);Ui.Add(p,result);
   var actions=new FlowLayoutPanel {AutoSize=true};var test=Ui.Button("Kiểm tra",true);var save=Ui.Button("Lưu kết nối");var cancel=Ui.Button("Đóng");actions.Controls.Add(test);actions.Controls.Add(save);actions.Controls.Add(cancel);Ui.Add(p,actions);
   var b=new SqlConnectionStringBuilder(ConnectionSettings.Load());server.Text=b.DataSource;database.Text=b.InitialCatalog;encrypt.Checked=b.Encrypt;
   server.TextChanged+=(s,e)=>tested=false;database.TextChanged+=(s,e)=>tested=false;encrypt.CheckedChanged+=(s,e)=>tested=false;
   test.Click+=async(s,e)=>await Ui.RunAsync(this,async()=>{
    await new PhongRepository(ConnectionSettings.Make(server.Text,database.Text,encrypt.Checked)).TestAsync();tested=true;result.Text="Kết nối thành công; module Phong đã được cài.";
   });
   save.Click+=async(s,e)=>await Ui.RunAsync(this,async()=>{
    if(!tested)await new PhongRepository(ConnectionSettings.Make(server.Text,database.Text,encrypt.Checked)).TestAsync();
    ConnectionSettings.Save(server.Text,database.Text,encrypt.Checked);DialogResult=DialogResult.OK;Close();
   });
   cancel.Click+=(s,e)=>Close();CancelButton=cancel;
  }
 }
}
