using AntdUI;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using Panel = AntdUI.Panel;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed partial class SchedulingPage : UserControl
{
    private readonly MainForm owner;
    internal ScheduleStore Store { get; }
    internal void UseShellNavigation() { breadcrumb.Visible = teachers.Visible = false; }
    private readonly SchedulingVisualCatalog visuals = new();
    private readonly Panel navigation = new() { Radius = 0, Back = SchedulingTheme.Canvas, BackColor = SchedulingTheme.Canvas };
    private readonly System.Windows.Forms.Panel content = new() { AutoScroll = true, BackColor = SchedulingTheme.Canvas };
    private readonly AntdUI.Label breadcrumb = AppTheme.Label("Không gian đào tạo", 8, color: AppTheme.Muted);
    private readonly AntdUI.Label pageTitle = AppTheme.Label("Lớp & phòng học", 18, true);
    private readonly AntdUI.Button teachers = SchedulingTheme.Button("Giảng viên", "ArrowLeftOutlined");
    private readonly Select semester = new();
    private readonly SchedulingStat[] stats = [new("Tổng lớp học phần", "book-open", SchedulingTheme.Cyan), new("Phòng học", "door-open", SchedulingTheme.Lavender), new("Đã xếp lịch", "calendar-days", SchedulingTheme.Amber), new("Tỷ lệ đăng ký", "users-round", SchedulingTheme.Rose)];
    private readonly Panel statsCard = SchedulingTheme.Surface(18), analyticsCard = SchedulingTheme.Surface(18), weeklyCard = SchedulingTheme.Surface(18);
    private readonly ScheduleAnalytics analytics = new();
    private readonly AntdUI.Label weeklyTitle = SchedulingTheme.Label("Lớp trong tuần", 10, true);
    private readonly AntdUI.Label weekCaption = SchedulingTheme.Label("", 8, color: SchedulingTheme.Muted);
    private readonly SchedulingPreview[] previews = Enumerable.Range(0, 3).Select(_ => new SchedulingPreview()).ToArray();
    private readonly Panel listCard = SchedulingTheme.Surface();
    private readonly AntdUI.Label listTitle = AppTheme.Label("Danh sách lớp", 12, true);
    private readonly SchedulingTabs tabs = new();
    private readonly Input search = new();
    private readonly Select filter = new();
    private readonly AntdUI.Button add = SchedulingTheme.Button("Mở lớp", "PlusOutlined", true);
    private readonly AntdUI.Button previousWeek = SchedulingTheme.Button("", "LeftOutlined"), nextWeek = SchedulingTheme.Button("", "RightOutlined");
    private readonly AntdUI.Button currentWeek = SchedulingTheme.Button("Tuần này", "CalendarOutlined");
    private readonly AntdUI.Label weekLabel = SchedulingTheme.Label("", 9, true);
    private readonly Table table = new();
    private readonly WeeklySchedule calendar = new();
    private readonly SchedulingDetail detail = new();
    private readonly AntdUI.Label empty = SchedulingTheme.Label("Không có kết quả phù hợp.\nThử từ khóa khác hoặc đặt lại bộ lọc.", 11, color: SchedulingTheme.Muted);
    private readonly SchedulingArtwork emptyArtwork = new("books-pastel.png");
    private readonly AntdUI.Button reset = SchedulingTheme.Button("Đặt lại bộ lọc", "ReloadOutlined");
    private readonly System.Windows.Forms.Timer debounce = new() { Interval = 180 };
    private readonly ToolTip tooltips = new();
    private readonly List<Font> ownedFonts = [];
    private bool ready;
    private string? selectedClassId, selectedRoomId;
    private DateOnly monday;
    internal int ResultCount { get; private set; }
    internal int TabIndexForCheck => tabs.SelectIndex;
    internal DateOnly WeekForCheck => monday;
    internal int CalendarCountForCheck => calendar.OccurrenceCount;

    internal SchedulingPage(MainForm owner, ScheduleStore store, TimetableDirectory? directory = null,
        Func<CancellationToken, Task<TimetableDirectory>>? directoryLoader = null, Func<Task>? reloadTimetable = null)
    {
        this.owner = owner;
        Store = store;
        timetableDirectory = directory ?? new([], []);
        timetableDirectoryReady = directory != null;
        loadTimetableDirectory = directoryLoader;
        reloadTimetableSnapshot = reloadTimetable;
        BackColor = SchedulingTheme.Canvas; AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var initial = Store.Catalog.Semesters.FirstOrDefault(s => s.StartDate <= today && s.EndDate >= today) ?? Store.Catalog.Semesters[0];
        monday = SchedulingQuery.WeekStart(today >= initial.StartDate && today <= initial.EndDate ? today : initial.StartDate);
        detail.Visuals = visuals; calendar.Visuals = visuals;
        BuildChrome(); BuildWorkspace(); Connect();
        semester.SelectedIndex = Store.Catalog.Semesters.ToList().IndexOf(initial);
        selectedClassId = Store.Classes.FirstOrDefault(c => c.SemesterId == initial.Id)?.Id;
        selectedRoomId = Store.Rooms.FirstOrDefault()?.Id;
        CollectFonts(this);
        ready = true; RefreshData(false); LayoutPage();
    }

    private void BuildChrome()
    {
        breadcrumb.TextMultiLine = pageTitle.TextMultiLine = weeklyTitle.TextMultiLine = listTitle.TextMultiLine = false;
        weekCaption.TextMultiLine = weekLabel.TextMultiLine = false;
        tooltips.SetToolTip(teachers, "Quay lại Giảng viên"); teachers.AccessibleName = "Quay lại Giảng viên";
        ConfigureSelect(semester, Store.Catalog.Semesters.Select(s => s.Name));
        semester.AccessibleName = "Chọn học kỳ";
        analyticsCard.Controls.Add(analytics); analytics.Dock = DockStyle.Fill; analyticsCard.Padding = new(AppTheme.S(8));
        statsCard.Back = statsCard.BackColor = SchedulingTheme.Canvas;
        statsCard.Radius = 0;
        weeklyCard.Back = Color.White;
        weeklyCard.Controls.AddRange([weeklyTitle, weekCaption]);
        var previewTones = new[] { SchedulingTheme.Cyan, SchedulingTheme.Lavender, SchedulingTheme.Amber };
        for (int i = 0; i < previews.Length; i++) { previews[i].SetTone(previewTones[i]); weeklyCard.Controls.Add(previews[i]); }
        navigation.Controls.AddRange([breadcrumb, pageTitle, teachers, semester, add]);
        content.Controls.AddRange([statsCard, analyticsCard, weeklyCard, listCard, detail]);
        statsCard.Controls.AddRange(stats);
        stats[0].SetAction("Xem lớp", () => SwitchTab(0));
        stats[1].SetAction("Xem phòng", () => SwitchTab(1));
        stats[2].SetAction("Xem lịch", () => SwitchTab(2));
        stats[3].SetAction("Xem sĩ số", () => SwitchTab(0));
        stats[^1].ShowSeparator = false;
        Controls.AddRange([navigation, content]);
    }

    private void BuildWorkspace()
    {
        tabs.SelectIndex = 0;
        tabs.AccessibleName = "Chọn Lớp học phần, Phòng học hoặc Lịch tuần";
        SchedulingTheme.ConfigureInput(search); search.PrefixSvg = "SearchOutlined"; search.AllowClear = true;
        search.PlaceholderText = "Tìm mã lớp, học phần, giảng viên…";
        search.BackColor = AppTheme.Canvas; search.BorderWidth = 0; search.AccessibleName = "Tìm lớp hoặc phòng";
        ConfigureSelect(filter, ["Tất cả trạng thái", SchedulingLabels.Open, SchedulingLabels.Closed, SchedulingLabels.Canceled]);
        BuildTimetableSelectors();
        filter.AccessibleName = "Lọc theo trạng thái hoặc tòa nhà";
        table.BackColor = Color.White; table.ForeColor = SchedulingTheme.Ink; table.Font = AppTheme.Font(9);
        table.ColumnFont = AppTheme.Font(8.5f); ownedFonts.Add(table.ColumnFont);
        table.ColumnBack = Color.White; table.ColumnFore = AppTheme.Muted;
        table.Gap = 8; table.RowHeight = 56; table.RowHeightHeader = 40; table.Radius = 12;
        table.BorderWidth = .6f; table.BorderCellWidth = 0; table.BorderColor = Color.FromArgb(110, AppTheme.Line);
        table.RowHoverBg = AppTheme.Canvas; table.RowSelectedBg = AppTheme.AccentSoft;
        table.AccessibleName = "Bảng lớp học phần hoặc phòng học";
        empty.TextMultiLine = true; empty.TextAlign = ContentAlignment.MiddleCenter;
        listCard.Controls.AddRange([listTitle, tabs, search, filter, timetableScope, timetableSubject, reloadTimetableSubjects,
            table, calendar, emptyArtwork, empty, reset, previousWeek, nextWeek, currentWeek, weekLabel]);
        ConfigureTable();
    }

    private static void ConfigureSelect(Select select, IEnumerable<string> values)
    {
        select.SelectedIndex = -1;
        select.Items.Clear(); select.Items.AddRange(values.Cast<object>().ToArray());
        select.SelectedIndex = select.Items.Count == 0 ? -1 : 0;
        if (select.Tag is not System.Drawing.Font)
        {
            select.Font = AppTheme.Font(9); select.Tag = select.Font;
        }
        select.Radius = 10; select.BorderColor = SchedulingTheme.Line;
        select.BorderActive = select.BorderHover = SchedulingTheme.Primary;
        select.ForeColor = SchedulingTheme.Ink; select.DropDownRadius = 12; select.ListAutoWidth = true;
    }

    private void Connect()
    {
        Resize += (_, _) => { SurfaceTransition.CancelAll(); LayoutPage(); };
        content.Scroll += (_, _) => SurfaceTransition.CancelAll();
        teachers.Click += (_, _) => owner.ShowLecturers();
        tabs.SelectIndexChanged += (_, _) => { if (!ready) return; ChangeTab(); };
        search.TextChanged += (_, _) => { if (!ready) return; debounce.Stop(); debounce.Start(); };
        debounce.Tick += (_, _) => { debounce.Stop(); RefreshData(); };
        filter.SelectedIndexChanged += (_, _) => { if (ready) RefreshData(); };
        semester.SelectedIndexChanged += (_, _) => { if (!ready) return; ResetWeek(); RefreshData(); };
        reset.Click += (_, _) => ResetFilters();
        add.Click += (_, _) => { if (tabs.SelectIndex == 1) EditRoom(null); else EditClass(null); };
        previousWeek.Click += (_, _) => MoveWeek(-7); nextWeek.Click += (_, _) => MoveWeek(7); currentWeek.Click += (_, _) => { ResetWeek(); RefreshData(); };
        detail.EditClass += EditClass; detail.EditRoom += EditRoom; detail.RoomSelected += SelectRoom;
        detail.RoomScheduleRequested += ShowRoomSchedule; calendar.ClassSelected += SelectClass;
        foreach (var b in previews) b.Click += (_, _) => { if (b.Tag is CourseClass item) { SwitchTab(0); SelectClass(item); } };
        table.CellClick += (_, e) => {
            if (e.Column?.Key == "Actions") return;
            if (e.Record is CourseClassRow row) SelectClass(row.Model);
            else if (e.Record is RoomRow room) SelectRoom(room.Model.Id, false);
        };
        table.CellButtonClick += (_, e) => {
            if (e.Record is CourseClassRow row) { if (e.Btn.Id == "edit") EditClass(row.Model); else DeleteClass(row.Model); }
            else if (e.Record is RoomRow room) { if (e.Btn.Id == "edit") EditRoom(room.Model); else DeleteRoom(room.Model); }
        };
        VisibleChanged += (_, _) => { if (!Visible) { debounce.Stop(); SurfaceTransition.CancelAll(); } };
    }

    private void CollectFonts(Control root)
    {
        if (root is not SchedulingStat and not ScheduleAnalytics and not SchedulingDetail and not WeeklySchedule and not SchedulingPreview and not SchedulingTabs)
        {
            if (root is AntdUI.Label or AntdUI.Button or System.Windows.Forms.Button or AntdUI.Select or Input or Table or Segmented) ownedFonts.Add(root.Font);
            foreach (Control child in root.Controls) CollectFonts(child);
        }
    }

    internal void Reveal()
    {
        RefreshData(false);
        foreach (var stat in stats)
        {
            // Live count-up must remain visible; a snapshot would freeze its first value.
            stat.RevealValue();
        }
        SurfaceTransition.Reveal(analyticsCard, 360, 120); SurfaceTransition.Reveal(weeklyCard, 360, 160);
        SurfaceTransition.Reveal(listCard, 360, 200); SurfaceTransition.Reveal(detail, 360, 240);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed) { timetableLifetime.Cancel(); timetableLifetime.Dispose(); debounce.Dispose(); tooltips.Dispose(); SurfaceTransition.CancelAll(); }
        base.Dispose(disposing);
        if (disposing) { visuals.Dispose(); foreach (var font in ownedFonts.Distinct<Font>(ReferenceEqualityComparer.Instance)) font.Dispose(); ownedFonts.Clear(); }
    }
}
