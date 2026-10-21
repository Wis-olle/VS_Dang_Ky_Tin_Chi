using System.Collections.Immutable;
using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Academics;
using QuanLyGiangVien.Ui;
using QuanLyGiangVien.Features.Registration.Visuals;
namespace QuanLyGiangVien.Features.Registration;
internal sealed class AcademicPlanningPage:ModulePage
{
 private readonly AcademicPlanningRepository repository;
 private readonly TabControl views=new(){Dock=DockStyle.Fill};
 private readonly AcademicProgressOverview overview=new();
 private readonly Label notice=new(){Name="lblAcademicPlanNotice",Dock=DockStyle.Top,Height=AppTheme.S(36),Padding=new(AppTheme.S(8)),BackColor=AppTheme.Lilac,AutoEllipsis=true};
 private readonly TextBox details=new(){Name="txtAcademicPlanDetails",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White};
 private readonly DataGridView audit=Ui.Grid(),groups=Ui.Grid(),allocation=Ui.Grid(),plan=Ui.Grid(),issues=Ui.Grid(),comments=Ui.Grid(),offerings=Ui.Grid(),substitutions=Ui.Grid(),assignments=Ui.Grid();
 private readonly ComboBox students=PhaseThreeUi.Choices(),programs=PhaseThreeUi.Choices(),course=PhaseThreeUi.Choices(),term=PhaseThreeUi.Choices(),advisor=PhaseThreeUi.Choices();
 private readonly ComboBox action=PhaseThreeUi.Choices(new("COMMENT","Góp ý"),new("APPROVE","Duyệt kế hoạch"),new("RETURN","Yêu cầu sửa"));
 private readonly TextBox comment=PhaseThreeUi.Text("txtAcademicPlanComment",500),source=PhaseThreeUi.Text("txtAcademicPlanSource",500),mappingReason=PhaseThreeUi.Text("txtAcademicAdvisorReason",500);
 private readonly ComboBox catalogCourse=PhaseThreeUi.Choices(),catalogTerm=PhaseThreeUi.Choices(),subSource=PhaseThreeUi.Choices(),subTarget=PhaseThreeUi.Choices();
 private readonly CheckBox active=new(){Text="Phân công hiệu lực",Checked=true,AutoSize=true};
 private readonly Button save=Ui.Button("Lưu nháp",true),submit=Ui.Button("Gửi cố vấn"),add=Ui.Button("Thêm vào kế hoạch"),remove=Ui.Button("Bỏ môn đã chọn");
 private readonly List<Button> officialWrites=[];
 private AcademicPlanningSnapshot? snapshot;private DataSet? directory;
 private ImmutableArray<AcademicPlanItem> draft=[];private string? draftHash,whatIf,draftStudent;private bool dirty;
 public override bool HasUnsavedChanges=>dirty||comment.TextLength>0||source.TextLength>0||mappingReason.TextLength>0;
 public AcademicPlanningPage(RegistrationWorkspace host):base(host)
 {
  Name="AcademicPlanningPage";repository=new(host.ConnectionString);
  foreach(var grid in new[]{audit,groups,allocation,plan,issues,comments,offerings,substitutions,assignments}){
   grid.DataBindingComplete+=(_,_)=>CompactGrid(grid);grid.VisibleChanged+=(_,_)=>CompactGrid(grid);
  }
  audit.Name="dgvDegreeAudit";plan.Name="dgvAcademicPlan";issues.Name="dgvAcademicPlanIssues";
  course.Name="cboAcademicPlanCourse";term.Name="cboAcademicPlanTerm";programs.Name="cboAcademicWhatIfProgram";students.Name="cboAcademicPlanStudent";action.Name="cboAcademicAdviceAction";
  add.Name="btnAcademicPlanAdd";remove.Name="btnAcademicPlanRemove";save.Name="btnAcademicPlanSave";submit.Name="btnAcademicPlanSubmit";
  var top=Ui.Toolbar();var reload=Ui.Button("Tải lại");var simulate=Ui.Button("Mô phỏng CTĐT");var official=Ui.Button("Về CTĐT hiện tại");
  top.AutoSize=false;top.Height=AppTheme.S(52);top.Padding=Padding.Empty;top.WrapContents=false;top.Visible=!Service.User.LaSinhVien;
  reload.MinimumSize=new(AppTheme.S(92),AppTheme.S(32));reload.Height=AppTheme.S(32);reload.Margin=new(AppTheme.S(2));
  if(!Service.User.LaSinhVien)PhaseThreeUi.Field(top,"Sinh viên được phân công",students);
  var simulation=Editor();PhaseThreeUi.Field(simulation,"CTĐT muốn mô phỏng",programs);simulation.Controls.AddRange([simulate,official]);top.Controls.Add(reload);
  simulate.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(programs.SelectedItem is WorkflowChoice c){whatIf=c.Value;await LoadSnapshot();}});
  official.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{whatIf=null;await LoadSnapshot();});
  reload.Click+=async(_,_)=>await Host.ExecuteAsync(RefreshDataAsync);
  var loadStudent=Ui.Button("Xem sinh viên");loadStudent.MinimumSize=new(AppTheme.S(100),AppTheme.S(32));loadStudent.Height=AppTheme.S(32);loadStudent.Margin=new(AppTheme.S(2));if(!Service.User.LaSinhVien){top.Controls.Add(loadStudent);loadStudent.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(ConfirmDiscard()){dirty=false;draft=[];draftHash=null;comment.Clear();source.Clear();mappingReason.Clear();await LoadSnapshot();}});}
  AddTab("Tổng quan học tập",overview);AddTab("Tiến độ CTĐT",audit);AddTab("Nhóm yêu cầu",groups);AddTab("Phân bổ tín chỉ",allocation);AddTab("Kế hoạch đã xếp",plan);AddTab("Điều kiện & dự kiến mở",issues);AddTab("Ý kiến cố vấn",comments);
  AddTab("Mô phỏng CTĐT",simulation);AddTab("Giải thích tiến độ",details);
  var edit=Editor();PhaseThreeUi.Field(edit,"Môn học",course);PhaseThreeUi.Field(edit,"Học kỳ dự kiến",term);edit.Controls.AddRange([add,remove]);AddTab("Xếp kế hoạch",edit);
  overview.CourseSelected+=code=>{var index=course.Items.Cast<WorkflowChoice>().ToList().FindIndex(c=>c.Value==code);if(index>=0)course.SelectedIndex=index;var tab=views.TabPages.Cast<TabPage>().FirstOrDefault(t=>t.Text=="Xếp kế hoạch");if(tab is not null)views.SelectedTab=tab;course.Focus();};
  add.Click+=(_,_)=>{if(snapshot is null||course.SelectedItem is not WorkflowChoice c||term.SelectedItem is not WorkflowChoice t)return;if(draft.Any(i=>i.Course==c.Value)){notice.Text="Môn đã có trong kế hoạch. Bỏ dòng cũ trước khi chuyển kỳ.";return;}draft=draft.Add(new(c.Value,t.Value));dirty=true;Render();};
  remove.Click+=(_,_)=>{if(Ui.Current(plan) is not{} row)return;draft=draft.Where(i=>i.Course!=Convert.ToString(row["course"])).ToImmutableArray();dirty=true;Render();};
  var bottom=Ui.Toolbar();bottom.Dock=DockStyle.Bottom;bottom.AutoSize=false;bottom.Height=AppTheme.S(44);bottom.Padding=Padding.Empty;bottom.WrapContents=false;
  var reconcile=Ui.Button("Đối soát lại nháp");reconcile.Name="btnAcademicPlanReconcile";bottom.Controls.AddRange([save,submit,reconcile]);
  foreach(var button in new[]{save,submit,reconcile}){button.MinimumSize=new(AppTheme.S(100),AppTheme.S(32));button.Height=AppTheme.S(32);button.Margin=new(AppTheme.S(2));}
  save.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(snapshot?.AssignedProgram is null)return;await repository.SavePlanAsync(new(snapshot.AssignedProgram,draftHash??snapshot.SourceHash,snapshot.PlanVersion,draft));dirty=false;draftHash=null;await LoadSnapshot();Host.NotifyDataChanged();});
  submit.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(snapshot?.PlanVersion is null)return;await repository.SubmitPlanAsync(snapshot.Student,snapshot.PlanVersion,snapshot.SourceHash);await LoadSnapshot();Host.NotifyDataChanged();});
  reconcile.Click+=(_,_)=>{if(snapshot is null)return;draftHash=snapshot.SourceHash;dirty=true;Render();notice.Text="Đã đối soát nháp theo dữ liệu mới. Xem điều kiện trước khi lưu.";};
  bottom.Visible=Service.User.LaSinhVien;
  if(!Service.User.LaSinhVien)BuildAdvisorEditor();
  BuildCatalogEditor();Controls.Add(views);Controls.Add(bottom);Controls.Add(notice);Controls.Add(top);
 }
 private static FlowLayoutPanel Editor()=>new(){Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,Padding=new(AppTheme.S(12)),BackColor=Color.White};
 private void AddTab(string title,Control control){var tab=new TabPage(title);tab.Controls.Add(control);views.TabPages.Add(tab);}
 private void BuildAdvisorEditor()
 {
  var editor=Editor();PhaseThreeUi.Field(editor,"Thao tác",action);PhaseThreeUi.Field(editor,"Nội dung cho sinh viên",comment);var apply=Ui.Button("Lưu ý kiến / quyết định",true);apply.Name="btnAcademicPlanAdvise";editor.Controls.Add(apply);AddTab("Cố vấn",editor);
  officialWrites.Add(apply);
  apply.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(snapshot?.PlanVersion is null)return;await repository.AdviseAsync(snapshot.Student,snapshot.PlanVersion,PhaseThreeUi.Choice(action),comment.Text);comment.Clear();await LoadSnapshot();Host.NotifyDataChanged();});
  if(Service.User.VaiTro!="QUAN_TRI")return;
  var mapping=Editor();PhaseThreeUi.Field(mapping,"Tài khoản cố vấn",advisor);mapping.Controls.Add(active);PhaseThreeUi.Field(mapping,"Căn cứ phân công",mappingReason);
  var assign=Ui.Button("Lưu phân công",true);mapping.Controls.Add(assign);AddTab("Phân công cố vấn",mapping);AddTab("Phân công hiện tại",assignments);
  assign.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(students.SelectedItem is not WorkflowChoice s||advisor.SelectedItem is not WorkflowChoice a)return;await repository.AssignAdvisorAsync(s.Value,int.Parse(a.Value),active.Checked,mappingReason.Text);mappingReason.Clear();await RefreshDataAsync();});
 }
 private void BuildCatalogEditor()
 {
  AddTab("Dự kiến mở môn",offerings);AddTab("Thay thế một chiều",substitutions);
  if(Service.User.LaSinhVien)return;
  var editor=Editor();PhaseThreeUi.Field(editor,"Môn dự kiến mở",catalogCourse);PhaseThreeUi.Field(editor,"Kỳ dự kiến",catalogTerm);
  var publish=Ui.Button("Lưu căn cứ mở môn",true);editor.Controls.Add(publish);
  officialWrites.Add(publish);
  if(Service.User.VaiTro=="QUAN_TRI"){
   PhaseThreeUi.Field(editor,"Môn nguồn thay thế",subSource);PhaseThreeUi.Field(editor,"Thay cho môn CTĐT",subTarget);
   var confirm=Ui.Button("Xác nhận thay thế một chiều");editor.Controls.Add(confirm);
   officialWrites.Add(confirm);
   confirm.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(snapshot?.SelectedProgram is null)return;var a=PhaseThreeUi.Choice(subSource);var b=PhaseThreeUi.Choice(subTarget);var old=snapshot.Data.Tables["Substitutions"]!.Rows.Cast<DataRow>().FirstOrDefault(r=>Convert.ToString(r["source_course"])==a&&Convert.ToString(r["target_course"])==b);await repository.SaveSubstitutionAsync(snapshot.SelectedProgram,a,b,source.Text,old is null?null:PhaseThreeUi.Version(old["version"]));source.Clear();await LoadSnapshot();});
  }
  PhaseThreeUi.Field(editor,"Nguồn / quyết định có thật",source);AddTab("Căn cứ kế hoạch",editor);
  publish.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{if(snapshot is null||catalogCourse.SelectedItem is null||catalogTerm.SelectedItem is null)return;var c=PhaseThreeUi.Choice(catalogCourse);var t=PhaseThreeUi.Choice(catalogTerm);var old=snapshot.Data.Tables["Offerings"]!.Rows.Cast<DataRow>().FirstOrDefault(r=>Convert.ToString(r["course"])==c&&Convert.ToString(r["term"])==t);await repository.SaveOfferingAsync(c,t,source.Text,old is null?null:PhaseThreeUi.Version(old["version"]));source.Clear();await LoadSnapshot();});
 }
 public override async Task RefreshDataAsync()
 {
  DataSet fresh;
  try{fresh=await repository.DirectoryAsync();}catch(PersistenceException e)when(e.Kind==PersistenceFailure.BusinessRejected){notice.Text=e.Message;return;}
  if(IsDisposed){fresh.Dispose();return;}
  var previous=students.SelectedItem as WorkflowChoice;directory?.Dispose();directory=fresh;
  Fill(students,fresh.Tables["Students"]!,"ma_sv","ho_ten",previous?.Value);
  Fill(advisor,fresh.Tables["Advisors"]!,"ma_tk","ten_dang_nhap");Ui.Bind(assignments,fresh.Tables["Assignments"]);
  await LoadSnapshot();
 }
 private async Task LoadSnapshot()
 {
  var student=Service.User.LaSinhVien?Host.Identity.StudentId:(students.SelectedItem as WorkflowChoice)?.Value;
  if(student is null){notice.Text="Chưa có sinh viên được phân công. Quản trị cần phân công cố vấn và cấp quyền ADVISE_PLAN.";return;}
  AcademicPlanningSnapshot fresh;
  try{fresh=await repository.SnapshotAsync(student,whatIf);}catch(PersistenceException e)when(e.Kind==PersistenceFailure.BusinessRejected){notice.Text=e.Message;return;}
  if(IsDisposed){fresh.Dispose();return;}
  if(!dirty||draftStudent!=fresh.Student){draft=fresh.Items;draftHash=fresh.Data.Tables["Plan"]!.Rows.Count>0?PhaseThreeUi.Version(fresh.Data.Tables["Plan"]!.Rows[0]["source_hash"]):fresh.SourceHash;dirty=false;}
  draftStudent=fresh.Student;
  foreach(var grid in new[]{offerings,substitutions,comments})Ui.Bind(grid,null);
  snapshot?.Dispose();snapshot=fresh;Fill(programs,fresh.Data.Tables["Programs"]!,"id","name",whatIf);
  Fill(course,fresh.Data.Tables["CourseChoices"]!,"course","name");Fill(catalogCourse,fresh.Data.Tables["CourseChoices"]!,"course","name");Fill(subSource,fresh.Data.Tables["CourseChoices"]!,"course","name");
  Fill(subTarget,fresh.Data.Tables["Courses"]!,"course","name");Fill(term,fresh.Data.Tables["Terms"]!,"ma_hk","name");Fill(catalogTerm,fresh.Data.Tables["Terms"]!,"ma_hk","name");
  Ui.Bind(offerings,fresh.Data.Tables["Offerings"]);Ui.Bind(substitutions,fresh.Data.Tables["Substitutions"]);Ui.Bind(comments,fresh.Data.Tables["Comments"]);Render();
 }
 private void Render()
 {
  if(snapshot is null)return;
  var result=snapshot.Audit(draft);var checksResult=snapshot.Check(draft);overview.ShowProgress(snapshot,result,checksResult);var table=new DataTable();foreach(var name in new[]{"course","Tên học phần","Nhóm","Loại yêu cầu","TC môn","Đã đạt","Đang học","Dự kiến","Thiếu TC"})table.Columns.Add(name);
  foreach(var row in result.Courses)table.Rows.Add(row.Requirement.Course,AcademicProgressOverview.CourseName(snapshot,row.Requirement.Course),result.Groups.FirstOrDefault(g=>g.Id==row.Requirement.Group)?.Name??"Ngoài nhóm",row.Requirement.Required?"Bắt buộc":"Tự chọn",row.Requirement.Credits,row.Done,row.InProgress,row.Planned,Math.Max(0,row.Requirement.Credits-row.Done));BindOwned(audit,table);
  var grouped=new DataTable();foreach(var name in new[]{"Nhóm yêu cầu","TC cần đạt","Đã đạt","Đang học","Dự kiến","Thiếu TC"})grouped.Columns.Add(name);
  foreach(var row in result.Groups)grouped.Rows.Add(row.Name,row.Required,row.Done,row.InProgress,row.Planned,Math.Max(0,row.Required-row.Done));BindOwned(groups,grouped);
  var allocated=new DataTable();foreach(var name in new[]{"Môn nguồn","Đáp ứng môn","Nhóm","Trạng thái","TC phân bổ"})allocated.Columns.Add(name);
  foreach(var a in result.Allocations)allocated.Rows.Add(a.SourceCourse,a.TargetCourse,a.Group,a.Stage switch{"DONE"=>"Đã đạt","IN_PROGRESS"=>"Đang học/chờ điểm",_=>"Dự kiến"},a.Credits);BindOwned(allocation,allocated);
  var planned=new DataTable();planned.Columns.Add("course");planned.Columns.Add("Tên học phần");planned.Columns.Add("term");foreach(var p in draft)planned.Rows.Add(p.Course,AcademicProgressOverview.CourseName(snapshot,p.Course),p.Term);BindOwned(plan,planned);
  var checks=new DataTable();foreach(var name in new[]{"course","Tên học phần","Mức","Nội dung"})checks.Columns.Add(name);foreach(var i in checksResult)checks.Rows.Add(i.Course,AcademicProgressOverview.CourseName(snapshot,i.Course),i.Blocking?"Cần xử lý trước gửi":"Điều kiện tương lai",i.Message);BindOwned(issues,checks);
  var stored=snapshot.Data.Tables["Plan"]!.Rows.Cast<DataRow>().FirstOrDefault();var status=Convert.ToString(stored?["status"]) switch{"SUBMITTED"=>"Chờ cố vấn","APPROVED"=>"Đã được cố vấn duyệt","RETURNED"=>"Cố vấn yêu cầu sửa",_=>"Nháp"};
  notice.Text=snapshot.Curriculum is null?"Chưa có CTĐT chính thức đã công bố và gắn cho sinh viên. Cần nhập, công bố và gắn CTĐT thật trước khi tính tiến độ hoặc lưu kế hoạch.":
   $"{snapshot.Student} • CTĐT {snapshot.SelectedProgram} • Đã phân bổ {result.AllocatedDone}/{snapshot.Curriculum.RequiredCredits} TC; đang học {result.AllocatedInProgress}, dự kiến {result.AllocatedPlanned}. {status}.\n"+
   (snapshot.IsWhatIf?"Đây là mô phỏng chỉ đọc; hồ sơ và kế hoạch chính thức được giữ nguyên.":"Kế hoạch không giữ chỗ đăng ký. Môn ở kỳ trước vẫn phải có kết quả đạt khi đăng ký thực tế. ")+
   " Đối soát này chưa thay quyết định tốt nghiệp. Môn tự chọn đóng góp vào hạn mức nhóm, không yêu cầu học mọi môn tự chọn. Các môn tương đương được gộp thành một yêu cầu; xem Phân bổ tín chỉ để biết môn nguồn đáp ứng.";
  if(stored is not null&&!Convert.ToBoolean(stored["source_current"]))notice.Text+="\nDữ liệu nguồn đã đổi. Đối soát lại nháp trước khi lưu/gửi; duyệt cũ cần được xem xét lại.";
  details.Text=notice.Text;
  notice.Text=snapshot.Curriculum is null?"Chưa có CTĐT chính thức phù hợp ngành/khóa. Cần nhập, công bố và gắn CTĐT thật.":
   $"{AcademicProgressOverview.ProgramName(snapshot)} · "+(snapshot.IsWhatIf?"Mô phỏng chỉ đọc":status)+(dirty?" · Có thay đổi chưa lưu":"")+
   (stored is not null&&!Convert.ToBoolean(stored["source_current"])?" · Nguồn đã đổi: cần đối soát lại":" · Kế hoạch không giữ chỗ đăng ký");
  bool canEdit=Service.User.LaSinhVien&&snapshot.Curriculum is not null&&!snapshot.IsWhatIf;
  foreach(var button in officialWrites)button.Enabled=!snapshot.IsWhatIf;
  add.Enabled=remove.Enabled=save.Enabled=canEdit;submit.Enabled=canEdit&&!dirty&&snapshot.PlanVersion is not null;
 }
 private static void Fill(ComboBox combo,DataTable table,string value,string label,string? selected=null)
 {
  combo.Items.Clear();foreach(DataRow row in table.Rows){if(table.Columns.Contains("active")&&!Convert.ToBoolean(row["active"]))continue;combo.Items.Add(new WorkflowChoice(Convert.ToString(row[value])!,row[value]+" • "+row[label]));}
  if(combo.Items.Count>0)combo.SelectedIndex=Math.Max(0,combo.Items.Cast<WorkflowChoice>().ToList().FindIndex(v=>v.Value==selected));
 }
 private static void BindOwned(DataGridView grid,DataTable table){var old=grid.DataSource as DataTable;Ui.Bind(grid,table);old?.Dispose();}
 private static void CompactGrid(DataGridView grid)
 {
  grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;grid.RowTemplate.Height=AppTheme.S(36);
  grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.EnableResizing;grid.ColumnHeadersHeight=AppTheme.S(32);
  grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
  foreach(DataGridViewRow row in grid.Rows)row.Height=AppTheme.S(36);
  foreach(DataGridViewColumn column in grid.Columns){
   if(column.Name is "version" or "program_version" or "plan_version")column.Visible=false;
   column.HeaderText=column.Name switch{"source_course"=>"Môn nguồn","target_course"=>"Thay cho môn","content"=>"Nội dung","action"=>"Thao tác","account_id"=>"Tài khoản cố vấn",_=>column.HeaderText};
  }
 }
 private bool ConfirmDiscard()=>!HasUnsavedChanges||MessageBox.Show(this,"Bỏ nội dung chưa lưu để xem sinh viên khác?","Kế hoạch học tập",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
 public override void ClearData()
 {
  foreach(var grid in new[]{audit,groups,allocation,plan,issues}){var old=grid.DataSource as DataTable;Ui.Bind(grid,null);old?.Dispose();}
  foreach(var grid in new[]{offerings,substitutions,comments,assignments})Ui.Bind(grid,null);
  snapshot?.Dispose();snapshot=null;directory?.Dispose();directory=null;
  overview.ClearProgress();
 }
}
