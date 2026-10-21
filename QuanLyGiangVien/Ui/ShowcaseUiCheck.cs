using System.Data;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Registration.Visuals;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

internal static class ShowcaseUiCheck
{
    internal static void Attach(CampusForm form, string connection)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_SHOWCASE_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(new SqlConnectionStringBuilder(connection).InitialCatalog,
            @"^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$")) throw new InvalidOperationException("Showcase check requires its own synthetic database.");
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 60, 0);
        form.Shown += async (_, _) => await Run(form, connection, output);
    }

    private static async Task Run(CampusForm form, string connection, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Expect(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add(message); }
        async Task Login(bool student)
        {
            form.SelectLoginPortal(student ? LoginPortal.Student : LoginPortal.Training);
            if (!Find<AntdUI.Button>(form, student ? "btnQuickStudentLogin" : "btnQuickTrainingLogin").Enabled) throw new InvalidOperationException("The demo quick-login configuration is unavailable.");
            await form.QuickLoginAsync(student ? LoginPortal.Student : LoginPortal.Training);
            Expect(form.ConnectedIdentity?.Role == (student ? "SINH_VIEN" : "DAO_TAO"), "Demo quick-login uses DPAPI and real SQL password authentication.");
        }
        async Task Select(RegistrationWorkspace host, string page)
        {
            var tabs = Find<TabControl>(host, "registrationPages");
            var target = tabs.TabPages.Cast<TabPage>().Single(t => t.Text == page);
            Find<FlowLayoutPanel>(host, "registrationTaskNavigation").Controls.OfType<Button>()
                .ElementAt(tabs.TabPages.IndexOf(target)).PerformClick();
            await Wait(() => !host.IsBusy); await Task.Delay(100);
        }
        async Task Shot(string name, int width = 1100, int height = 720)
        {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(height)); await Task.Delay(form.CurrentModulePage is MainForm ? 1000 : 180);
            using var bitmap = UiCapture.Client(form); bitmap.Save(Path.Combine(output, name + ".png"));
        }
        try
        {
            await Login(true); var host = (RegistrationWorkspace)form.CurrentModulePage!;
            await host.ReloadForSemesterAsync("HK1-2627");
            await Select(host, "Gợi ý thời khóa biểu");
            var navigation = Find<FlowLayoutPanel>(host, "registrationTaskNavigation");
            Expect(navigation.Controls.OfType<Button>().Count() == 9, "All student tasks have a single visible navigation entry.");
            Expect(!Find<ComboBox>(host, "cboRegistrationModule").Visible, "The old duplicate content picker is not shown.");
            Expect(Find<Label>(host, "lblRegistrationDeadline").Height <= AppTheme.S(32), "Registration dates use one compact status line.");
            var planner = Descendants(host).OfType<SchedulePlannerPage>().Single();
            Find<TextBox>(planner, "txtPlannerCourses").Text = "IT103";
            Find<Button>(planner, "btnPlannerGenerate").PerformClick();
            await Wait(() => Find<Button>(planner, "btnPlannerGenerate").Enabled);
            var preview = Descendants(planner).OfType<PlannerWeekPreview>().Single();
            Expect(preview.MeetingCardCount > 0 && preview.Visible, "Generated options open an actual dated weekly preview.");
            var cards = Descendants(preview).OfType<Button>().Where(b => b.Name.StartsWith("btnPlannerMeeting_", StringComparison.Ordinal)).ToArray();
            Expect(cards.Any(b => b.BackColor == AppTheme.Lilac) && cards.Any(b => b.BackColor == AppTheme.Cyan), "Weekly preview shows both fixed and newly suggested classes.");
            Expect(cards.All(b => b.CanSelect), "Meeting cards remain keyboard focusable.");
            cards[0].Focus(); typeof(Control).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cards[0], [Keys.Enter]);
            Expect(Find<Label>(preview, "lblPlannerMeetingDetail").Text.Contains("Tiết"), "Enter on a focused meeting uses the native button action.");
            var weeks = Find<ComboBox>(preview, "cboPlannerPreviewWeek"); var oldWeek = weeks.SelectedIndex;
            if (weeks.Items.Count > 1) { Find<Button>(preview, "btnPlannerNextWeek").PerformClick(); Expect(weeks.SelectedIndex == oldWeek + 1, "Week navigation changes the displayed dated meetings."); Find<Button>(preview, "btnPlannerPreviousWeek").PerformClick(); }
            await Shot("01-planner-wide", 1440, 960); await Shot("02-planner-compact");
            Expect(Descendants(preview).OfType<Button>().Where(b => b.Name.StartsWith("btnPlannerMeeting_", StringComparison.Ordinal)).All(b => b.Bottom <= b.Parent!.ClientSize.Height), "All meetings in the demo week fit their day at compact size.");
            Expect(preview.Height >= AppTheme.S(220), "Compact weekly preview retains room for day headings and a complete meeting card.");
            Expect(Find<Button>(planner, "btnPlannerToCart").Enabled, "The default visual option can be transferred without opening the comparison table.");
            Expect(weeks.Text.Contains('–'), "Week selection displays the complete date range.");
            host.ConfirmAction = _ => true; await host.ReloadForSemesterAsync("HK1-2627");
            await Select(host, "CTĐT & kế hoạch học");
            var overview = Descendants(host).OfType<AcademicProgressOverview>().Single();
            Expect(overview.GroupCardCount == 3 && overview.Visible, "Academic overview shows three named curriculum groups from the real snapshot.");
            Expect(Descendants(overview).OfType<Label>().Any(l => l.Text.Contains("6/23")), "Overview distinguishes six completed credits from in-progress and planned credits.");
            await Shot("03-academic-wide", 1440, 960); await Shot("04-academic-compact");
            Expect(Descendants(overview).OfType<SoftSurface>().Where(card => card.Parent is TableLayoutPanel)
                .All(card => card.Bottom <= card.Parent!.ClientSize.Height), "Academic metric surfaces fit the table row without clipping their rounded bottoms.");
            var groups = Find<FlowLayoutPanel>(overview, "pnlAcademicGroups");
            Expect(groups.Controls.Cast<Control>().Select(c => c.Top).Distinct().Count() == 1, "Three curriculum groups share one readable row at compact size.");
            File.WriteAllText(Path.Combine(output, "academic-layout.json"), JsonSerializer.Serialize(Descendants(groups).OfType<Label>().Select(l => new { l.Text, l.Height, l.Width, preferred = l.GetPreferredSize(new Size(l.Width, 0)).Height, top = l.PointToScreen(Point.Empty).Y, bottom = l.PointToScreen(new Point(0, l.Height)).Y, parentBottom = l.Parent!.PointToScreen(new Point(0, l.Parent.ClientSize.Height)).Y }), new JsonSerializerOptions { WriteIndented = true }));
            var nextCourse = Descendants(overview).OfType<Button>().FirstOrDefault();
            Expect(nextCourse is not null, "Academic next steps are actionable native controls."); nextCourse!.PerformClick();
            Expect(Find<ComboBox>(host, "cboAcademicPlanCourse").Visible, "A curriculum next step opens the editable planning form.");
            await Select(host, "Giỏ nháp & đổi lớp");
            await Shot("13-cart-wide", 1440, 960);
            var cartDetail = Find<TextBox>(host, "txtCartDetails");
            Expect(cartDetail.Height == AppTheme.S(104), "Wide cart retains the full read-only detail height.");
            await Shot("14-cart-compact");
            Expect(cartDetail.Height == AppTheme.S(48) && cartDetail.Parent is RegistrationCartPage, "Compact cart still shrinks its native read-only detail to 48px.");
            await Select(host, "Kết quả đăng ký");
            var creditResult = Find<TextBox>(host, "txtCreditPlanCheck");
            Expect(!creditResult.Visible && creditResult.Parent is KetQuaPage, "Credit-check output stays hidden without reserving an empty rounded field.");
            Find<Button>(host, "btnCheckCreditPlan").PerformClick(); await Wait(() => !host.IsBusy);
            Expect(creditResult.Visible && creditResult.Text.Length > 0, "Credit check can reveal its original native output after styling.");
            await Shot("15-registration-results-compact");
            await form.LogoutAsync(); await Login(false); await form.OpenModuleAsync(CampusModule.Reports);
            host = (RegistrationWorkspace)form.CurrentModulePage!; await host.ReloadForSemesterAsync("HK1-2627");
            await Select(host, "Nhập dữ liệu nguồn"); var import = Descendants(host).OfType<DataImportPage>().Single();
            import.LoadDocument(JsonSerializer.Serialize(new { batchId = Guid.NewGuid(), source = "SYNTHETIC DEMO - native presentation verification", rows = new[] { new { kind = "PROGRAM", id = "DEMO_IMPORT", payload = new { name = "Chương trình mô phỏng nhập liệu", major = "CNTT", cohort = 2024, requiredCredits = 120 } } } }));
            Find<Button>(import, "btnImportPreview").PerformClick(); await Wait(() => !host.IsBusy);
            var grid = Descendants(import).OfType<DataGridView>().Single();
            await Shot("05-import-preview");
            Expect(Convert.ToString(grid.Rows[0].Cells["row_index"].FormattedValue) == "1" && string.IsNullOrEmpty(grid.Rows[0].Cells["row_index"].ErrorText), "Import row number is readable and has no contradictory error glyph.");
            Expect(Convert.ToBoolean(grid.Rows[0].Cells["valid"].Value), "Import preview validates the synthetic source without committing it.");
            import.LoadDocument(JsonSerializer.Serialize(new { batchId = Guid.NewGuid(), source = "SYNTHETIC DEMO - invalid row", rows = new[] { new { kind = "PROGRAM", id = "BAD_IMPORT", payload = new { name = "Chương trình chưa hợp lệ", major = "NOT_REAL", cohort = 2024, requiredCredits = 120 } } } }));
            Find<Button>(import, "btnImportPreview").PerformClick(); await Wait(() => !host.IsBusy);
            Expect(!Convert.ToBoolean(grid.Rows[0].Cells["valid"].Value) && !Find<Button>(import, "btnImportCommit").Enabled, "Invalid import has a clear error state and cannot commit.");
            host.ConfirmAction = _ => true; await host.ReloadForSemesterAsync("HK1-2627"); await Select(host, "Nhu cầu & sức khỏe");
            await Shot("06-operations-wide", 1440, 960); await Shot("07-operations-compact");
            var operations = Descendants(host).OfType<OperationsDashboardPage>().Single();
            var demand = Descendants(operations).OfType<DataGridView>().Single();
            Expect(demand.Rows.Count >= 8 && demand.Height >= demand.ColumnHeadersHeight + 4 * AppTheme.S(38), "Compact operations leaves room for several real data rows.");
            using (var snapshot = await new OperationsRepository(connection).SnapshotAsync(host.MaHocKy))
            {
                var available = snapshot.Tables["Demand"]!.AsEnumerable().Where(r => Convert.ToString(r["lifecycle"]) == "REGISTRATION").Sum(r => Convert.ToInt64(r["available"]));
                Expect(Find<Label>(operations, "lblOperationsMetric2").Text == available.ToString("N0") && available > 0, "Available-seat metric matches the actual open-class snapshot.");
            }
            await Select(host, "Đợt, quota & hàng chờ"); await Shot("08-seat-compact");
            Expect(Descendants(host).OfType<TabControl>().Where(t => t.Visible && t.IsHandleCreated).All(t =>
                Enumerable.Range(0, t.TabCount).All(index =>
                {
                    var header = t.GetTabRect(index); header.Offset(t.Location);
                    return !header.IntersectsWith(t.Parent!.ClientRectangle);
                })), "Outer and nested native tab headers are completely clipped after pages are populated.");
            var waitingButton = Descendants(host).OfType<Button>().Single(b => b.Visible && b.AccessibleName == "Hàng chờ & lời mời");
            Expect(waitingButton.Width >= TextRenderer.MeasureText(waitingButton.Text, waitingButton.Font).Width + AppTheme.S(18),
                "Waiting-list navigation shows the complete label without truncation.");
            var seatQueue = Find<DataGridView>(host, "dgvSeatWaitlist");
            bool SeatHeadersFit() => seatQueue.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).All(c =>
            {
                var style = c.HeaderCell.InheritedStyle;
                var text = TextRenderer.MeasureText(c.HeaderText, style.Font ?? seatQueue.Font,
                    new Size(Math.Max(1, c.Width - style.Padding.Horizontal - AppTheme.S(4)), int.MaxValue), TextFormatFlags.WordBreak);
                var line = TextRenderer.MeasureText(c.HeaderText, style.Font ?? seatQueue.Font);
                return text.Height + style.Padding.Vertical <= seatQueue.ColumnHeadersHeight &&
                    line.Width + style.Padding.Horizontal + AppTheme.S(18) <= c.Width;
            });
            Expect(SeatHeadersFit(), "Waiting-list headers have enough width and height for complete single-line captions; compact tables can scroll horizontally.");
            var seatTabs = (TabControl)seatQueue.Parent!.Parent!;
            seatTabs.SelectedIndex = 1; await Task.Delay(80); seatTabs.SelectedIndex = 0; await Task.Delay(80);
            Expect(SeatHeadersFit() && seatQueue.Columns["position"]!.HeaderText == "Thứ tự tham khảo",
                "Returning to the waiting-list tab retains full captions and measured column widths after generic formatting.");
            await Shot("18-seat-wide", 1440, 960);
            await form.OpenModuleAsync(CampusModule.Catalogs); await Shot("09-catalogs-compact");
            await form.OpenModuleAsync(CampusModule.Academics); await Shot("10-academics-compact");
            Expect(form.CurrentModulePage is QuanLyGiangVien.Features.Academics.AcademicPage academic && !academic.HasUnsavedChanges, "Opening academic references creates no unsaved draft or exit prompt.");
            await form.OpenModuleAsync(CampusModule.Students); await Shot("11-students-compact");
            var studentInputs = Descendants(form.CurrentModulePage!).OfType<TextBox>().ToArray();
            Expect(studentInputs.All(input => input.Parent is SoftInputSurface && input.Parent.Height >= AppTheme.S(34)), "Student fields have padded surfaces while retaining native TextBox controls.");
            await Shot("16-students-wide", 1440, 960);
            await form.OpenModuleAsync(CampusModule.Academics); await Shot("17-academics-wide", 1440, 960);
            await form.OpenModuleAsync(CampusModule.Lecturers); await Shot("12-lecturers-wide", 1440, 960);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, checks, scope = "Native WinForms controls, focus/dialog-key dispatch, actual SQL fixture; offscreen 96-DPI captures, not physical keyboard or multi-monitor DPI certification." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks })); }
        finally { if (form.CurrentModulePage is RegistrationWorkspace host) host.ConfirmAction = _ => true; form.Close(); }
    }

    private static T Find<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static async Task Wait(Func<bool> predicate) { var until = DateTime.UtcNow.AddSeconds(45); while (!predicate()) { if (DateTime.UtcNow > until) throw new InvalidOperationException("Showcase action timed out."); await Task.Delay(40); } }
}
