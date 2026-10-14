using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DangKyTinChi.Phong.Data;
namespace DangKyTinChi.Phong.Forms
{
 public sealed class MainForm : Form
 {
  private PhongRepository repository;
  private readonly HocPhanControl courses;
  private readonly HocKyControl semesters;
  private readonly Label connection=Ui.Label("Chưa kết nối");
  public MainForm()
  {
   Text="Đăng ký tín chỉ • Module Phong";Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
   MinimumSize=new Size(1120,750);ClientSize=new Size(1360,820);BackColor=Ui.Background;AutoScaleMode=AutoScaleMode.Font;
   repository=new PhongRepository(ConnectionSettings.Load());
   var header=new Panel {Dock=DockStyle.Top,Height=95,BackColor=Ui.Navy};
   var title=Ui.Label("QUẢN LÝ ĐĂNG KÝ TÍN CHỈ",19,true);title.ForeColor=Color.White;title.Location=new Point(22,14);header.Controls.Add(title);
   var sub=Ui.Label("Phòng đào tạo  /  Học phần và học kỳ",10);sub.ForeColor=Color.FromArgb(185,206,231);sub.Location=new Point(24,53);header.Controls.Add(sub);
   var settings=Ui.Button("Kết nối SQL Server");settings.Location=new Point(1100,26);settings.Anchor=AnchorStyles.Top|AnchorStyles.Right;header.Controls.Add(settings);
   var tabs=new TabControl {Dock=DockStyle.Fill,Padding=new Point(22,12)};
   var hp=new TabPage("Học phần");var hk=new TabPage("Học kỳ • Đợt đăng ký");tabs.TabPages.Add(hp);tabs.TabPages.Add(hk);
   courses=new HocPhanControl(()=>repository);semesters=new HocKyControl(()=>repository);hp.Controls.Add(courses);hk.Controls.Add(semesters);
   var footer=new Panel {Dock=DockStyle.Bottom,Height=38,BackColor=Color.White};connection.Location=new Point(20,1);footer.Controls.Add(connection);
   var author=Ui.Label("Thực hiện: Phong",9);author.Location=new Point(1180,2);author.Anchor=AnchorStyles.Right|AnchorStyles.Top;footer.Controls.Add(author);
   Controls.Add(tabs);Controls.Add(footer);Controls.Add(header);
   settings.Click+=async(s,e)=>{
    if((courses.HasUnsavedChanges||semesters.HasUnsavedChanges)&&!Ui.Confirm(this,"Đổi kết nối sẽ tải lại màn hình. Bỏ các thay đổi chưa lưu?"))return;
    using(var form=new ConnectionForm())if(form.ShowDialog(this)==DialogResult.OK)
    {
     await Ui.RunAsync(this,async()=>{
      repository=new PhongRepository(ConnectionSettings.Load());
      courses.ResetEditor();semesters.ResetEditor();await RefreshAllAsync();
     });
    }
   };
   Shown+=async(s,e)=>await Ui.RunAsync(this,RefreshAllAsync);
   FormClosing+=(s,e)=>{if((courses.HasUnsavedChanges||semesters.HasUnsavedChanges)&&!Ui.Confirm(this,"Có dữ liệu chưa lưu. Bạn vẫn muốn thoát?"))e.Cancel=true;};
  }
  private async Task RefreshAllAsync()
  {
   await repository.TestAsync();await courses.ReloadAsync();await semesters.ReloadAsync();
   connection.Text="Đã kết nối  •  Múi giờ dữ liệu: Việt Nam (UTC+7)";connection.ForeColor=Color.FromArgb(0,120,70);
  }
 }
}
