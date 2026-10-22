using AntdUI;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed partial class SchedulingPage
{
    // Lấy mã kỳ từ bộ chọn; các buổi trong tuần được giới hạn trong đúng kỳ đang xem.
    private string SemesterId => Store.Catalog.Semesters[Math.Clamp(semester.SelectedIndex, 0, Store.Catalog.Semesters.Count - 1)].Id;
    private IReadOnlyList<ScheduledOccurrence> WeekSessions => SchedulingQuery.Occurrences(Store, monday, monday.AddDays(6)).Where(s => s.Class.SemesterId == SemesterId).ToArray();

    // Cập nhật danh sách và thống kê
    internal void RefreshData(bool animate = true)
    {
        if (!ready) return;
        ConfigureTimetableSubjects();
        SurfaceTransition.CancelAll();
        // Tính số lớp, phòng, mức lấp đầy từ Store; tỷ lệ được bảo vệ trước tổng bằng 0.
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
        // Một nguồn buổi học tuần được dùng cho biểu đồ, phần xem trước và lịch bên dưới.
        var sessions = WeekSessions;
        analytics.SetData(sessions, monday, animate);
        weekCaption.Text = $"{monday:dd/MM} – {monday.AddDays(6):dd/MM/yyyy}";
        var previewItems = sessions.GroupBy(s => s.Class.Id).Select(g => g.First()).Take(3).ToArray();
        // Mỗi lớp chỉ xuất hiện một lần trong tối đa ba dòng xem trước, dù có nhiều buổi học.
        for (int i = 0; i < 3; i++)
        {
            var item = previewItems.ElementAtOrDefault(i);
            previews[i].Visible = item != null; previews[i].Tag = item?.Class;
            previews[i].Text = item == null ? "" : $"{Store.Catalog.Courses.First(c => c.Id == item.Class.CourseId).Name}\n{CourseClassRow.Day(item.Date.DayOfWeek)} · Tiết {item.Meeting.StartPeriod}–{item.Meeting.EndPeriod} · {item.RoomId}";
        }
        if (previewItems.Length == 0) { previews[0].Text = "Tuần này chưa có lịch học."; previews[0].Visible = true; previews[0].Enabled = false; }
        else previews[0].Enabled = true;
        // Nạp đúng nguồn của tab đang chọn rồi đồng bộ trạng thái có/không có kết quả.
        if (tabs.SelectIndex == 1) RefreshRooms(animate);
        else if (tabs.SelectIndex == 2) RefreshCalendar(sessions);
        else RefreshClasses(animate);
        table.Visible = tabs.SelectIndex != 2 && ResultCount > 0;
        calendar.Visible = tabs.SelectIndex == 2;
        emptyArtwork.Visible = empty.Visible = reset.Visible = tabs.SelectIndex != 2 && ResultCount == 0;
        weekLabel.Text = weekCaption.Text;
        LayoutPage();
    }

    // Tìm kiếm và hiển thị lớp học phần
    private void RefreshClasses(bool animate)
    {
        var values = SchedulingQuery.FilterClasses(Store, search.Text, SemesterId, filter.SelectedIndex <= 0 ? "" : filter.SelectedValue?.ToString() ?? "");
        ResultCount = values.Count;
        // Giữ lớp đang chọn nếu vẫn còn trong kết quả; nếu đã bị lọc mất thì chọn lớp đầu tiên.
        selectedClassId = values.FirstOrDefault(c => c.Id == selectedClassId)?.Id ?? values.FirstOrDefault()?.Id;
        table.DataSource = values.Select(c => new CourseClassRow(c, Store, visuals)).ToArray();
        detail.ShowClass(Store.Classes.FirstOrDefault(c => c.Id == selectedClassId), Store, animate);
    }
    // Tìm kiếm và hiển thị phòng học
    private void RefreshRooms(bool animate)
    {
        var values = SchedulingQuery.FilterRooms(Store, search.Text, filter.SelectedIndex <= 0 ? "" : filter.SelectedValue?.ToString() ?? "", "", "");
        ResultCount = values.Count;
        // Đồng bộ phòng được chọn, dòng bảng và bảng chi tiết theo cùng tập kết quả đã lọc.
        selectedRoomId = values.FirstOrDefault(r => r.Id == selectedRoomId)?.Id ?? values.FirstOrDefault()?.Id;
        table.DataSource = values.Select(r => new RoomRow(r, visuals)).ToArray();
        detail.ShowRoom(Store.Rooms.FirstOrDefault(r => r.Id == selectedRoomId), Store, animate);
    }
    // Hiển thị lịch tuần
    private void RefreshCalendar(IReadOnlyList<ScheduledOccurrence> sessions)
    {
        // Khi chưa tải danh mục sinh viên, để lịch rỗng thay vì hiển thị nhầm lịch của người khác.
        var filtered = UsesStudentDirectory && !timetableDirectoryReady ? []
            : TimetableScope.Select(Store, sessions, timetableDirectory, scopeKind, timetableSubjectId);
        ResultCount = filtered.Count;
        calendar.SetData(Store, filtered, monday, SchedulingQuery.Occurrences(Store, monday, monday.AddDays(6)),
            personalSchedule: scopeKind == TimetableScopeKind.Student, caption: TimetableCaption());
        // Khóa nút trước/sau ở biên học kỳ, kể cả tuần chỉ có một phần ngày nằm trong kỳ.
        var term = Store.Catalog.Semesters.First(s => s.Id == SemesterId);
        previousWeek.Enabled = monday > SchedulingQuery.WeekStart(term.StartDate);
        nextWeek.Enabled = monday < SchedulingQuery.WeekStart(term.EndDate);
    }

    // Chuyển tab
    private void ChangeTab()
    {
        // Tạm chặn sự kiện trong lúc thay bộ lọc, tiêu đề và cột bảng cho tab mới.
        ready = false; debounce.Stop(); search.Text = "";
        ConfigureSelect(filter, tabs.SelectIndex == 1 ? new[] { "Tất cả tòa nhà" }.Concat(Store.Rooms.Select(r => r.Building).Distinct())
            : new[] { "Tất cả trạng thái", SchedulingLabels.Open, SchedulingLabels.Closed, SchedulingLabels.Canceled });
        add.Text = tabs.SelectIndex == 1 ? "Thêm phòng" : "Mở lớp";
        listTitle.Text = tabs.SelectIndex == 1 ? "Danh sách phòng" : tabs.SelectIndex == 2 ? "Thời khóa biểu" : "Danh sách lớp";
        search.PlaceholderText = tabs.SelectIndex == 1 ? "Tìm mã phòng, tòa nhà, loại phòng…" : "Tìm mã lớp, học phần, giảng viên…";
        ConfigureTable(); ready = true; RefreshData(false);
        SurfaceTransition.Reveal(tabs.SelectIndex == 2 ? calendar : ResultCount > 0 ? table : emptyArtwork, 180);
    }
    // Chọn tab lớp, phòng hoặc lịch tuần
    internal void SwitchTab(int index)
    {
        if (index == tabs.SelectIndex) return;
        tabs.SelectIndex = index;
    }
    // Đặt lại bộ lọc
    private void ResetFilters()
    {
        ready = false; search.Text = ""; filter.SelectedIndex = 0; timetableSubjectId = null;
        ready = true; debounce.Stop(); RefreshData();
    }
    // Về tuần hiện tại trong học kỳ
    private void ResetWeek()
    {
        var term = Store.Catalog.Semesters.First(s => s.Id == SemesterId);
        var today = DateOnly.FromDateTime(DateTime.Today);
        monday = SchedulingQuery.WeekStart(today >= term.StartDate && today <= term.EndDate ? today : term.StartDate);
    }
    // Chuyển tuần
    internal void MoveWeek(int days)
    {
        var term = Store.Catalog.Semesters.First(s => s.Id == SemesterId);
        var next = monday.AddDays(days);
        if (next < SchedulingQuery.WeekStart(term.StartDate) || next > SchedulingQuery.WeekStart(term.EndDate)) return;
        monday = next; RefreshData();
    }
    // Xem chi tiết lớp học phần
    private void SelectClass(CourseClass item)
    {
        if (tabs.SelectIndex != 0) SwitchTab(0);
        selectedClassId = item.Id; detail.ShowClass(item, Store, true);
    }
    // Chọn phòng học
    private void SelectRoom(string id) => SelectRoom(id, true);
    // Xem chi tiết phòng học
    private void SelectRoom(string id, bool switchTab)
    {
        selectedRoomId = id;
        if (switchTab && tabs.SelectIndex != 1) SwitchTab(1);
        detail.ShowRoom(Store.Rooms.FirstOrDefault(r => r.Id == id), Store, true);
    }
    // Xem lịch sử dụng phòng
    private void ShowRoomSchedule(string id)
    {
        SwitchTab(2);
        _ = SelectTimetableAsync(TimetableScopeKind.Room, id);
    }
    // Thêm hoặc sửa lớp học phần
    private void EditClass(CourseClass? item)
    {
        // Form con tự kiểm tra và lưu qua Store; trang cha chỉ cập nhật khi người dùng xác nhận OK.
        using var editor = new CourseClassEditor(Store, item);
        if (editor.ShowDialog(owner) != DialogResult.OK) return;
        selectedClassId = editor.Result?.Id;
        if (editor.Result != null) SelectSavedSemester(editor.Result.SemesterId);
        RefreshData();
        AntdUI.Message.success(owner, "Đã lưu lớp học phần.");
    }
    // Thêm hoặc sửa phòng học
    private void EditRoom(Room? room)
    {
        // Chỉ nạp lại danh sách và lựa chọn phòng sau khi form thêm/sửa lưu thành công.
        using var editor = new RoomEditor(Store, room);
        if (editor.ShowDialog(owner) != DialogResult.OK) return;
        selectedRoomId = editor.Result?.Id;
        RefreshRoomChoices(); RefreshData(); AntdUI.Message.success(owner, "Đã lưu phòng học.");
    }
    // Xác nhận xóa
    private bool ConfirmDelete(string title, string message)
        => Modal.open(new Modal.Config(owner, title, message, TType.Warn) { OkText = "Xóa", CancelText = "Hủy", OkType = TTypeMini.Error, MaskClosable = false }) == DialogResult.OK;
    // Xóa lớp học phần
    private void DeleteClass(CourseClass item)
    {
        // Chặn sớm lớp đang có sinh viên; Store tiếp tục kiểm tra các ràng buộc khi xóa thật.
        if (item.Enrolled > 0) { AntdUI.Message.warn(owner, "Lớp đã có sinh viên đăng ký. Không thể xóa lớp."); return; }
        if (!ConfirmDelete("Xóa lớp học phần?", $"Xóa lớp {item.Id} và lịch học của lớp?")) return;
        if (!Store.TryDeleteClass(item.Id, out var error)) { AntdUI.Message.error(owner, error!); return; }
        RefreshRoomChoices(); RefreshData();
    }
    // Chọn học kỳ của lớp vừa lưu
    internal void SelectSavedSemester(string id)
    {
        bool changed = SemesterId != id;
        ready = false; semester.SelectedIndex = Store.Catalog.Semesters.ToList().FindIndex(s => s.Id == id); ready = true;
        if (changed) ResetWeek();
    }
    // Cập nhật bộ chọn phòng và tòa nhà
    internal void RefreshRoomChoices()
    {
        // Ghi nhớ tòa nhà đang lọc; sau khi sửa phòng sẽ tìm lại lựa chọn đó nếu còn tồn tại.
        var selectedBuilding = filter.SelectedValue?.ToString();
        ready = false;
        ConfigureTimetableSubjects();
        if (tabs.SelectIndex == 1)
        {
            ConfigureSelect(filter, new[] { "Tất cả tòa nhà" }.Concat(Store.Rooms.Select(r => r.Building).Distinct()));
            filter.SelectedIndex = Math.Max(0, filter.Items.ToList().FindIndex(x => x.ToString() == selectedBuilding));
        }
        ready = true;
    }
    // Ghi nhớ trạng thái trang lớp và phòng
    internal SchedulingViewState CaptureView() => new(tabs.SelectIndex, semester.SelectedIndex, search.Text,
        filter.SelectedValue?.ToString(), scopeKind == TimetableScopeKind.Room ? timetableSubjectId : null,
        monday, selectedClassId, selectedRoomId, new Point(-content.AutoScrollPosition.X, -content.AutoScrollPosition.Y),
        scopeKind == TimetableScopeKind.Lecturer ? timetableSubjectId : null, scopeKind, timetableSubjectId);
    // Khôi phục trạng thái trang lớp và phòng
    internal void RestoreView(SchedulingViewState state)
    {
        SwitchTab(state.Tab);
        // Phục hồi toàn bộ bộ lọc trước khi nạp lại một lần, tránh chuỗi sự kiện nạp trung gian.
        ready = false;
        semester.SelectedIndex = state.Semester; search.Text = state.Query;
        filter.SelectedIndex = Math.Max(0, filter.Items.ToList().FindIndex(x => x.ToString() == state.Filter));
        scopeKind = state.Scope; timetableScope.SelectedIndex = (int)scopeKind;
        timetableSubjectId = state.SubjectId;
        monday = state.Week; selectedClassId = state.ClassId; selectedRoomId = state.RoomId;
        ready = true; RefreshData(false); content.AutoScrollPosition = state.Scroll;
        if (UsesStudentDirectory) _ = EnsureTimetableDirectoryAsync();
    }
    // Điểm đọc trạng thái phục vụ kiểm chứng, không tạo thêm lựa chọn phòng trên giao diện.
    internal bool RoomChoiceExistsForCheck(string id) => Store.Rooms.Any(r => ScheduleRules.Same(r.Id, id));
    // Xóa phòng học
    private void DeleteRoom(Room room)
    {
        if (!ConfirmDelete("Xóa phòng học?", $"Xóa phòng {room.Id} khỏi danh sách?")) return;
        if (!Store.TryDeleteRoom(room.Id, out var error)) { AntdUI.Message.warn(owner, error!); return; }
        RefreshRoomChoices(); RefreshData();
    }
    // Các điểm kiểm chứng bên dưới thao tác trực tiếp bộ lọc và đọc bố cục để kiểm tra form tự động.
    internal void SetSearchForCheck(string query) { search.Text = query; debounce.Stop(); RefreshData(false); }
    internal void SetRoomFilterForCheck(string id) => _ = SetScopeForCheckAsync(TimetableScopeKind.Room, string.IsNullOrWhiteSpace(id) ? null : id);
    internal void SetLecturerFilterForCheck(string id) => _ = SetScopeForCheckAsync(TimetableScopeKind.Lecturer, string.IsNullOrWhiteSpace(id) ? null : id);
    internal IReadOnlyList<ScheduledOccurrence> VisibleCalendarSessionsForCheck => calendar.PlacementsForCheck.Select(p => p.Occurrence).ToArray();
    internal bool CalendarFiltersDoNotOverlapForCheck => !timetableScope.Bounds.IntersectsWith(timetableSubject.Bounds)
        && !timetableSubject.Bounds.IntersectsWith(reloadTimetableSubjects.Bounds)
        && !timetableScope.Bounds.IntersectsWith(currentWeek.Bounds)
        && reloadTimetableSubjects.Right <= listCard.Width && timetableSubject.Bottom <= calendar.Top;
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

// Ảnh chụp trạng thái hiển thị, dùng khôi phục tab, bộ lọc, dòng chọn và vị trí cuộn khi nạp lại trang.
internal sealed record SchedulingViewState(int Tab, int Semester, string Query, string? Filter, string? RoomFilter,
    DateOnly Week, string? ClassId, string? RoomId, Point Scroll, string? LecturerFilter = null,
    TimetableScopeKind Scope = TimetableScopeKind.Lecturer, string? SubjectId = null);
