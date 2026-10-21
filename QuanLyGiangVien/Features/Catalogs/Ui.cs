using System;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace QuanLyGiangVien.Features.Catalogs
{
 internal static class Ui
 {
  private sealed class BusyState { public bool Busy; }
  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,BusyState> busyStates=new();
  public static bool IsBusy(Control owner)=>busyStates.TryGetValue(owner,out var state)&&state.Busy;
  public static readonly Color Navy=AppTheme.Ink, Blue=AppTheme.Primary, Background=AppTheme.Canvas;
  public static Button Button(string text,bool primary=false)
  {
   var b=new QuanLyGiangVien.Features.Registration.ShowcaseButton {Primary=primary,Text=text,AutoSize=true,MinimumSize=new Size(94,34),Height=34,FlatStyle=FlatStyle.Flat,
    BackColor=primary?Blue:Color.White,ForeColor=primary?Color.White:Navy,Cursor=Cursors.Hand,Margin=new Padding(0,4,8,4)};
   b.FlatAppearance.BorderColor=primary?Blue:Color.FromArgb(206,216,229); return b;
  }
  public static Label Label(string text,int size=10,bool bold=false)
  { return new Label {Text=text,AutoSize=true,Font=AppTheme.Font(size,bold),ForeColor=Navy,Margin=new Padding(0,10,0,4)}; }
  public static ComboBox Combo(params string[] items)
  { var c=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=150}; c.Items.AddRange(items); c.SelectedIndex=0; return c; }
  public static DataGridView Grid()
  {
   return new DataGridView {Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
    AllowUserToResizeRows=false,ReadOnly=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,
    RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,
    ColumnHeadersHeight=42,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
    ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {BackColor=AppTheme.Lilac,ForeColor=Navy,Font=AppTheme.Font(9,true)},
    DefaultCellStyle=new DataGridViewCellStyle {Font=AppTheme.Font(9),SelectionBackColor=AppTheme.AccentSoft,SelectionForeColor=Navy,Padding=new Padding(5,0,5,0)},
    AlternatingRowsDefaultCellStyle=new DataGridViewCellStyle {BackColor=Color.FromArgb(255,250,250)},
    RowTemplate={Height=34},AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None};
  }
  public static void Column(DataGridView g,string field,string title,int width=100,string? format=null)
  {
   var col=new DataGridViewTextBoxColumn {Name=field,DataPropertyName=field,HeaderText=title,Width=width,SortMode=DataGridViewColumnSortMode.Automatic};
   if(format!=null)col.DefaultCellStyle.Format=format; g.Columns.Add(col);
  }
  public static TableLayoutPanel Editor(string heading)
  {
   var p=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,AutoScroll=true,Padding=new Padding(20,8,20,18),BackColor=Color.White};
   p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Add(p,Label(heading,13,true)); return p;
  }
  public static void Add(TableLayoutPanel panel,Control c)
  {
   int row=panel.RowCount; panel.RowCount=row+1; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   c.Anchor=AnchorStyles.Left|AnchorStyles.Right; panel.Controls.Add(c,0,row);
  }
  public static void Field(TableLayoutPanel p,string caption,Control c)
  { Add(p,Label(caption)); c.Margin=new Padding(0,0,0,4); Add(p,c); }
  public static NumericUpDown Number(int value)
  { return new NumericUpDown {Minimum=1,Maximum=30,Value=value,Height=28}; }
  public static DateTimePicker Date()
  { return new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm:ss",Height=28}; }
  public static DateTimePicker DateOnly()=>new() {Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Height=28};
  public static Label Hint(string text)
  { return new Label {Text=text,AutoSize=true,MaximumSize=new Size(320,0),ForeColor=Color.FromArgb(82,102,129),Margin=new Padding(0,14,0,6)}; }
  public static async Task RunAsync(Control owner,Func<Task> operation)
  {
   if(owner.IsDisposed || !owner.Enabled || IsBusy(owner))return;
   var state=busyStates.GetOrCreateValue(owner);state.Busy=true;
   owner.Enabled=false; owner.UseWaitCursor=true;
   try { await operation(); }
   catch(Exception ex) when(ex is ArgumentException || ex is InvalidOperationException || ex is PersistenceException)
   { if(!owner.IsDisposed)MessageBox.Show(owner,ex.Message,"Thông báo",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
   finally { state.Busy=false;if(!owner.IsDisposed) {owner.Enabled=true;owner.UseWaitCursor=false;} }
  }
  public static bool Confirm(Control owner,string message)
  { return MessageBox.Show(owner,message,"Xác nhận",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes; }
 }
}
