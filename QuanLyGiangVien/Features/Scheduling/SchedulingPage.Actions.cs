using AntdUI;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed partial class SchedulingPage
{
    private string SemesterId => Store.Catalog.Semesters[Math.Clamp(semester.SelectedIndex, 0, Store.Catalog.Semesters.Count - 1)].Id;
    private IReadOnlyList<ScheduledOccurrence> WeekSessions => SchedulingQuery.Occurrences(Store, monday, monday.AddDays(6)).Where(s => s.Class.SemesterId == SemesterId).ToArray();

    internal void RefreshData(bool animate = true)
    {
        if (!ready) return;
        SurfaceTransition.CancelAll();
        var summary = SchedulingQuery.Summary(Store, SemesterId);
        int activeClasses = Store.Classes.Count(c => c.SemesterId == SemesterId && c.Status != SchedulingLabels.Canceled);
        int scheduledClasses = activeClasses - summary.UnscheduledClasses;
        static float Fraction(int part, int total) => total > 0 ? (float)part / total : 0;
        stats[0].SetValue(summary.TotalClasses, $"{summary.OpenClasses} lớp đang mở", animate: animate);
        stats[0].SetRatio(Fraction(summary.OpenClasses, summary.TotalClasses), animate);
        stats[1].SetValue(summary.TotalRooms, $"{summary.AvailableRooms} sẵn sàng · {summary.MaintenanceRooms} bảo trì", animate: animate);
        stats[1].SetRatio(Fraction(summary.AvailableRooms, summary.TotalRooms), animate);
        stats[2].SetValue(scheduledClasses, $"{summary.UnscheduledClasses} lớp cần xếp lịch", animate: animate);
        stats[2].SetRatio(Fraction(scheduledClasses, activeClasses), animate);
        stats[3].SetValue(summary.FillPercent, $"{summary.Enrolled} / {summary.Capacity} chỗ", true, animate);
        stats[3].SetRatio(Fraction(summary.Enrolled, summary.Capacity), animate);
        var sessions = WeekSessions;
        analytics.SetData(sessions, monday, animate);
        weekCaption.Text = $"{monday:dd/MM} – {monday.AddDays(6):dd/MM/yyyy}";
        var previewItems = sessions.GroupBy(s => s.Class.Id).Select(g => g.First()).Take(3).ToArray();
        for (int i = 0; i < 3; i++)
        {
            var item = previewItems.ElementAtOrDefault(i);
            previews[i].Visible = item != null; previews[i].Tag = item?.Class;
            previews[i].Text = item == null ? "" : $"{Store.Catalog.Courses.First(c => c.Id == item.Class.CourseId).Name}\n{CourseClassRow.Day(item.Date.DayOfWeek)} · Tiết {item.Meeting.StartPeriod}–{item.Meeting.EndPeriod} · {item.RoomId}";
        }
        if (previewItems.Length == 0) { previews[0].Text = "Tuần này chưa có lịch học."; previews[0].Visible = true; previews[0].Enabled = false; }
        else previews[0].Enabled = true;
        if (tabs.SelectIndex == 1) RefreshRooms(animate);
        else if (tabs.SelectIndex == 2) RefreshCalendar(sessions);
        else RefreshClasses(animate);
        table.Visible = tabs.SelectIndex != 2 && ResultCount > 0;
        calendar.Visible = tabs.SelectIndex == 2;
        emptyArtwork.Visible = empty.Visible = reset.Visible = tabs.SelectIndex != 2 && ResultCount == 0;
        weekLabel.Text = weekCaption.Text;
        LayoutPage();
    }

    private void RefreshClasses(bool animate)
    {
        var values = SchedulingQuery.FilterClasses(Store, search.Text, SemesterId, filter.SelectedIndex <= 0 ? "" : filter.SelectedValue?.ToString() ?? "");
        ResultCount = values.Count;
        selectedClassId = values.FirstOrDefault(c => c.Id == selectedClassId)?.Id ?? values.FirstOrDefault()?.Id;
        table.DataSource = values.Select(c => new CourseClassRow(c, Store, visuals)).ToArray();
        detail.ShowClass(Store.Classes.FirstOrDefault(c => c.Id == selectedClassId), Store, animate);
    }
    private void RefreshRooms(bool animate)
    {
        var values = SchedulingQuery.FilterRooms(Store, search.Text, filter.SelectedIndex <= 0 ? "" : filter.SelectedValue?.ToString() ?? "", "", "");
        ResultCount = values.Count;
        selectedRoomId = values.FirstOrDefault(r => r.Id == selectedRoomId)?.Id ?? values.FirstOrDefault()?.Id;
        table.DataSource = values.Select(r => new RoomRow(r, visuals)).ToArray();
        detail.ShowRoom(Store.Rooms.FirstOrDefault(r => r.Id == selectedRoomId), Store, animate);
    }
    private void RefreshCalendar(IReadOnlyList<ScheduledOccurrence> sessions)
    {
        var room = roomFilter.SelectedIndex <= 0 ? null : roomFilter.SelectedValue?.ToString();
        var filtered = sessions.Where(s => room == null || s.RoomId == room).ToArray();
        ResultCount = filtered.Length;
        calendar.SetData(Store, filtered, monday);
        var term = Store.Catalog.Semesters.First(s => s.Id == SemesterId);
        previousWeek.Enabled = monday > SchedulingQuery.WeekStart(term.StartDate);
        nextWeek.Enabled = monday < SchedulingQuery.WeekStart(term.EndDate);
    }

    private void ChangeTab()
    {
        ready = false; debounce.Stop(); search.Text = "";
        ConfigureSelect(filter, tabs.SelectIndex == 1 ? new[] { "Tất cả tòa nhà" }.Concat(Store.Rooms.Select(r => r.Building).Distinct())
            : new[] { "Tất cả trạng thái", SchedulingLabels.Open, SchedulingLabels.Closed, SchedulingLabels.Canceled });
        add.Text = tabs.SelectIndex == 1 ? "Thêm phòng" : "Mở lớp";
        listTitle.Text = tabs.SelectIndex == 1 ? "Danh sách phòng" : tabs.SelectIndex == 2 ? "Lịch giảng dạy" : "Danh sách lớp";
        search.PlaceholderText = tabs.SelectIndex == 1 ? "Tìm mã phòng, tòa nhà, loại phòng…" : "Tìm mã lớp, học phần, giảng viên…";
        ConfigureTable(); ready = true; RefreshData(false);
        SurfaceTransition.Reveal(tabs.SelectIndex == 2 ? calendar : ResultCount > 0 ? table : emptyArtwork, 180);
    }
    internal void SwitchTab(int index)
    {
        if (index == tabs.SelectIndex) return;
        tabs.SelectIndex = index;
    }
    private void ResetFilters()
    {
        ready = false; search.Text = ""; filter.SelectedIndex = roomFilter.SelectedIndex = 0;
        ready = true; debounce.Stop(); RefreshData();
    }
    private void ResetWeek()
    {
        var term = Store.Catalog.Semesters.First(s => s.Id == SemesterId);
        var today = DateOnly.FromDateTime(DateTime.Today);
        monday = SchedulingQuery.WeekStart(today >= term.StartDate && today <= term.EndDate ? today : term.StartDate);
    }
    internal void MoveWeek(int days)
    {
        var term = Store.Catalog.Semesters.First(s => s.Id == SemesterId);
        var next = monday.AddDays(days);
        if (next < SchedulingQuery.WeekStart(term.StartDate) || next > SchedulingQuery.WeekStart(term.EndDate)) return;
        monday = next; RefreshData();
    }
    private void SelectClass(CourseClass item)
    {
        if (tabs.SelectIndex != 0) SwitchTab(0);
        selectedClassId = item.Id; detail.ShowClass(item, Store, true);
    }
    private void SelectRoom(string id) => SelectRoom(id, true);
    private void SelectRoom(string id, bool switchTab)
    {
        selectedRoomId = id;
        if (switchTab && tabs.SelectIndex != 1) SwitchTab(1);
        detail.ShowRoom(Store.Rooms.FirstOrDefault(r => r.Id == id), Store, true);
    }
    private void ShowRoomSchedule(string id)
    {
        SwitchTab(2);
        roomFilter.SelectedIndex = Math.Max(0, roomFilter.Items.ToList().FindIndex(x => x.ToString() == id));
        RefreshData();
    }
    private void EditClass(CourseClass? item)
    {
        using var editor = new CourseClassEditor(Store, item);
        if (editor.ShowDialog(owner) != DialogResult.OK) return;
        selectedClassId = editor.Result?.Id;
        if (editor.Result != null) SelectSavedSemester(editor.Result.SemesterId);
        RefreshData();
        AntdUI.Message.success(owner, "Đã lưu lớp học phần.");
    }
    private void EditRoom(Room? room)
    {
        using var editor = new RoomEditor(Store, room);
        if (editor.ShowDialog(owner) != DialogResult.OK) return;
        selectedRoomId = editor.Result?.Id;
        RefreshRoomChoices(); RefreshData(); AntdUI.Message.success(owner, "Đã lưu phòng học.");
    }
    private bool ConfirmDelete(string title, string message)
        => Modal.open(new Modal.Config(owner, title, message, TType.Warn) { OkText = "Xóa", CancelText = "Hủy", OkType = TTypeMini.Error, MaskClosable = false }) == DialogResult.OK;
    private void DeleteClass(CourseClass item)
    {
        if (item.Enrolled > 0) { AntdUI.Message.warn(owner, "Lớp đã có sinh viên đăng ký. Không thể xóa lớp."); return; }
        if (!ConfirmDelete("Xóa lớp học phần?", $"Xóa lớp {item.Id} và lịch học của lớp?")) return;
        if (!Store.TryDeleteClass(item.Id, out var error)) { AntdUI.Message.error(owner, error!); return; }
        RefreshRoomChoices(); RefreshData();
    }
    internal void SelectSavedSemester(string id)
    {
        bool changed = SemesterId != id;
        ready = false; semester.SelectedIndex = Store.Catalog.Semesters.ToList().FindIndex(s => s.Id == id); ready = true;
        if (changed) ResetWeek();
    }
    internal void RefreshRoomChoices()
    {
        var selectedRoom = roomFilter.SelectedValue?.ToString();
        var selectedBuilding = filter.SelectedValue?.ToString();
        ready = false;
        ConfigureSelect(roomFilter, new[] { "Tất cả phòng" }.Concat(Store.Rooms.Select(r => r.Id)));
        roomFilter.SelectedIndex = Math.Max(0, roomFilter.Items.ToList().FindIndex(x => x.ToString() == selectedRoom));
        if (tabs.SelectIndex == 1)
        {
            ConfigureSelect(filter, new[] { "Tất cả tòa nhà" }.Concat(Store.Rooms.Select(r => r.Building).Distinct()));
            filter.SelectedIndex = Math.Max(0, filter.Items.ToList().FindIndex(x => x.ToString() == selectedBuilding));
        }
        ready = true;
    }
    internal SchedulingViewState CaptureView() => new(tabs.SelectIndex, semester.SelectedIndex, search.Text,
        filter.SelectedValue?.ToString(), roomFilter.SelectedValue?.ToString(), monday, selectedClassId, selectedRoomId,
        new Point(-content.AutoScrollPosition.X, -content.AutoScrollPosition.Y));
    internal void RestoreView(SchedulingViewState state)
    {
        SwitchTab(state.Tab);
        ready = false;
        semester.SelectedIndex = state.Semester; search.Text = state.Query;
        filter.SelectedIndex = Math.Max(0, filter.Items.ToList().FindIndex(x => x.ToString() == state.Filter));
        roomFilter.SelectedIndex = Math.Max(0, roomFilter.Items.ToList().FindIndex(x => x.ToString() == state.RoomFilter));
        monday = state.Week; selectedClassId = state.ClassId; selectedRoomId = state.RoomId;
        ready = true; RefreshData(false); content.AutoScrollPosition = state.Scroll;
    }
    internal bool RoomChoiceExistsForCheck(string id) => roomFilter.Items.ToList().Any(x => x.ToString() == id);
    private void DeleteRoom(Room room)
    {
        if (!ConfirmDelete("Xóa phòng học?", $"Xóa phòng {room.Id} khỏi danh sách?")) return;
        if (!Store.TryDeleteRoom(room.Id, out var error)) { AntdUI.Message.warn(owner, error!); return; }
        RefreshRoomChoices(); RefreshData();
    }
    internal void SetSearchForCheck(string query) { search.Text = query; debounce.Stop(); RefreshData(false); }
    internal void SetRoomFilterForCheck(string id) { roomFilter.SelectedIndex = Math.Max(0, roomFilter.Items.ToList().FindIndex(x => x.ToString() == id)); RefreshData(false); }
    internal void SetSemesterForCheck(int index) { semester.SelectedIndex = index; RefreshData(false); }
    internal void SelectClassForCheck(int index) => SelectClass(Store.Classes[index]);
    internal bool HasHorizontalScrollForCheck => content.HorizontalScroll.Visible;
    internal bool HasDistinctWorkspaceForCheck => !content.Controls.OfType<AntdUI.Panel>().Any(p => p.BackExtend == AppTheme.HeroGradient);
    internal bool WorkspaceStartsEarlyForCheck => listCard.Top - content.AutoScrollPosition.Y <= statsCard.Height + AppTheme.S(18)
        && listCard.Top >= statsCard.Bottom + AppTheme.S(10);
    internal bool HeaderActionsAccessibleForCheck => semester.Parent == navigation && add.Parent == navigation
        && !semester.Bounds.IntersectsWith(add.Bounds) && !teachers.Bounds.IntersectsWith(semester.Bounds);
    internal bool PaletteMatchesLecturerForCheck => BackColor.ToArgb() == AppTheme.Canvas.ToArgb()
        && SchedulingTheme.Primary.ToArgb() == AppTheme.Primary.ToArgb() && (add.ForeColor ?? Color.Empty).ToArgb() == Color.White.ToArgb();
    internal IReadOnlyList<Control> StatsForCheck => stats;
    internal SchedulingTabs TabsForCheck => tabs;
    internal (float Displayed, float Target, bool Running) SeatProgressForCheck => detail.ProgressForCheck;
    internal bool ArtworkReadyForCheck => emptyArtwork.HasArtworkForCheck;
    internal IReadOnlyList<Control> ArtworkForCheck => [emptyArtwork];
    internal Control AgendaForCheck => weeklyCard;
    internal bool AgendaHasNoArtworkForCheck => !weeklyCard.Controls.OfType<SchedulingArtwork>().Any();
    internal SchedulingVisualCatalog VisualsForCheck => visuals;
    internal string? DetailVisualKeyForCheck => detail.VisualKeyForCheck;
    internal bool CalendarThumbnailsFitForCheck => calendar.ThumbnailsFitForCheck;
    internal int CalendarThumbnailCountForCheck => calendar.VisualThumbnailsForCheck;
    internal IReadOnlyList<Font> OwnedFontResourcesForCheck => ownedFonts.Concat(detail.OwnedFontResourcesForCheck).ToArray();
    internal bool SidebarRemovedForCheck => Controls.Count == 2 && content.Left == AppTheme.S(28) && navigation.Height == AppTheme.S(70) && teachers.Parent == navigation;
    internal void ReturnToLecturersForCheck() => teachers.PerformClick();
    internal bool PreviewHasMetadataForCheck => previews.Where(p => p.Visible && p.Enabled).All(p => p.Text.Contains('\n'));
    internal bool HeadersSingleLineForCheck => !pageTitle.TextMultiLine && !breadcrumb.TextMultiLine && !weeklyTitle.TextMultiLine && !listTitle.TextMultiLine;
    internal bool DetailBodyReadableForCheck => detail.ScheduleBodyHeightForCheck >= AppTheme.S(80);
    internal bool TabsAcceptNavigationKeysForCheck => tabs.AcceptsNavigationKeysForCheck;
    internal void NavigateTabForCheck(Keys key) => tabs.NavigateForCheck(key);
    internal bool CalendarCardsReadableForCheck => calendar.CardsReadableForCheck;
    internal void ScrollToBottomForCheck() => content.AutoScrollPosition = new(0, content.VerticalScroll.Maximum);
    internal bool CompactDetailInViewForCheck => detail.Bottom <= content.ClientSize.Height && detail.Bottom > 0;
    internal void ScrollToTopForCheck() => content.AutoScrollPosition = Point.Empty;
    internal object LayoutDiagnosticsForCheck => new {
        DeviceDpi, Size, contentSize = content.Size, content.ClientSize, content.AutoScrollMinSize, content.AutoScrollPosition,
        horizontal = new { content.HorizontalScroll.Visible, content.HorizontalScroll.Maximum },
        controls = content.Controls.Cast<Control>().Select(c => new { type = c.GetType().Name, c.Text, c.Bounds, c.Visible,
            font = c.Font.Size, c.Font.Height, c.ForeColor }).ToArray()
    };
}

internal sealed record SchedulingViewState(int Tab, int Semester, string Query, string? Filter, string? RoomFilter,
    DateOnly Week, string? ClassId, string? RoomId, Point Scroll);
