using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Catalogs;
namespace QuanLyGiangVien.Features.Catalogs
{
 public sealed class HocKyControl : UserControl
 {
  private readonly Func<CatalogRepository> repository;
  private readonly DataGridView grid=Ui.Grid();
  private readonly TextBox search=new TextBox {Width=235,MaxLength=200};
  private readonly TextBox code=new TextBox {MaxLength=20,CharacterCasing=CharacterCasing.Upper};
  private readonly TextBox year=new TextBox {MaxLength=9};
  private readonly ComboBox term=Ui.Combo("Học kỳ 1","Học kỳ 2","Học kỳ hè (3)");
  private readonly DateTimePicker start=Ui.Date(),end=Ui.Date(),cancel=Ui.Date(),studyStart=Ui.DateOnly(),studyEnd=Ui.DateOnly();
  private readonly Label mode=Ui.Label("Thêm học kỳ mới",10,true),status=Ui.Label("Đợt mới: đóng",10,true),count=Ui.Label("Chưa tải dữ liệu");
  private readonly Button open=Ui.Button("Mở đợt",true),close=Ui.Button("Đóng đợt");
  private readonly ErrorProvider errors=new ErrorProvider();
  private HocKy? editing;
  private DateTime defaultStart,defaultEnd,defaultCancel,defaultStudyStart,defaultStudyEnd;
  private bool binding;
  public HocKyControl(Func<CatalogRepository> repository)
  {
   this.repository=repository;Dock=DockStyle.Fill;BackColor=Ui.Background;Padding=new Padding(12);
   var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,360));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
   var list=new Panel {Dock=DockStyle.Fill,Padding=new Padding(0,0,12,0)};layout.Controls.Add(list,0,0);
   var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(0,4,0,8)};
   toolbar.Controls.Add(Ui.Label("Tìm mã / năm học:"));toolbar.Controls.Add(search);var find=Ui.Button("Tìm kiếm",true);var refresh=Ui.Button("Tải lại");toolbar.Controls.Add(find);toolbar.Controls.Add(refresh);
   count.Dock=DockStyle.Bottom;count.AutoSize=false;count.Height=34;list.Controls.Add(grid);list.Controls.Add(count);list.Controls.Add(toolbar);
   Ui.Column(grid,"ma_hk","Mã học kỳ",120);Ui.Column(grid,"nam_hoc","Năm học",105);Ui.Column(grid,"hoc_ky","Học kỳ",70);
   Ui.Column(grid,"tg_bat_dau","Bắt đầu đăng ký",165,"dd/MM/yyyy HH:mm:ss");Ui.Column(grid,"tg_ket_thuc","Kết thúc đăng ký",165,"dd/MM/yyyy HH:mm:ss");
   Ui.Column(grid,"han_huy_dk","Hạn hủy",165,"dd/MM/yyyy HH:mm:ss");Ui.Column(grid,"trang_thai","Mở / đóng",95);
   Ui.Column(grid,"trang_thai_hieu_luc","Hiệu lực thực tế",165);
   grid.CellFormatting+=(s,e)=> {
    if(grid.Columns[e.ColumnIndex].DataPropertyName=="trang_thai"&&e.Value!=null){e.Value=e.Value.ToString()=="DANG_MO"?"Mở":"Đóng";e.FormattingApplied=true;}
   };
   var editor=Ui.Editor("Học kỳ và đợt đăng ký");layout.Controls.Add(editor,1,0);Ui.Add(editor,mode);
   Ui.Field(editor,"Mã học kỳ *",code);Ui.Field(editor,"Năm học * (ví dụ 2026-2027)",year);Ui.Field(editor,"Học kỳ *",term);
   Ui.Field(editor,"Bắt đầu đăng ký * (giờ Việt Nam)",start);Ui.Field(editor,"Kết thúc đăng ký *",end);Ui.Field(editor,"Hạn hủy đăng ký *",cancel);Ui.Field(editor,"Ngày bắt đầu học *",studyStart);Ui.Field(editor,"Ngày kết thúc học *",studyEnd);Ui.Add(editor,status);
   var actions=new FlowLayoutPanel {AutoSize=true};var add=Ui.Button("Thêm mới");var save=Ui.Button("Lưu",true);actions.Controls.Add(add);actions.Controls.Add(save);var delete=Ui.Button("Xóa");actions.Controls.Add(delete);delete.Click+=async(s,e)=>await Ui.RunAsync(this,DeleteAsync);Ui.Add(editor,actions);
   var stateActions=new FlowLayoutPanel {AutoSize=true};stateActions.Controls.Add(open);stateActions.Controls.Add(close);Ui.Add(editor,stateActions);
   Ui.Add(editor,Ui.Hint("Lưu học kỳ trước khi mở đợt. Mở trước ngày bắt đầu chỉ lên lịch; hết thời gian sẽ tự ngừng nhận đăng ký. Đóng đợt dừng cả đăng ký mới và hủy đăng ký theo bộ SQL Huệ. Hủy chỉ được khi đợt đang mở và chưa quá hạn hủy."));
   errors.ContainerControl=this;
   add.Click+=(s,e)=>{if(DiscardConfirmed())NewItem();};
   save.Click+=async(s,e)=>await Ui.RunAsync(this,SaveAsync);
   find.Click+=async(s,e)=>await Ui.RunAsync(this,ReloadAsync);
   refresh.Click+=async(s,e)=>{if(!DiscardConfirmed())return;await Ui.RunAsync(this,async()=>{search.Clear();await ReloadAsync();if(IsDisposed||Disposing)return;NewItem();});};
   open.Click+=async(s,e)=>await Ui.RunAsync(this,()=>ChangeStatusAsync("DANG_MO"));
   close.Click+=async(s,e)=>await Ui.RunAsync(this,()=>ChangeStatusAsync("DONG"));
   search.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Ui.RunAsync(this,ReloadAsync);}};
   grid.SelectionChanged+=(s,e)=>SelectItem();NewItem();
  }
  private HocKy ReadEditor()
  {
   return new HocKy {Ma=code.Text.Trim().ToUpperInvariant(),NamHoc=year.Text.Trim(),Ky=(byte)(term.SelectedIndex+1),
    NgayHocBatDau=studyStart.Value.Date,NgayHocKetThuc=studyEnd.Value.Date, BatDau=start.Value,KetThuc=end.Value,HanHuy=cancel.Value,TrangThai=editing==null?"DONG":editing.TrangThai,PhienBan=editing==null?null:editing.PhienBan};
  }
  private static DateTime Second(DateTime value) {return new DateTime(value.Year,value.Month,value.Day,value.Hour,value.Minute,value.Second);}
  public bool HasUnsavedChanges
  {
   get {
    var hk=ReadEditor();
    if(editing==null)return hk.Ma!=""||hk.NamHoc!=DateTime.Today.Year+"-"+(DateTime.Today.Year+1)||hk.Ky!=1||hk.BatDau!=defaultStart||hk.KetThuc!=defaultEnd||hk.HanHuy!=defaultCancel||hk.NgayHocBatDau!=defaultStudyStart||hk.NgayHocKetThuc!=defaultStudyEnd;
    return hk.NamHoc!=editing.NamHoc||hk.Ky!=editing.Ky||hk.BatDau!=editing.BatDau||hk.KetThuc!=editing.KetThuc||hk.HanHuy!=editing.HanHuy||hk.NgayHocBatDau!=editing.NgayHocBatDau||hk.NgayHocKetThuc!=editing.NgayHocKetThuc;
   }
  }
  private bool DiscardConfirmed(){return !HasUnsavedChanges||Ui.Confirm(this,"Bạn có thay đổi chưa lưu. Bỏ các thay đổi này?");}
  public void ResetEditor() { NewItem(); }
  private void NewItem()
  {
   binding=true;grid.ClearSelection();binding=false;editing=null;code.ReadOnly=false;code.Clear();
   year.Text=DateTime.Today.Year+"-"+(DateTime.Today.Year+1);term.SelectedIndex=0;
   // Chỉ là gợi ý nhập liệu. Quyết định đang mở/hết hạn dùng đồng hồ SQL Server.
   defaultStart=Second(DateTime.Now);defaultEnd=defaultStart.AddDays(14);defaultCancel=defaultStart.AddDays(21);
   defaultStudyStart=DateTime.Today;defaultStudyEnd=defaultStudyStart.AddDays(112);studyStart.Value=defaultStudyStart;studyEnd.Value=defaultStudyEnd;
   start.Value=defaultStart;end.Value=defaultEnd;cancel.Value=defaultCancel;
   status.Text="Đợt mới: đóng";mode.Text="Thêm học kỳ mới";open.Enabled=close.Enabled=false;errors.Clear();code.Focus();
  }
  private void Fill(HocKy item)
  {
   editing=item;code.Text=item.Ma;code.ReadOnly=true;year.Text=item.NamHoc;term.SelectedIndex=item.Ky-1;
   studyStart.Value=item.NgayHocBatDau;studyEnd.Value=item.NgayHocKetThuc;start.Value=item.BatDau;end.Value=item.KetThuc;cancel.Value=item.HanHuy;
   status.Text=item.TrangThai=="DANG_MO"?"Trạng thái đã lưu: mở":"Trạng thái đã lưu: đóng";
   status.ForeColor=item.TrangThai=="DANG_MO"?Color.FromArgb(0,120,70):Ui.Navy;
   open.Enabled=item.TrangThai!="DANG_MO";close.Enabled=item.TrangThai!="DONG";mode.Text="Sửa học kỳ: "+item.Ma;errors.Clear();
  }
  private void SelectItem()
  {
   if(binding||grid.SelectedRows.Count==0)return;
   var row=grid.SelectedRows[0].DataBoundItem as DataRowView;if(row==null)return;
   var next=HocKy.FromRow(row.Row);if(editing!=null&&next.Ma==editing.Ma)return;
   if(!DiscardConfirmed()){RestoreSelection();return;}Fill(next);
  }
  private void RestoreSelection()
  {
   binding=true;grid.ClearSelection();
   if(editing!=null)foreach(DataGridViewRow row in grid.Rows)if(Convert.ToString(row.Cells["ma_hk"].Value)==editing.Ma){row.Selected=true;break;}
   binding=false;
  }
  public async Task ReloadAsync()
  {
   if(IsDisposed||Disposing)return;
   var dt=await repository().HocKyAsync(search.Text.Trim());if(IsDisposed||Disposing)return;binding=true;grid.DataSource=dt;grid.ClearSelection();binding=false;RestoreSelection();
   count.Text=dt.Rows.Count+" học kỳ  •  Ngày giờ theo múi giờ Việt Nam (UTC+7)";
  }
  private async Task SaveAsync()
  {
   errors.Clear();var hk=ReadEditor();if(string.IsNullOrWhiteSpace(hk.Ma))errors.SetError(code,"Chưa nhập mã.");Validation.HocKy(hk);
   await repository().SaveHocKyAsync(hk,editing==null);if(IsDisposed||Disposing)return;search.Clear();await ReloadAsync();
   if(IsDisposed||Disposing)return;
   // Đọc token mới trước khi cho mở/đóng đợt vừa lưu.
   var dt=grid.DataSource as DataTable??throw new InvalidOperationException("Chưa tải được học kỳ.");foreach(DataRow row in dt.Rows)if((string)row["ma_hk"]==hk.Ma){Fill(HocKy.FromRow(row));break;}
   RestoreSelection();MessageBox.Show(this,"Đã lưu học kỳ. Bạn có thể mở hoặc đóng đợt đăng ký.","Thành công",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  private async Task ChangeStatusAsync(string newStatus)
  {
   if(editing==null)throw new ArgumentException("Chọn hoặc lưu học kỳ trước.");
   if(HasUnsavedChanges)throw new ArgumentException("Có thay đổi chưa lưu. Hãy lưu thông tin trước khi mở/đóng đợt.");
   if(!Ui.Confirm(this,(newStatus=="DANG_MO"?"Mở":"Đóng")+" đợt đăng ký của "+editing.Ma+"?"))return;
   var old=editing;await repository().SetHocKyStatusAsync(old,newStatus);if(IsDisposed||Disposing)return;await ReloadAsync();if(IsDisposed||Disposing)return;
   var dt=grid.DataSource as DataTable??throw new InvalidOperationException("Chưa tải được học kỳ.");foreach(DataRow row in dt.Rows)if((string)row["ma_hk"]==old.Ma){Fill(HocKy.FromRow(row));break;}
   RestoreSelection();
  }
  private async Task DeleteAsync()
  {
   if(editing==null)throw new ArgumentException("Chọn học kỳ cần xóa.");
   if(!DiscardConfirmed()||!Ui.Confirm(this,"Xóa học kỳ "+editing.Ma+"?"))return;
   await repository().DeleteHocKyAsync(editing);if(IsDisposed||Disposing)return;await ReloadAsync();if(!IsDisposed&&!Disposing)NewItem();
  }
  protected override void Dispose(bool disposing){if(disposing)errors.Dispose();base.Dispose(disposing);}
 }
}