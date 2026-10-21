using System.Collections.Immutable;
using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Ui;
using QuanLyGiangVien.Features.Registration.Visuals;
namespace QuanLyGiangVien.Features.Registration;

internal sealed class SchedulePlannerPage : ModulePage
{
 private readonly SchedulePlannerRepository repository;
 private readonly TabControl tabs=new(){Dock=DockStyle.Fill},views=new(){Dock=DockStyle.Fill};
 private readonly PlannerWeekPreview preview=new();
 private readonly TextBox courses=PhaseThreeUi.Text("txtPlannerCourses",300),fixedClasses=PhaseThreeUi.Text("txtPlannerFixed",600);
 private readonly DataGridView offered=Ui.Grid(),busy=Ui.Grid(),options=Ui.Grid(),meetings=Ui.Grid();
 private readonly CheckBox fewerDays=new(){Text="Ít ngày đến trường",Checked=true,AutoSize=true},fewerGaps=new(){Text="Ít tiết trống",Checked=true,AutoSize=true};
 private readonly ComboBox time=PhaseThreeUi.Choices(new("Neutral","Không ưu tiên giờ"),new("Early","Ưu tiên học sớm"),new("Late","Ưu tiên học muộn"));
 private readonly NumericUpDown count=new(){Minimum=1,Maximum=10,Value=5,Width=AppTheme.S(55)},start=new(){Minimum=1,Maximum=20,Value=1,Width=AppTheme.S(55)},end=new(){Minimum=1,Maximum=20,Value=3,Width=AppTheme.S(55)};
 private readonly DateTimePicker from=Picker(),until=Picker();
 private readonly ComboBox day=PhaseThreeUi.Choices(new("Monday","Thứ hai"),new("Tuesday","Thứ ba"),new("Wednesday","Thứ tư"),new("Thursday","Thứ năm"),new("Friday","Thứ sáu"),new("Saturday","Thứ bảy"),new("Sunday","Chủ nhật"));
 private readonly Button generate=Ui.Button("Sinh phương án",true),cancel=Ui.Button("Dừng tìm kiếm"),put=Ui.Button("Đưa vào giỏ nháp",true);
 private readonly TextBox explanation=new(){Name="txtPlannerExplanation",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Bottom,Height=AppTheme.S(78),BorderStyle=BorderStyle.None,BackColor=AppTheme.Lilac};
 private readonly Label notice=new(){Name="lblPlannerNotice",Dock=DockStyle.Top,Height=AppTheme.S(50),BackColor=AppTheme.Lilac,Padding=new(AppTheme.S(8)),Text="Gợi ý theo lịch thực tế và điều kiện học vụ. Phương án không giữ chỗ; đưa vào giỏ rồi kiểm tra/gửi đăng ký riêng."};
 private readonly FlowLayoutPanel inputBar,busyBar,resultActions;
 private ImmutableArray<SchedulePlannerBusy> busyTimes=[];
 private SchedulePlannerSnapshot? snapshot;private SchedulePlannerResult? result;private CancellationTokenSource? cancellation;
 private readonly List<DataTable> tables=[];private int generation,selectedOption=-1;private bool dirty,loading;
 public override bool HasUnsavedChanges=>dirty||cancellation!=null;
 public SchedulePlannerPage(RegistrationWorkspace host):base(host)
 {
  Name="SchedulePlannerPage";repository=new(host.ConnectionString);courses.Width=fixedClasses.Width=AppTheme.S(320);
  offered.Name="dgvPlannerOfferings";busy.Name="dgvPlannerBusy";options.Name="dgvPlannerOptions";meetings.Name="dgvPlannerMeetings";
  generate.Name="btnPlannerGenerate";cancel.Name="btnPlannerCancel";put.Name="btnPlannerToCart";cancel.Enabled=put.Enabled=false;
  var inputs=Tab("Môn & ưu tiên");var form=inputBar=Bar(150);PhaseThreeUi.Field(form,"Mã HP, cách nhau dấu phẩy",courses);PhaseThreeUi.Field(form,"Giữ lớp (mã lớp, nếu có)",fixedClasses);
  form.Controls.AddRange([fewerDays,fewerGaps]);PhaseThreeUi.Field(form,"Giờ học",time);PhaseThreeUi.Field(form,"Số phương án",count);form.Controls.AddRange([generate,cancel]);inputs.Controls.Add(offered);inputs.Controls.Add(form);
  offered.CellDoubleClick+=(_,e)=>{if(e.RowIndex<0||cancellation!=null||Ui.Current(offered)is not{}row)return;try{var id=Convert.ToString(row["Mã HP"])!;courses.Text=string.Join(",",Codes(courses.Text).Append(id).Distinct(StringComparer.OrdinalIgnoreCase));}catch(ArgumentException error){notice.Text=error.Message;}};
  var busyTab=Tab("Khung giờ bận");var editor=busyBar=Bar(145);PhaseThreeUi.Field(editor,"Từ ngày",from);PhaseThreeUi.Field(editor,"Đến ngày",until);PhaseThreeUi.Field(editor,"Thứ",day);PhaseThreeUi.Field(editor,"Từ tiết",start);PhaseThreeUi.Field(editor,"Đến tiết",end);
  var addBusy=Ui.Button("Thêm giờ bận");var deleteBusy=Ui.Button("Bỏ dòng giờ bận");editor.Controls.AddRange([addBusy,deleteBusy]);busyTab.Controls.Add(busy);busyTab.Controls.Add(editor);
  addBusy.Click+=(_,_)=>{try{var item=new SchedulePlannerBusy(DateOnly.FromDateTime(from.Value),DateOnly.FromDateTime(until.Value),Enum.Parse<DayOfWeek>(PhaseThreeUi.Choice(day)),(int)start.Value,(int)end.Value);if(busyTimes.Length>=30)throw new ArgumentException("Tối đa 30 khung giờ bận.");if(item.Until<item.From||item.EndPeriod<item.StartPeriod)throw new ArgumentException("Khoảng ngày hoặc tiết chưa hợp lệ.");busyTimes=busyTimes.Add(item);InvalidateSuggestions();BindBusy();}catch(ArgumentException e){notice.Text=e.Message;}};
  deleteBusy.Click+=(_,_)=>{if(Ui.Current(busy)is not{}row)return;busyTimes=busyTimes.RemoveAt((int)row["Dòng"]-1);InvalidateSuggestions();BindBusy();};
  var results=Tab("Phương án");var calendarTab=new TabPage("Xem lịch tuần");calendarTab.Controls.Add(preview);var listTab=new TabPage("Danh sách phương án");listTab.Controls.Add(options);var dateTab=new TabPage("Ngày học của phương án");dateTab.Controls.Add(meetings);var explainTab=new TabPage("Giải thích phương án");explanation.Dock=DockStyle.Fill;explainTab.Controls.Add(explanation);views.TabPages.AddRange([calendarTab,listTab,dateTab,explainTab]);
  preview.OptionSelected+=index=>{selectedOption=index;if(index>=0&&index<options.Rows.Count){options.CurrentCell=options.Rows[index].Cells[0];options.Rows[index].Selected=true;}ShowSelected();};
  var actions=resultActions=Bar(44);actions.Padding=new(AppTheme.S(3));actions.WrapContents=false;actions.Controls.Add(put);
  foreach(var (title,index) in new[]{("Lịch tuần",0),("So sánh",1),("Ngày học",2),("Giải thích",3)}){var view=Ui.Button(title);view.Width=AppTheme.S(100);view.Click+=(_,_)=>views.SelectedIndex=index;actions.Controls.Add(view);}
  results.Controls.Add(ShowcaseTabs.HideHeader(views));results.Controls.Add(actions);
  options.SelectionChanged+=(_,_)=>ShowSelected();generate.Click+=async(_,_)=>await GenerateAsync();cancel.Click+=(_,_)=>cancellation?.Cancel();
  put.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{
   if(result==null||selectedOption<0||selectedOption>=result.Options.Length)return;if(Host.PendingStore().Load()!=null)throw new InvalidOperationException("Đối soát yêu cầu đăng ký đang chờ trong Giỏ nháp trước khi sửa giỏ.");
   var selected=result.Options[selectedOption];using var saved=await repository.AddToCartAsync(Host.MaHocKy,selected,result.CartVersion);dirty=false;result=null;put.Enabled=false;Host.NotifyDataChanged();await Host.OpenCartAsync();
  });
  foreach(var text in new[]{courses,fixedClasses})text.TextChanged+=(_,_)=>{if(!loading)InvalidateSuggestions();};
  fewerDays.CheckedChanged+=(_,_)=>{if(!loading)InvalidateSuggestions();};fewerGaps.CheckedChanged+=(_,_)=>{if(!loading)InvalidateSuggestions();};time.SelectedIndexChanged+=(_,_)=>{if(!loading)InvalidateSuggestions();};count.ValueChanged+=(_,_)=>{if(!loading)InvalidateSuggestions();};
  Controls.Add(tabs);Controls.Add(notice);
  SizeChanged+=(_,_)=>ApplyLayout();foreach(var grid in new[]{offered,busy,options,meetings})grid.VisibleChanged+=(_,_)=>FormatGrid(grid);ApplyLayout();
 }
 private static DateTimePicker Picker()=>new(){Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Width=AppTheme.S(120),Value=DateTime.UtcNow.AddHours(7).Date};
 private TabPage Tab(string text){var tab=new TabPage(text);tabs.TabPages.Add(tab);return tab;}
 private static FlowLayoutPanel Bar(int height){var bar=Ui.Toolbar();bar.Dock=DockStyle.Bottom;bar.AutoSize=false;bar.AutoScroll=true;bar.Height=AppTheme.S(height);return bar;}
 private static ImmutableArray<string> Codes(string text)=>text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(RegistrationValidation.RequiredCode).Select(v=>v.ToUpperInvariant()).Distinct().ToImmutableArray();
 private async Task GenerateAsync()
 {
  if(cancellation!=null||snapshot==null)return;var term=Host.MaHocKy;var currentGeneration=++generation;
  SchedulePlannerRequest request;
  try{request=new(Codes(courses.Text),Codes(fixedClasses.Text),busyTimes,new(fewerDays.Checked,fewerGaps.Checked,Enum.Parse<ScheduleTimePreference>(PhaseThreeUi.Choice(time))),MaxMilliseconds:5000,MaxOptions:(int)count.Value);SchedulePlanner.Validate(request);}
  catch(ArgumentException error){notice.Text=error.Message;return;}
  result=null;ClearGrid(options);ClearGrid(meetings);preview.ClearPreview();
  using var source=new CancellationTokenSource();cancellation=source;generate.Enabled=put.Enabled=false;cancel.Enabled=true;notice.Text="Đang sinh và kiểm học vụ cho từng kế hoạch đầy đủ…";
  try{
   var generated=await repository.GenerateAsync(term,request,source.Token);if(IsDisposed||currentGeneration!=generation||term!=Host.MaHocKy)return;result=generated;
   var table=new DataTable();table.Columns.Add("Phương án",typeof(int));table.Columns.Add("Lớp mới");table.Columns.Add("Tín chỉ",typeof(int));table.Columns.Add("Ngày / tuần",typeof(int));table.Columns.Add("Tiết trống",typeof(int));
   for(var i=0;i<generated.Options.Length;i++){var option=generated.Options[i];table.Rows.Add(i+1,string.Join(", ",option.AddClassIds),option.TotalCredits,option.DaysPerWeek,option.GapPeriods);}Bind(options,table);preview.SetOptions(generated,snapshot);
   selectedOption=generated.Options.IsEmpty?-1:0;if(options.Rows.Count>0)options.CurrentCell=options.Rows[0].Cells[0];
   explanation.Text=string.Join(Environment.NewLine,generated.Explanations);notice.Text=$"{generated.Options.Length} phương án phù hợp · Xem lịch rồi đưa vào giỏ để đăng ký"+(generated.Truncated?" · Tìm kiếm đã đạt giới hạn":"");tabs.SelectedIndex=2;views.SelectedIndex=0;ShowSelected();
  }
  catch(OperationCanceledException){if(!IsDisposed&&currentGeneration==generation)notice.Text="Đã dừng tìm kiếm. Không đăng ký hoặc giữ chỗ nào.";}
  catch(Exception error){if(!IsDisposed&&currentGeneration==generation)notice.Text=error is ArgumentException or InvalidOperationException or QuanLyGiangVien.Domain.PersistenceException?error.Message:"Không sinh được phương án. Hãy tải lại dữ liệu và thử lại.";}
  finally{if(ReferenceEquals(cancellation,source))cancellation=null;if(!IsDisposed){cancel.Enabled=false;generate.Enabled=snapshot!=null;put.Enabled=result!=null&&selectedOption>=0&&selectedOption<result.Options.Length&&!result.Options[selectedOption].AddClassIds.IsEmpty;}}
 }
 private void ShowSelected()
 {
  if(Ui.Current(options)is{}row)selectedOption=(int)row["Phương án"]-1;
  put.Enabled=cancellation==null&&result!=null&&selectedOption>=0&&selectedOption<result.Options.Length;
  if(result==null||selectedOption<0||selectedOption>=result.Options.Length)return;var option=result.Options[selectedOption];put.Enabled&=!option.AddClassIds.IsEmpty;
  preview.SelectOption(selectedOption);
  var table=new DataTable();table.Columns.Add("Lớp học phần");table.Columns.Add("Ngày",typeof(DateTime));table.Columns.Add("Từ tiết",typeof(int));table.Columns.Add("Đến tiết",typeof(int));
  table.Columns.Add("Tên học phần");table.Columns.Add("Trong phương án");foreach(var meeting in option.Meetings)table.Rows.Add(meeting.ClassId,meeting.Date.ToDateTime(TimeOnly.MinValue),meeting.StartPeriod,meeting.EndPeriod,snapshot?.Classes.FirstOrDefault(c=>c.Id==meeting.ClassId)?.CourseName??meeting.ClassId,option.AddClassIds.Contains(meeting.ClassId)?"Lớp mới":"Giữ nguyên");Bind(meetings,table);
  explanation.Text=string.Join(Environment.NewLine,option.Warnings.Concat(result.Explanations));
 }
 private void BindBusy()
 {
  var table=new DataTable();table.Columns.Add("Dòng",typeof(int));table.Columns.Add("Từ ngày",typeof(DateTime));table.Columns.Add("Đến ngày",typeof(DateTime));table.Columns.Add("Thứ");table.Columns.Add("Từ tiết",typeof(int));table.Columns.Add("Đến tiết",typeof(int));
  var names=new[]{"Chủ nhật","Thứ hai","Thứ ba","Thứ tư","Thứ năm","Thứ sáu","Thứ bảy"};for(var i=0;i<busyTimes.Length;i++){var v=busyTimes[i];table.Rows.Add(i+1,v.From.ToDateTime(TimeOnly.MinValue),v.Until.ToDateTime(TimeOnly.MinValue),names[(int)v.Day],v.StartPeriod,v.EndPeriod);}Bind(busy,table);
 }
 private void Bind(DataGridView grid,DataTable table)
 {
  if(grid.DataSource is DataTable old){Ui.Bind(grid,null);tables.Remove(old);old.Dispose();}tables.Add(table);Ui.Bind(grid,table);FormatGrid(grid);
 }
 private void ApplyLayout()
 {
  var compact=Height<AppTheme.S(480);notice.Height=AppTheme.S(32);notice.Padding=new(AppTheme.S(5));explanation.Height=AppTheme.S(compact?40:78);
  resultActions.Height=AppTheme.S(44);inputBar.Height=AppTheme.S(compact?130:150);busyBar.Height=AppTheme.S(compact?130:145);
  foreach(var grid in new[]{offered,busy,options,meetings})FormatGrid(grid);
 }
 private static void FormatGrid(DataGridView grid)
 {
  if(grid.IsDisposed)return;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;grid.RowTemplate.Height=AppTheme.S(32);
  grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.ColumnHeadersHeight=AppTheme.S(40);
  foreach(DataGridViewRow row in grid.Rows)row.Height=AppTheme.S(32);
 }
 private void ClearGrid(DataGridView grid){if(grid.DataSource is not DataTable old)return;Ui.Bind(grid,null);tables.Remove(old);old.Dispose();}
 private void InvalidateSuggestions()
 {
  generation++;dirty=true;cancellation?.Cancel();result=null;put.Enabled=false;ClearGrid(options);ClearGrid(meetings);
  preview.ClearPreview();
  explanation.Clear();notice.Text="Điều kiện đã thay đổi. Sinh lại phương án trước khi đưa vào giỏ.";
 }
 public override async Task RefreshDataAsync()
 {
  var term=Host.MaHocKy;var fresh=await repository.LoadAsync(term);if(IsDisposed||term!=Host.MaHocKy)return;snapshot=fresh;loading=true;
  try{
   var table=new DataTable();table.Columns.Add("Mã HP");table.Columns.Add("Tên học phần");table.Columns.Add("Mã lớp");table.Columns.Add("Tín chỉ",typeof(int));table.Columns.Add("Tình trạng");
   foreach(var c in fresh.Classes.Where(c=>c.Term==term))table.Rows.Add(c.CourseId,c.CourseName,c.Id,c.Credits,c.IsRegistered?"Đã đăng ký — giữ nguyên":c.CanAdd?"Có thể đưa vào phương án":"Không còn khả dụng");Bind(offered,table);BindBusy();
   var dates=fresh.Classes.Where(c=>c.Term==term).SelectMany(c=>c.Meetings).Select(m=>m.Date).ToArray();if(dates.Length>0){from.Value=dates.Min().ToDateTime(TimeOnly.MinValue);until.Value=dates.Max().ToDateTime(TimeOnly.MinValue);}generate.Enabled=true;
  }finally{loading=false;}
 }
 public override void ClearData()
 {
  generation++;cancellation?.Cancel();snapshot=null;result=null;foreach(var grid in new[]{offered,busy,options,meetings})Ui.Bind(grid,null);foreach(var table in tables)table.Dispose();tables.Clear();generate.Enabled=put.Enabled=false;
  preview.ClearPreview();
 }
}
