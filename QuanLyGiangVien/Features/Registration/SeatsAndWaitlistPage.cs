using System.Data;
using System.Text.Json;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;
using QuanLyGiangVien.Ui;
namespace QuanLyGiangVien.Features.Registration;

internal sealed class SeatsAndWaitlistPage : ModulePage
{
    private readonly SeatAllocationRepository repository;
    private readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    private readonly DataGridView queue=Ui.Grid();
    private readonly Label notice=new(){Name="lblSeatNotice",Dock=DockStyle.Top,Height=AppTheme.S(44),BackColor=AppTheme.Lilac,Padding=new(AppTheme.S(6))};
    private readonly Button join=Ui.Button("Chờ các lớp trong giỏ",true),claim=Ui.Button("Nhận chỗ",true),decline=Ui.Button("Từ chối"),cancel=Ui.Button("Rút khỏi hàng chờ"),process=Ui.Button("Xử lý hàng chờ",true),lookup=Ui.Button("Tra cứu kết quả nhận chỗ");
    private readonly List<DataGridView> grids=[];
    private readonly List<Control> configEditors=[];
    private readonly List<ConfigDraft> configs=[];
    private bool binding;
    private DataSet? snapshot;
    public override bool HasUnsavedChanges=>configs.Any(c=>c.IsDirty);
    private sealed class ConfigDraft(DataGridView grid,TextBox id,Dictionary<string,Control> inputs)
    {
        internal DataGridView Grid {get;}=grid;
        internal TextBox Id {get;}=id;
        internal Dictionary<string,Control> Inputs {get;}=inputs;
        internal string? SelectedId,Version;
        internal bool Restoring;
        internal string Baseline="";
        internal string Signature()=>JsonSerializer.Serialize(new[]{Id.Text}.Concat(Inputs.Values.Select(c=>c switch{
            DateTimePicker d=>$"{d.Value.Ticks}:{d.Checked}",CheckBox b=>b.Checked.ToString(),ComboBox b=>b.SelectedIndex.ToString(),_=>c.Text})));
        internal bool IsDirty=>Signature()!=Baseline;
    }
    public SeatsAndWaitlistPage(RegistrationWorkspace host):base(host)
    {
        Name="SeatsAndWaitlistPage";repository=new(host.ConnectionString);queue.Name="dgvSeatWaitlist";grids.Add(queue);
        queue.ColumnHeadersHeight=AppTheme.S(44);queue.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;queue.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;queue.RowTemplate.Height=AppTheme.S(40);
        // Apply after Ui.Grid's shared formatter, including when revisiting a nested tab.
        queue.DataBindingComplete+=(_,_)=>FormatQueue();
        queue.VisibleChanged+=(_,_)=>FormatQueue();
        queue.HandleCreated+=(_,_)=>FormatQueue();
        var waiting=new TabPage("Hàng chờ & lời mời");var actions=Ui.Toolbar();actions.Dock=DockStyle.Bottom;actions.AutoSize=false;actions.Height=AppTheme.S(52);actions.AutoScroll=true;
        var refresh=Ui.Button("Tải lại");refresh.Name="btnSeatRefresh";join.Name="btnSeatJoin";claim.Name="btnSeatClaim";process.Name="btnSeatWorker";lookup.Name="btnSeatResult";
        actions.Controls.AddRange([refresh,join,claim,decline,cancel,lookup,process]);waiting.Controls.Add(queue);waiting.Controls.Add(actions);tabs.TabPages.Add(waiting);
        refresh.Click+=async(_,_)=>await Host.ExecuteAsync(RefreshDataAsync);
        join.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>
        {
            using var cart=await Host.Workflow.CartAsync(Host.MaHocKy);var codes=JsonSerializer.Deserialize<string[]>(Convert.ToString(cart.Rows[0]["class_json"])!)??[];
            using var result=await repository.JoinAsync(Host.MaHocKy,codes);await RefreshDataAsync();notice.Text="Đã vào hàng chờ. Xét theo nhóm và thứ tự gửi trong lượt; yêu cầu bị chặn được quét luân phiên. Chưa giữ chỗ.";
        });
        process.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>{using var result=await repository.ProcessAsync(Host.MaHocKy,100);await RefreshDataAsync();notice.Text="Đã xử lý lời mời và hết hạn. Thông báo được chuyển sau khi giao dịch đã ghi.";});
        claim.Click+=async(_,_)=>await Host.ExecuteAsync(ClaimAsync);
        decline.Click+=async(_,_)=>await Host.ExecuteAsync(()=>RespondAsync(true));cancel.Click+=async(_,_)=>await Host.ExecuteAsync(()=>RespondAsync(false));
        lookup.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>
        {
            if(Ui.Current(queue) is not {} row)return;
            var request=row.IsNull("claim_request_id")?SeatAllocationValidation.ClaimRequest(Host.Identity.AccountId,Convert.ToInt64(row["id"]),Convert.ToString(row["version"])!):Guid.Parse(Convert.ToString(row["claim_request_id"])!);
            using var result=await Host.Workflow.ResultAsync(request);notice.Text=result.Rows.Count==1?"Máy chủ đã xác nhận đăng ký từ lời mời. Xem Kết quả đăng ký để đối soát.":"Chưa có kết quả đã ghi. Tải lại lời mời; nếu còn hiệu lực có thể nhận lại cùng yêu cầu.";
        });
        queue.SelectionChanged+=(_,_)=>ApplyDeadline();
        AddConfig("Đợt đăng ký","ROUND",0,[("name","Tên đợt"),("startsUtc","Bắt đầu (giờ Việt Nam)"),("endsUtc","Kết thúc (giờ Việt Nam)"),("publicUtc","Mở chung (giờ Việt Nam)"),("offerMinutes","Phút nhận chỗ"),("calendarMode","Ngày nghỉ"),("publishPosition","Hiện vị trí chờ")]);
        AddConfig("Nhóm ưu tiên","GROUP",1,[("name","Tên nhóm"),("major","Mã ngành (nếu có)"),("cohort","Khóa (nếu có)"),("source","Nguồn xác nhận"),("priority","Độ ưu tiên (số nhỏ trước)")]);
        AddConfig("Quota lớp","QUOTA",2,[("class","Mã lớp học phần"),("group","Mã nhóm"),("capacity","Số chỗ dự trữ"),("releaseUtc","Trả phần dư (giờ Việt Nam)")]);
        AddConfig("Thành viên nhóm","MEMBER",4,[("group","Mã nhóm"),("student","Mã sinh viên"),("source","Nguồn xác nhận"),("expiresUtc","Hiệu lực tới (giờ Việt Nam)")]);
        AddConfig("Lịch hẹn","TICKET",5,[("round","Mã đợt"),("group","Mã nhóm"),("opensUtc","Giờ được đăng ký (Việt Nam)")]);
        Controls.Add(tabs);Controls.Add(notice);
    }
    private void AddConfig(string title,string kind,int tableIndex,(string Key,string Label)[] fields)
    {
        var tab=new TabPage(title);var modes=new TabControl{Dock=DockStyle.Fill};var list=new TabPage("Danh sách");var grid=Ui.Grid();grids.Add(grid);grid.Tag=tableIndex;list.Controls.Add(grid);modes.TabPages.Add(list);
        if(Service.User.LaDaoTao)
        {
            var edit=new TabPage("Cấu hình");var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new(AppTheme.S(12))};
            var inputs=new Dictionary<string,Control>();var id=PhaseThreeUi.Text("txtSeat"+kind+"Id");AddField(flow,"Mã cấu hình",id);
            foreach(var field in fields)
            {
                Control input=field.Key.EndsWith("Utc",StringComparison.Ordinal)?new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm",Width=AppTheme.S(230),ShowCheckBox=field.Key=="publicUtc",Value=DateTime.UtcNow.AddHours(7).AddDays(field.Key=="startsUtc"?-1:7)}:
                    field.Key=="calendarMode"?PhaseThreeUi.Choices(new("ELAPSED","Tính theo phút thực"),new("SKIP_HOLIDAYS","Dời hạn nhận khỏi ngày nghỉ")):
                    field.Key=="publishPosition"?new CheckBox{Checked=true,AutoSize=true}:
                    PhaseThreeUi.Text("txtSeat"+kind+field.Key,field.Key is "name" or "source"?250:20);
                if(input is TextBox text && field.Key is "offerMinutes" or "capacity")text.Text="30";
                if(input is TextBox priority && field.Key=="priority")priority.Text="1000";
                inputs.Add(field.Key,input);AddField(flow,field.Label,input);
            }
            var save=Ui.Button("Lưu cấu hình",true);save.Name="btnSeat"+kind+"Save";
            flow.Controls.Add(save);edit.Controls.Add(flow);modes.TabPages.Add(edit);configEditors.Add(flow);
            var draft=new ConfigDraft(grid,id,inputs);draft.Baseline=draft.Signature();configs.Add(draft);
            grid.CurrentCellChanged+=(_,_)=>
            {
                if(binding||draft.Restoring||Ui.Current(grid) is not {} row||Convert.ToString(row["id"])==draft.SelectedId)return;
                if(draft.IsDirty&&!Host.Confirm("Có cấu hình chưa lưu. Bỏ bản nháp để chọn cấu hình khác?"))
                {
                    draft.Restoring=true;
                    BeginInvoke(()=>{if(!IsDisposed)RestoreSelection(draft);draft.Restoring=false;});return;
                }
                LoadDraft(draft,row);
            };
            save.Click+=async(_,_)=>await Host.ExecuteAsync(async()=>
            {
                var payload=new Dictionary<string,object?>();if(kind=="ROUND")payload["term"]=Host.MaHocKy;
                foreach(var pair in inputs)payload[pair.Key]=pair.Value switch{DateTimePicker date=>date.ShowCheckBox&&!date.Checked?null:DateTime.SpecifyKind(date.Value.AddHours(-7),DateTimeKind.Utc),CheckBox check=>check.Checked,ComboBox combo=>PhaseThreeUi.Choice(combo),TextBox text=>string.IsNullOrWhiteSpace(text.Text)?null:text.Text.Trim(),_=>null};
                if(!Host.Confirm("Lưu cấu hình phân bổ chỗ? Các đăng ký/lời mời hiện có được kiểm trước khi ghi."))return;
                var expected=id.Text.Trim()==draft.SelectedId?draft.Version:null;
                using var result=await repository.SaveAsync(new(kind,id.Text.Trim(),expected,JsonSerializer.Serialize(payload)));
                draft.SelectedId=id.Text.Trim();draft.Baseline=draft.Signature();await RefreshDataAsync();
            });
        }
        tab.Controls.Add(modes);tabs.TabPages.Add(tab);
    }
    private static void RestoreSelection(ConfigDraft draft)
    {
        draft.Grid.ClearSelection();
        var row=draft.Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>Convert.ToString(r.Cells["id"].Value)==draft.SelectedId);
        draft.Grid.CurrentCell=row?.Cells.Cast<DataGridViewCell>().FirstOrDefault(c=>c.Visible);
        if(row!=null)row.Selected=true;
    }
    private static void LoadDraft(ConfigDraft draft,DataRow row)
    {
        draft.Id.Text=Convert.ToString(row["id"]);draft.SelectedId=draft.Id.Text;draft.Version=Convert.ToString(row["version"]);
        var columns=new Dictionary<string,string>{{"group","group_id"},{"class","class_id"},{"round","round_id"},{"startsUtc","starts_utc"},{"endsUtc","ends_utc"},{"publicUtc","public_utc"},{"offerMinutes","offer_minutes"},{"calendarMode","calendar_mode"},{"publishPosition","publish_position"},{"releaseUtc","release_utc"},{"expiresUtc","expires_utc"},{"opensUtc","opens_utc"}};
        foreach(var pair in draft.Inputs)
        {
            var column=columns.GetValueOrDefault(pair.Key,pair.Key);if(!row.Table.Columns.Contains(column))continue;var value=row[column];
            if(pair.Value is TextBox text)text.Text=value==DBNull.Value?"":Convert.ToString(value);
            if(pair.Value is DateTimePicker date){date.Checked=value!=DBNull.Value;if(value!=DBNull.Value)date.Value=Convert.ToDateTime(value).AddHours(7);}
            if(pair.Value is CheckBox check)check.Checked=value!=DBNull.Value&&Convert.ToBoolean(value);
            if(pair.Value is ComboBox combo)for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is WorkflowChoice c&&c.Value==Convert.ToString(value))combo.SelectedIndex=i;
        }
        draft.Baseline=draft.Signature();
    }
    private static void AddField(FlowLayoutPanel flow,string caption,Control input)
    {
        var row=new FlowLayoutPanel{Width=AppTheme.S(650),Height=AppTheme.S(48),WrapContents=false};var label=Ui.Label(caption);label.AutoSize=false;label.Width=AppTheme.S(270);row.Controls.Add(label);row.Controls.Add(input);flow.Controls.Add(row);
    }
    private async Task ClaimAsync()
    {
        if(Ui.Current(queue) is not {} row)return;var id=Convert.ToInt64(row["id"]);var version=Convert.ToString(row["version"])!;
        var request=SeatAllocationValidation.ClaimRequest(Host.Identity.AccountId,id,version);
        using var result=await repository.ClaimAsync(id,version,request);Host.NotifyDataChanged();await RefreshDataAsync();notice.Text="Máy chủ đã xác nhận nhận chỗ. Nếu mất phản hồi, tải lại hoặc tra cứu; cùng lời mời dùng cùng mã yêu cầu.";
    }
    private async Task RespondAsync(bool declineOffer)
    {
        if(Ui.Current(queue) is not {} row)return;using var result=await repository.RespondAsync(Convert.ToInt64(row["id"]),Convert.ToString(row["version"])!,declineOffer);await RefreshDataAsync();
    }
    public override async Task RefreshDataAsync()
    {
        var fresh=await repository.SnapshotAsync(Host.MaHocKy);if(IsDisposed){fresh.Dispose();return;}
        binding=true;
        try
        {
            ClearData();snapshot=fresh;
            Ui.Bind(queue,fresh.Tables[3]);foreach(var grid in grids.Where(g=>g!=queue))Ui.Bind(grid,fresh.Tables[(int)grid.Tag!]);
            foreach(var draft in configs)
            {
                var dirty=draft.IsDirty;
                var row=fresh.Tables[(int)draft.Grid.Tag!].Rows.Cast<DataRow>().FirstOrDefault(r=>Convert.ToString(r["id"])==draft.SelectedId);
                if(row==null&&!dirty)row=fresh.Tables[(int)draft.Grid.Tag!].Rows.Cast<DataRow>().FirstOrDefault();
                if(!dirty&&row!=null)LoadDraft(draft,row);
                RestoreSelection(draft);
            }
        }
        finally{binding=false;}
        FormatQueue();
        foreach(var grid in grids)if(grid.Columns.Contains("version"))grid.Columns["version"]!.Visible=false;
        using var permissions=await Host.EventLog.PermissionsAsync();bool Has(string name)=>Service.User.VaiTro=="QUAN_TRI"||permissions.Rows.Cast<DataRow>().Any(r=>Convert.ToString(r["permission"])==name);
        foreach(var editor in configEditors)editor.Enabled=Has("SEAT_MANAGE");process.Visible=Service.User.LaDaoTao;process.Enabled=Has("WAITLIST_OPERATE");join.Visible=claim.Visible=decline.Visible=lookup.Visible=Service.User.LaSinhVien;
        notice.Text="Chờ chưa giữ chỗ; lời mời có hạn. Ưu tiên/FIFO giữa SV đủ điều kiện; hồ sơ bị chặn được kiểm lại luân phiên.";ApplyDeadline();
    }
    private void FormatQueue()
    {
        if(queue.IsDisposed||!queue.IsHandleCreated||!queue.Visible)return;
        var captions=new Dictionary<string,string>{{"id","Mã chờ"},{"student","Sinh viên"},{"course","Học phần"},{"class_json","Lớp chờ"},{"status","Trạng thái"},{"priority","Ưu tiên"},{"offer_expires_utc","Hạn nhận (VN)"},{"position","Thứ tự tham khảo"},{"last_error","Cần xử lý"}};
        foreach(DataGridViewColumn column in queue.Columns)
        {
            column.Visible=captions.ContainsKey(column.Name)&&(!Service.User.LaSinhVien||column.Name is not ("student" or "priority"));
            if(captions.TryGetValue(column.Name,out var caption))column.HeaderText=caption;
            if(column.Visible)
            {
                var headerStyle=column.HeaderCell.InheritedStyle;
                column.MinimumWidth=Math.Max(column.MinimumWidth,TextRenderer.MeasureText(column.HeaderText,headerStyle.Font??queue.Font).Width+headerStyle.Padding.Horizontal+AppTheme.S(24));
            }
            column.FillWeight=column.Name switch{"id" or "position" or "priority"=>45,"class_json" or "last_error"=>180,"offer_expires_utc"=>140,_=>85};
        }
        queue.ColumnHeadersHeight=AppTheme.S(44);
    }
    public override void ApplyDeadline(){var state=Convert.ToString(Ui.Current(queue)?["status"]);claim.Enabled=decline.Enabled=state=="Offered";cancel.Enabled=state is "Waiting" or "Offered";lookup.Enabled=Ui.Current(queue)!=null;}
    public override void ClearData(){var previous=binding;binding=true;try{foreach(var grid in grids)Ui.Bind(grid,null);snapshot?.Dispose();snapshot=null;}finally{binding=previous;}}
}
