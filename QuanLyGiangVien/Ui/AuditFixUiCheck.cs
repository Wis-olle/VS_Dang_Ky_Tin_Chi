using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in native regressions for unsaved drafts, on disposable SQL fixtures only.</summary>
internal static class AuditFixUiCheck
{
    internal static void Attach(CampusForm form, string connection)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_AUDIT_FIX_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        if (!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Audit native checks require a disposable database.");
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => await Run(form, connection, output);
    }

    private static async Task Run(CampusForm form, string cs, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Expect(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks.Add(text); }
        async Task Sql(string sql)
        {
            await using var c = new SqlConnection(cs); await c.OpenAsync();
            using var q = new SqlCommand(sql, c); await q.ExecuteNonQueryAsync();
        }
        async Task Login(bool student)
        {
            form.SelectLoginPortal(student ? LoginPortal.Student : LoginPortal.Training);
            Find<TextBox>(form, "txtLoginUsername").Text = student ? "sv2024001" : "daotao";
            Find<TextBox>(form, "txtLoginPassword").Text = Environment.GetEnvironmentVariable(student ? "DEMO_STUDENT_PASSWORD" : "DEMO_TRAINING_PASSWORD")!;
            await form.AuthenticateAsync();
            Expect(form.ConnectedIdentity?.Role == (student ? "SINH_VIEN" : "DAO_TAO"), "Fixture password login opens intended portal.");
        }
        async Task Select(RegistrationWorkspace workspace, string label)
        {
            Find<ComboBox>(workspace, "cboRegistrationModule").SelectedItem = label;
            await Wait(() => !workspace.IsBusy); await Task.Delay(100);
        }
        try
        {
            await using (var c = new SqlConnection(cs))
            {
                await c.OpenAsync();
                using var q = new SqlCommand("DELETE dbo.CAMPUS_PERMISSION WHERE account_id IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao') AND permission='SEAT_MANAGE'; INSERT dbo.CAMPUS_PERMISSION SELECT ma_tk,'SEAT_MANAGE' FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao'; DELETE dbo.SEAT_QUOTA; DELETE dbo.SEAT_TICKET; DELETE dbo.SEAT_ROUND; INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,offer_minutes,calendar_mode,publish_position) VALUES('AU_R1','HK1-2627',N'Audit round one',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),30,'ELAPSED',1),('AU_R2','HK1-2627',N'Audit round two',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,1,SYSUTCDATETIME()),30,'ELAPSED',1);", c);
                await q.ExecuteNonQueryAsync();
            }
            RegistrationWorkspace workspace;
            if (Environment.GetEnvironmentVariable("CAMPUS_AUDIT_SCOPE_ONLY") != "1")
            {
            await Login(false); await form.OpenModuleAsync(CampusModule.Reports);
            workspace = (RegistrationWorkspace)form.CurrentModulePage!;
            await workspace.ReloadForSemesterAsync("HK1-2627"); await Select(workspace, "Đợt, quota & hàng chờ");
            var seats = Descendants(workspace).OfType<SeatsAndWaitlistPage>().Single();
            var seatTabs = Descendants(seats).OfType<TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Hàng chờ & lời mời")); seatTabs.SelectedIndex = 1;
            var grid = Descendants(seatTabs.SelectedTab!).OfType<DataGridView>().Single();
            var first = grid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["id"].Value) == "AU_R1");
            grid.CurrentCell = first.Cells["name"]; first.Selected = true;
            var input = Find<TextBox>(seats, "txtSeatROUNDname"); input.Text = "Unsubmitted round draft";
            var prompts = 0; workspace.ConfirmAction = _ => { prompts++; return false; };
            var second = grid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["id"].Value) == "AU_R2");
            grid.CurrentCell = second.Cells["name"]; second.Selected = true; await Task.Delay(120);
            Expect(seats.HasUnsavedChanges && input.Text == "Unsubmitted round draft", "Cancelling round selection retains unsaved editor values and dirty state.");
            Expect(Convert.ToString(grid.CurrentRow?.Cells["id"].Value) == "AU_R1" && prompts == 1, $"Cancelling round selection restores the prior selected row once (row={grid.CurrentRow?.Cells["id"].Value}, prompts={prompts}).");
            await workspace.ExecuteAsync(seats.RefreshDataAsync);
            Expect(input.Text == "Unsubmitted round draft" && seats.HasUnsavedChanges, "Refreshing a seat snapshot preserves unsaved editor values.");
            await Sql("UPDATE dbo.SEAT_ROUND SET name=N'Concurrent operator edit' WHERE id='AU_R1';");
            await workspace.ExecuteAsync(seats.RefreshDataAsync);
            workspace.ConfirmAction = _ => true;
            Descendants(seatTabs.SelectedTab!).OfType<TabControl>().Single().SelectedIndex = 1;
            Find<Button>(seats, "btnSeatROUNDSave").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(input.Text == "Unsubmitted round draft" && seats.HasUnsavedChanges && Find<Label>(workspace, "lblRegistrationStatus").Text.Contains("đã thay đổi"), "Refresh keeps the original draft row version, so a concurrent operator edit rejects stale save and retains the draft.");
            Descendants(seatTabs.SelectedTab!).OfType<TabControl>().Single().SelectedIndex = 0;
            workspace.ConfirmAction = _ => true;
            grid.CurrentCell = grid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["id"].Value) == "AU_R2").Cells["name"];
            await Task.Delay(100);
            Expect(input.Text == "Audit round two" && !seats.HasUnsavedChanges, "Confirming row discard loads a new clean baseline.");
            await Sql("DELETE dbo.SEAT_MEMBER WHERE group_id='AU_G'; DELETE dbo.SEAT_GROUP WHERE id='AU_G'; INSERT dbo.SEAT_GROUP(id,name,source,priority) VALUES('AU_G',N'Audit fixture group',N'Synthetic fixture',1000); INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES('AU_Q1','LHP0001','AU_G',1,DATEADD(day,1,SYSUTCDATETIME())),('AU_Q2','LHP0002','AU_G',1,DATEADD(day,1,SYSUTCDATETIME()));");
            await workspace.ExecuteAsync(seats.RefreshDataAsync); seatTabs.SelectedIndex = 3;
            var quotaGrid = Descendants(seatTabs.SelectedTab!).OfType<DataGridView>().Single();
            quotaGrid.CurrentCell = quotaGrid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["id"].Value) == "AU_Q1").Cells["capacity"];
            var capacity = Find<TextBox>(seats, "txtSeatQUOTAcapacity"); capacity.Text = "2";
            workspace.ConfirmAction = _ => false;
            quotaGrid.CurrentCell = quotaGrid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["id"].Value) == "AU_Q2").Cells["capacity"];
            await Task.Delay(100);
            Expect(capacity.Text == "2" && Convert.ToString(quotaGrid.CurrentRow?.Cells["id"].Value) == "AU_Q1", "Cancelling quota selection restores its own row and unsaved capacity.");
            await workspace.ExecuteAsync(seats.RefreshDataAsync);
            Expect(capacity.Text == "2" && seats.HasUnsavedChanges, "Quota draft survives snapshot refresh independently of the round editor.");
            workspace.ConfirmAction = _ => true;
            Descendants(seatTabs.SelectedTab!).OfType<TabControl>().Single().SelectedIndex = 1;
            Find<Button>(seats, "btnSeatQUOTASave").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(!seats.HasUnsavedChanges && capacity.Text == "2", "Successful quota save refreshes its clean baseline.");
            await form.LogoutAsync();
            }
            await Login(true);
            workspace = (RegistrationWorkspace)form.CurrentModulePage!;
            await workspace.ReloadForSemesterAsync("HK1-2627"); await Select(workspace, "Đơn ngoại lệ");
            var petition = Descendants(workspace).OfType<ExceptionRequestsPage>().Single();
            var reason = Find<TextBox>(petition, "txtExceptionReason"); reason.Text = "Draft bound to HK1";
            var terms = Find<ComboBox>(workspace, "cboHocKy"); workspace.ConfirmAction = _ => false;
            terms.SelectedValue = "HK2-2627"; await Wait(() => !workspace.IsBusy); await Task.Delay(100);
            Expect(workspace.MaHocKy == "HK1-2627" && reason.Text == "Draft bound to HK1", "Cancelling semester change restores the original term and petition draft.");
            Find<Button>(workspace, "btnLamMoiDangKy").PerformClick(); await Wait(() => !workspace.IsBusy);
            Expect(reason.Text == "Draft bound to HK1" && !petition.IsDisposed, "Cancelling workspace reload preserves the same draft controls.");
            await Select(workspace, "Thông báo"); await Select(workspace, "Đơn ngoại lệ");
            Expect(reason.Text == "Draft bound to HK1", "Revisiting a tab preserves its pending draft.");
            workspace.ConfirmAction = _ => true; terms.SelectedValue = "HK2-2627";
            await Wait(() => !workspace.IsBusy); await Task.Delay(100);
            petition = Descendants(workspace).OfType<ExceptionRequestsPage>().Single();
            Expect(workspace.MaHocKy == "HK2-2627" && reason.IsDisposed && !petition.HasUnsavedChanges, "Confirmed semester discard disposes every old scoped form and resets dirty state.");
            Expect(Find<TextBox>(petition, "txtExceptionReason").TextLength == 0 && Find<TextBox>(petition, "txtExceptionClass").TextLength == 0, "New semester has no stale petition values.");
            var oldPlanner = Descendants(workspace).OfType<SchedulePlannerPage>().Single();
            await Select(workspace, "Gợi ý thời khóa biểu");
            Find<TextBox>(oldPlanner, "txtPlannerCourses").Text = "IT102";
            workspace.ConfirmAction = _ => false; terms.SelectedValue = "HK1-2627"; await Task.Delay(100);
            Expect(workspace.MaHocKy == "HK2-2627" && Find<TextBox>(oldPlanner, "txtPlannerCourses").Text == "IT102", "Planner input also blocks unconfirmed semester reassignment.");
            workspace.ConfirmAction = _ => true; terms.SelectedValue = "HK1-2627"; await Wait(() => !workspace.IsBusy);
            Expect(oldPlanner.IsDisposed && Find<TextBox>(workspace, "txtPlannerCourses").TextLength == 0, "Confirmed semester discard replaces planner inputs and captured search state.");
            await Select(workspace, "Đơn ngoại lệ");
            form.ClientSize = new(AppTheme.S(1100), AppTheme.S(720)); await Task.Delay(150);
            using var bitmap = UiCapture.Client(form); bitmap.Save(Path.Combine(output, "scope-reset-compact.png"));
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, scope = "Native controls, code-driven input on disposable SQL fixture; no physical input, FPS or DPI scaling claim." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString());
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
        }
        finally
        {
            if (form.CurrentModulePage is RegistrationWorkspace workspace) workspace.ConfirmAction = _ => true;
            form.Close();
        }
    }
    private static T Find<T>(Control parent, string name) where T : Control => Descendants(parent).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static async Task Wait(Func<bool> predicate)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!predicate()) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Native action timed out."); await Task.Delay(40); }
    }
}
