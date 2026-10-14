using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Catalogs;
namespace QuanLyGiangVien.Features.Catalogs
{
 public sealed class HocPhanControl : UserControl
 {
  private readonly Func<CatalogRepository> repository;
  private readonly DataGridView grid=Ui.Grid();
  private readonly TextBox search=new TextBox {Width=200,MaxLength=200};
  private readonly ComboBox filterType=Ui.Combo("Tất cả loại","Bắt buộc","Tự chọn");
  private readonly ComboBox filterStatus=Ui.Combo("Tất cả trạng thái","Đang sử dụng","Ngừng sử dụng");
  private readonly TextBox code=new TextBox {MaxLength=20,CharacterCasing=CharacterCasing.Upper};
  private readonly TextBox name=new TextBox {MaxLength=150};
  private readonly ComboBox type=Ui.Combo("Bắt buộc","Tự chọn");
  private readonly NumericUpDown credits=Ui.Number(3),minimum=Ui.Number(3),maximum=Ui.Number(3);
  private readonly ComboBox faculty=Ui.Combo("Chưa phân khoa");
  private readonly CheckBox active=new CheckBox {Text="Đang sử dụng",Checked=true,AutoSize=true,Margin=new Padding(0,14,0,10)};
  private readonly Label count=Ui.Label("Chưa tải dữ liệu"),mode=Ui.Label("Thêm học phần mới",10,true);
  private readonly ErrorProvider errors=new ErrorProvider();
  private HocPhan? editing;
  private bool binding;
  public HocPhanControl(Func<CatalogRepository> repository)
  {
   credits.Maximum=12;this.repository=repository; Dock=DockStyle.Fill; BackColor=Ui.Background; Padding=new Padding(12);
   var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,360));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(layout);
   var list=new Panel {Dock=DockStyle.Fill,Padding=new Padding(0,0,12,0)};layout.Controls.Add(list,0,0);
   var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(0,4,0,8)};
   toolbar.Controls.Add(Ui.Label("Tìm mã / tên:"));toolbar.Controls.Add(search);toolbar.Controls.Add(filterType);toolbar.Controls.Add(filterStatus);
   var find=Ui.Button("Tìm kiếm",true);var reset=Ui.Button("Tải lại");toolbar.Controls.Add(find);toolbar.Controls.Add(reset);
   count.Dock=DockStyle.Bottom;count.AutoSize=false;count.Height=34;
   list.Controls.Add(grid);list.Controls.Add(count);list.Controls.Add(toolbar);
   Ui.Column(grid,"ma_hp","Mã học phần",105);Ui.Column(grid,"ten_hp","Tên học phần",265);
   Ui.Column(grid,"so_tin_chi","Tín chỉ",70);Ui.Column(grid,"loai_hp","Loại",105);
   Ui.Column(grid,"tin_chi_min","TC tối thiểu",100);Ui.Column(grid,"tin_chi_max","TC tối đa",95);Ui.Column(grid,"trang_thai","Sử dụng",95);
   grid.CellFormatting+=(s,e)=> {
    string field=grid.Columns[e.ColumnIndex].DataPropertyName;
    if(field=="loai_hp" && e.Value!=null) { e.Value=e.Value.ToString()=="BAT_BUOC"?"Bắt buộc":"Tự chọn";e.FormattingApplied=true; }
    if(field=="trang_thai" && e.Value is bool) {e.Value=(bool)e.Value?"Có":"Ngừng";e.FormattingApplied=true;}
   };
   var editor=Ui.Editor("Thông tin học phần");layout.Controls.Add(editor,1,0); Ui.Add(editor,mode);
   Ui.Field(editor,"Mã học phần *",code);Ui.Field(editor,"Tên học phần *",name);Ui.Field(editor,"Loại học phần *",type);Ui.Field(editor,"Khoa phụ trách",faculty);
   Ui.Field(editor,"Số tín chỉ *",credits);Ui.Field(editor,"Tín chỉ tối thiểu *",minimum);Ui.Field(editor,"Tín chỉ tối đa *",maximum);Ui.Add(editor,active);
   var actions=new FlowLayoutPanel {AutoSize=true};var add=Ui.Button("Thêm mới");var save=Ui.Button("Lưu",true);actions.Controls.Add(add);actions.Controls.Add(save);var delete=Ui.Button("Xóa");actions.Controls.Add(delete);delete.Click+=async(s,e)=>await Ui.RunAsync(this,DeleteAsync);Ui.Add(editor,actions);
   Ui.Add(editor,Ui.Hint("Chọn một dòng để sửa. Mã học phần được giữ cố định khi sửa. Với môn có tín chỉ cố định, nhập min = số tín chỉ = max."));
   errors.ContainerControl=this;
   add.Click+=(s,e)=> {if(!DiscardConfirmed())return;NewItem();};
   save.Click+=async(s,e)=>await Ui.RunAsync(this,SaveAsync);
   find.Click+=async(s,e)=>await Ui.RunAsync(this,ReloadAsync);
   reset.Click+=async(s,e)=> {if(!DiscardConfirmed())return; await Ui.RunAsync(this,async()=> {search.Clear();filterType.SelectedIndex=0;filterStatus.SelectedIndex=0;await ReloadAsync();if(IsDisposed||Disposing)return;NewItem();});};
   search.KeyDown+=async(s,e)=> {if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Ui.RunAsync(this,ReloadAsync);}};
   grid.SelectionChanged+=(s,e)=>SelectItem(); NewItem();
  }
  private HocPhan ReadEditor()
  {
   return new HocPhan {Ma=code.Text.Trim().ToUpperInvariant(),Ten=name.Text.Trim(),Loai=type.SelectedIndex==0?"BAT_BUOC":"TU_CHON",
    MaKhoa=(faculty.SelectedItem as FacultyChoice)?.Id, TinChi=(byte)credits.Value,Min=(byte)minimum.Value,Max=(byte)maximum.Value,HoatDong=active.Checked,PhienBan=editing==null?null:editing.PhienBan};
  }
  public bool HasUnsavedChanges
  {
   get {
    var hp=ReadEditor();
    if(editing==null)return hp.Ma!=""||hp.Ten!=""||hp.TinChi!=3||hp.Min!=3||hp.Max!=3||hp.Loai!="BAT_BUOC"||!hp.HoatDong||hp.MaKhoa!=null;
    return hp.Ten!=editing.Ten||hp.TinChi!=editing.TinChi||hp.Min!=editing.Min||hp.Max!=editing.Max||hp.Loai!=editing.Loai||hp.HoatDong!=editing.HoatDong||hp.MaKhoa!=editing.MaKhoa;
   }
  }
  private bool DiscardConfirmed() {return !HasUnsavedChanges||Ui.Confirm(this,"Bạn có thay đổi chưa lưu. Bỏ các thay đổi này?");}
  public void ResetEditor() { NewItem(); }
  private void NewItem()
  {
   binding=true;grid.ClearSelection();binding=false;editing=null;code.ReadOnly=false;code.Clear();name.Clear();
   type.SelectedIndex=0;credits.Value=minimum.Value=maximum.Value=3;active.Checked=true;faculty.SelectedIndex=0;mode.Text="Thêm học phần mới";errors.Clear();code.Focus();
  }
  private void SelectItem()
  {
   if(binding||grid.SelectedRows.Count==0)return;
   var row=grid.SelectedRows[0].DataBoundItem as DataRowView;if(row==null)return;
   var next=HocPhan.FromRow(row.Row);if(editing!=null && next.Ma==editing.Ma)return;
   if(!DiscardConfirmed()) {RestoreSelection();return;}
   editing=next;code.Text=editing.Ma;code.ReadOnly=true;name.Text=editing.Ten;type.SelectedIndex=editing.Loai=="BAT_BUOC"?0:1;
   credits.Value=editing.TinChi;minimum.Value=editing.Min;maximum.Value=editing.Max;active.Checked=editing.HoatDong;SelectFaculty(editing.MaKhoa);mode.Text="Sửa học phần: "+editing.Ma;errors.Clear();
  }
  private void RestoreSelection()
  {
   binding=true;grid.ClearSelection();
   if(editing!=null)foreach(DataGridViewRow row in grid.Rows)if(Convert.ToString(row.Cells["ma_hp"].Value)==editing.Ma){row.Selected=true;break;}
   binding=false;
  }
  public async Task ReloadAsync()
  {
   if(IsDisposed||Disposing)return;
   string? t=filterType.SelectedIndex==0?null:filterType.SelectedIndex==1?"BAT_BUOC":"TU_CHON";
   bool? a=filterStatus.SelectedIndex==0?(bool?)null:filterStatus.SelectedIndex==1;
   var savedFaculty=(faculty.SelectedItem as FacultyChoice)?.Id;
   var faculties=await repository().FacultiesAsync();if(IsDisposed||Disposing)return;faculty.Items.Clear();faculty.Items.Add(new FacultyChoice(null,"Chưa phân khoa"));
   foreach(DataRow row in faculties.Rows)faculty.Items.Add(new FacultyChoice((string)row["ma_khoa"],(string)row["ten_khoa"]+((bool)row["trang_thai"]?"":" (ngừng hoạt động)")));SelectFaculty(savedFaculty);
   var dt=await repository().HocPhanAsync(search.Text.Trim(),t,a);if(IsDisposed||Disposing)return;
   binding=true;grid.DataSource=dt;grid.ClearSelection();binding=false;RestoreSelection();
   count.Text=dt.Rows.Count+" học phần  •  Chọn dòng để sửa thông tin";
  }
  private async Task SaveAsync()
  {
   errors.Clear();var hp=ReadEditor();
   if(string.IsNullOrWhiteSpace(hp.Ma))errors.SetError(code,"Chưa nhập mã.");
   if(string.IsNullOrWhiteSpace(hp.Ten))errors.SetError(name,"Chưa nhập tên.");
   Validation.HocPhan(hp);
   await repository().SaveHocPhanAsync(hp,editing==null);if(IsDisposed||Disposing)return;
   await ReloadAsync();if(IsDisposed||Disposing)return;NewItem();
   MessageBox.Show(this,"Đã lưu học phần.","Thành công",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  private sealed record FacultyChoice(string? Id,string Name) { public override string ToString()=>Name; }
  private void SelectFaculty(string? id)
  {
   for(int i=0;i<faculty.Items.Count;i++)if(faculty.Items[i] is FacultyChoice f && f.Id==id){faculty.SelectedIndex=i;return;}
   if(id!=null){faculty.Items.Add(new FacultyChoice(id,id+" (không hoạt động)"));faculty.SelectedIndex=faculty.Items.Count-1;}else faculty.SelectedIndex=0;
  }
  private async Task DeleteAsync()
  {
   if(editing==null)throw new ArgumentException("Chọn học phần cần xóa.");
   if(!DiscardConfirmed()||!Ui.Confirm(this,"Xóa học phần "+editing.Ma+"?"))return;
   await repository().DeleteHocPhanAsync(editing);if(IsDisposed||Disposing)return;await ReloadAsync();if(!IsDisposed&&!Disposing)NewItem();
  }
  protected override void Dispose(bool disposing) {if(disposing)errors.Dispose();base.Dispose(disposing);}
 }
}