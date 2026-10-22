using System.Collections.Immutable;
using AntdUI;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Dialogs;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Panels;
using QuanLyGiangVien.Ui;
using QuanLyGiangVien.Features.Scheduling;
using QuanLyGiangVien.Domain.Scheduling;
using Label = AntdUI.Label;
using Panel = AntdUI.Panel;

namespace QuanLyGiangVien;

// Màn hình quản lý giảng viên: hiển thị thống kê, tìm/lọc hồ sơ và mở các thao tác thêm, sửa, xóa.
// Lớp được chia thành nhiều file partial; FormQuanLyGiangVien.Nhung.cs chứa cờ nhúng vào khung ứng dụng.
public sealed partial class MainForm : Window
{
    // Nguồn dữ liệu và trạng thái tải: dùng kho SQL khi đăng nhập thật, dùng dữ liệu mẫu khi chạy demo.
    private LecturerStore store;
    private IReadOnlyList<string> facultyNames;
    private readonly bool databaseMode;
    private readonly SqlMinhRepository? databaseRepository;
    private IReadOnlyList<DatabaseRegistration> timetableRegistrations = [];
    private bool reloadBusy;
    // Thanh tiêu đề và điều hướng tạo sẵn; trang lớp/phòng chỉ được tạo khi cần mở.
    private readonly PageHeader titlebar = new();
    private readonly Panel nav = new();
    private readonly Label brand = AppTheme.Label("campus.", 22, true);
    private readonly Label department = AppTheme.Label("PHÒNG ĐÀO TẠO", 8, true);
    private readonly AntdUI.Button navSelected = AppTheme.Button("Giảng viên", "TeamOutlined");
    private readonly AntdUI.Button schedulingNav = AppTheme.Button("Lớp & phòng học", "CalendarOutlined");
    private SchedulingPage? scheduling;
    private ScheduleStore scheduleStore;
    private SchedulingViewState? schedulingView;
    private bool schedulingVisible;
    private readonly Avatar user = new();
    private readonly Label userName = AppTheme.Label("Minh", 10, true);
    // Các vùng giao diện: thống kê phía trên, danh sách giảng viên và hồ sơ chi tiết phía dưới.
    private readonly System.Windows.Forms.Panel body = new() { AutoScroll = true, BackColor = AppTheme.Canvas };
    private readonly DashboardHero hero = new();
    private readonly DashboardOverview overview = new();
    private readonly DashboardStats stats = new();
    private readonly DashboardFaculty faculties = new();
    private readonly Panel listCard = AppTheme.Surface();
    private readonly Label title = AppTheme.Label("Danh sách giảng viên", 15, true);
    private readonly Panel toolbar = AppTheme.Surface(16);
    private readonly Input search = new();
    private readonly AnimatedSelect faculty = new();
    private readonly AnimatedSelect status = new();
    private readonly Segmented view = new();
    private readonly LecturerGrid grid = new();
    private readonly Panel tableCard = AppTheme.Surface();
    private readonly Table table = new();
    private readonly LecturerProfile profile = new();
    private readonly Label emptyTitle = AppTheme.Label("Không tìm thấy giảng viên", 16, true);
    private readonly Label emptyDescription = AppTheme.Label("Thử tên, mã giảng viên khác hoặc đặt lại bộ lọc.", 10, false, AppTheme.Muted);
    private readonly AntdUI.Button clear = AppTheme.Button("Đặt lại bộ lọc", "ReloadOutlined");
    // Chờ người dùng ngừng gõ 180 ms rồi mới lọc; giữ hồ sơ đang chọn và các tài nguyên cần giải phóng.
    private readonly System.Windows.Forms.Timer debounce = new() { Interval = 180 };
    private readonly System.Windows.Forms.ContextMenuStrip menu = new();
    private IReadOnlyList<Lecturer> filtered = [];
    private Lecturer? selected;
    private Form? profileDrawer;
    private Action? afterDrawerClose;
    private readonly List<Font> scaledFonts = [];
    private readonly PageMotionCoordinator pageMotion;
    private bool ready;
    private bool assignedOnly;
    public LecturerStore Store => store;
    public LecturerEditor? CurrentEditor { get; private set; }
    internal string? SelectedIdForCheck => selected?.Id;

    // Khởi tạo nguồn dữ liệu, dựng giao diện rồi nối các sự kiện để form sẵn sàng tương tác.
    public MainForm(bool startWithScheduling = false, DatabaseSnapshot? database = null, bool embedded = false)
    {
        // Khi được nhúng, bỏ viền cửa sổ riêng và lấp đầy vùng nội dung của ứng dụng cha.
        this.embedded = embedded;
        if (embedded) { TopLevel = false; FormBorderStyle = FormBorderStyle.None; Dock = DockStyle.Fill; }
        AutoHandDpi = false;
        AutoScaleMode = AutoScaleMode.None;
        AppTheme.SetScale(DeviceDpi);
        // Với dữ liệu SQL, kiểm tra vai trò trước khi đưa dữ liệu giảng viên/lớp học lên màn hình.
        databaseMode = database != null;
        if (database != null)
        {
            if (database.Identity.Role is not ("DAO_TAO" or "QUAN_TRI"))
                throw new PersistenceException("Chức năng quản lý chỉ dành cho phòng đào tạo hoặc quản trị.");
            var data = database.CreateStores();
            databaseRepository = database.Repository;
            timetableRegistrations = database.Registrations;
            store = data.Lecturers; scheduleStore = data.Scheduling;
            facultyNames = database.Faculties.Select(item => item.Name).ToArray();
            userName.Text = database.Identity.DisplayName;
            profile.ClassesProvider = AssignedClasses;
            faculties.SetFaculties(facultyNames);
        }
        // Không có snapshot SQL thì tạo kho mẫu trong bộ nhớ để có thể xem và thử giao diện.
        else
        {
            store = new(DemoData.Create());
            scheduleStore = SchedulingDemo.Create(() => store.Items);
            facultyNames = DemoData.Faculties;
        }
        // Thiết lập màu, font và kích thước phù hợp vùng làm việc của màn hình hiện tại.
        pageMotion = new(this);
        BackColor = AppTheme.Canvas;
        Font = AppTheme.Font();
        Text = "Campus · Quản lý giảng viên";
        StartPosition = FormStartPosition.CenterScreen;
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = embedded ? new(AppTheme.S(1440), AppTheme.S(900))
            : ClampClientSize(new(AppTheme.S(1440), AppTheme.S(900)), workArea.Size);
        ApplyMinimumSize(workArea.Size);
        // Dựng các vùng hiển thị trước, sau đó gắn sự kiện và nạp danh sách lần đầu.
        BuildShell();
        if (embedded) titlebar.Visible = nav.Visible = false;
        BuildToolbar();
        BuildTable();
        RefreshControlFonts();
        ConnectEvents();
        selected = store.Items.FirstOrDefault();
        ready = true;
        RefreshResults(false);
        LayoutScreen();
        // Đăng ký hiệu ứng nền qua bộ điều phối chuyển động chung của trang.
        pageMotion.SetAmbient((seconds, enabled) => {
            hero.ApplyAmbient(seconds, enabled);
            if (!enabled) overview.ResetHover();
        });
        // Khi form đã hiện, cập nhật lại DPI/kích thước và mở trang khởi đầu được yêu cầu.
        Shown += (_, _) => {
            AppTheme.SetScale(DeviceDpi);
            RefreshControlFonts();
            var available = Screen.FromControl(this).WorkingArea.Size;
            ApplyMinimumSize(available);
            if (!embedded) ClientSize = ClampClientSize(new(AppTheme.S(1440), AppTheme.S(900)), available);
            RefreshResults(false);
            if (startWithScheduling) ShowScheduling();
            else BeginInvoke(StartPageEntrance);
        };
        // Gắn các bộ kiểm chứng tự động; từng bộ tự quyết định có chạy theo cấu hình kiểm thử hay không.
        UiCheck.Attach(this);
        SchedulingUiCheck.Attach(this);
        WeeklyTimetableUiCheck.Attach(this);
        ScopedTimetableUiCheck.Attach(this);
        DatabaseUiCheck.Attach(this, database);
        // Bắt phím F5 ở cấp form để tải lại SQL khi đang ở chế độ cơ sở dữ liệu.
        KeyPreview = true;
        KeyDown += async (_, key) => {
            if (key.KeyCode != Keys.F5 || !databaseMode) return;
            key.Handled = true; key.SuppressKeyPress = true;
            await ReloadDatabaseAsync();
        };
    }
    // Tải lại snapshot SQL và cập nhật giảng viên/lớp/phòng mà vẫn cố giữ bộ lọc hiện tại.
    internal async Task ReloadDatabaseAsync()
    {
        // Không tải chồng hoặc thay dữ liệu khi người dùng đang mở form sửa/hồ sơ dạng ngăn kéo.
        if (databaseRepository == null || reloadBusy || CurrentEditor != null || HasOpenProfileDrawer) return;
        reloadBusy = true; Enabled = false;
        try
        {
            // Chờ dữ liệu mới mà không chặn luồng giao diện; kiểm tra lại quyền sau khi tải xong.
            var snapshot = await databaseRepository.LoadAsync();
            if (IsDisposed) return;
            if (snapshot.Identity.Role is not ("DAO_TAO" or "QUAN_TRI"))
                throw new PersistenceException("Tài khoản không còn quyền quản lý. Vui lòng đóng ứng dụng và đăng nhập lại.");
            // Nhớ bộ lọc và trạng thái trang lớp/phòng trước khi thay các kho dữ liệu trong bộ nhớ.
            var data = snapshot.CreateStores();
            var selectedFaculty = faculty.SelectedValue?.ToString();
            timetableRegistrations = snapshot.Registrations;
            CaptureSchedulingView();
            if (!scheduleStore.Catalog.Semesters.SequenceEqual(data.Scheduling.Catalog.Semesters)) schedulingView = null;
            store = data.Lecturers; scheduleStore = data.Scheduling;
            facultyNames = snapshot.Faculties.Select(item => item.Name).ToArray();
            faculties.SetFaculties(facultyNames);
            // Tạm ngăn sự kiện lọc trong lúc thay danh mục khoa rồi phục hồi lựa chọn còn hợp lệ.
            ready = false; faculty.SetItems(new[] { "Tất cả khoa" }.Concat(facultyNames));
            faculty.SelectedIndex = Math.Max(0, faculty.Items.ToList().IndexOf(selectedFaculty ?? "Tất cả khoa"));
            ready = true; userName.Text = snapshot.Identity.DisplayName;
            RefreshResults(false);
            if (schedulingVisible) ShowScheduling();
            AntdUI.Message.success(this, "Đã tải lại dữ liệu.");
        }
        // Hiển thị lỗi lưu trữ cho người dùng và luôn mở lại tương tác sau khi kết thúc tải.
        catch (PersistenceException error) { if (!IsDisposed) AntdUI.Message.error(this, error.Message); }
        finally { reloadBusy = false; if (!IsDisposed) Enabled = true; }
    }
    #region Nhóm 1 - Dựng giao diện danh sách giảng viên
    // Tạo khung trang, điều hướng và ghép các khối thống kê/danh sách/hồ sơ vào vùng nội dung.
    private void BuildShell()
    {
        // Dùng dấu cách để ẩn chữ tiêu đề nhưng vẫn giữ kích thước font cho các nút cửa sổ.
        titlebar.Text = " ";
        titlebar.Font = AppTheme.Font(8);
        titlebar.BackColor = AppTheme.Canvas;
        titlebar.ForeColor = AppTheme.Ink;
        titlebar.ShowButton = true;
        titlebar.ShowIcon = false;
        titlebar.UseSystemStyleColor = true;
        nav.Back = AppTheme.Canvas;
        nav.BackColor = AppTheme.Canvas;
        nav.Radius = 0;
        navSelected.DefaultBack = AppTheme.AccentSoft;
        navSelected.DefaultBorderColor = AppTheme.AccentSoft;
        navSelected.ForeColor = AppTheme.PrimaryPressed;
        navSelected.BorderWidth = 0;
        // Hiển thị ảnh người dùng và gắn nút chuyển sang trang quản lý lớp/phòng.
        user.Image = Portraits.Get("portrait-04");
        user.ImageFit = TFit.Cover;
        user.Round = true;
        user.BackColor = Color.White;
        schedulingNav.AccessibleName = "Mở trang Lớp học và Phòng học";
        schedulingNav.Click += (_, _) => ShowScheduling();
        nav.Controls.AddRange([brand, department, navSelected, schedulingNav, user, userName]);
        // Đưa control vào đúng vùng cha; thông báo rỗng dùng khi bộ lọc không còn kết quả.
        emptyTitle.TextAlign = emptyDescription.TextAlign = ContentAlignment.MiddleCenter;
        listCard.Controls.AddRange([title, toolbar, view, grid, tableCard,
            emptyTitle, emptyDescription, clear]);
        body.Controls.AddRange([hero, overview, stats, faculties, listCard, profile]);
        Controls.AddRange([titlebar, nav, body]);
    }
    // Tạo ô tìm kiếm, hai bộ lọc và nút chuyển giữa cách xem dạng thẻ/dạng bảng.
    private void BuildToolbar()
    {
        search.PlaceholderText = "Tìm tên, mã hoặc email…";
        search.PrefixSvg = "SearchOutlined";
        search.AllowClear = true;
        search.Radius = 10;
        search.Font = AppTheme.Font(10);
        search.BorderWidth = 0;
        search.BackColor = AppTheme.Canvas;
        search.AccessibleName = "Tìm giảng viên";
        // Nạp danh mục khoa/trạng thái; lựa chọn đầu tiên tương ứng với không giới hạn bộ lọc.
        faculty.SetItems(new[] { "Tất cả khoa" }.Concat(facultyNames));
        faculty.SelectedIndex = 0;
        status.SetItems(["Tất cả trạng thái", "Đang giảng dạy", "Tạm nghỉ"]);
        status.SelectedIndex = 0;
        foreach (var control in new[] { faculty, status })
        {
            control.Font = AppTheme.Font(9);
            control.Radius = 10;
            control.BorderColor = AppTheme.Line;
        }
        faculty.AccessibleName = "Lọc theo khoa";
        status.AccessibleName = "Lọc theo trạng thái";
        // Hai lựa chọn chỉ đổi cách trình bày, cùng dùng một tập giảng viên đã lọc.
        view.Items.Add(new SegmentedItem { IconSvg = "AppstoreOutlined", Tooltip = "Xem dạng thẻ" });
        view.Items.Add(new SegmentedItem { IconSvg = "UnorderedListOutlined", Tooltip = "Xem dạng bảng" });
        view.SelectIndex = 0;
        view.Radius = 10;
        view.Font = AppTheme.Font(9, true);
        view.Full = true;
        view.IconRatio = 1.3F;
        view.BackColor = AppTheme.Canvas;
        view.ForeActive = AppTheme.PrimaryHover;
        view.AccessibleName = "Chế độ xem danh sách giảng viên";
        view.AccessibleDescription = "Đang xem dạng thẻ";
        toolbar.Controls.AddRange([search, faculty, status]);
    }
    // Khai báo các cột bảng và vùng chứa bảng; dữ liệu từng dòng được gán ở RefreshResults.
    private void BuildTable()
    {
        table.Dock = DockStyle.Fill;
        table.BackColor = Color.White;
        table.Font = AppTheme.Font(9);
        table.Gap = 10;
        table.BorderWidth = 1;
        table.RowHoverBg = Color.Transparent;
        table.Columns = new ColumnCollection
        {
            new Column("Avatar", "") { Width = "62" },
            new Column("Name", "Họ và tên") { Width = "190" },
            new Column("Id", "Mã GV") { Width = "86" },
            new Column("Faculty", "Khoa") { Width = "160" },
            new Column("Email", "Email") { Width = "210", Ellipsis = true },
            new Column("Phone", "Điện thoại") { Width = "120" },
            new Column("Status", "Trạng thái") { Width = "144" },
            new Column("Actions", "Thao tác") { Width = "130", Align = ColumnAlign.Center }
        };
        tableCard.Padding = new(AppTheme.S(12));
        tableCard.Controls.Add(table);
    }
    #endregion

    #region Nhóm 1 - Sự kiện, tìm kiếm và cập nhật danh sách
    // Nối thao tác trên control với các hàm xử lý; ready ngăn lọc sớm khi form chưa khởi tạo xong.
    private void ConnectEvents()
    {
        // Hủy hiệu ứng cũ khi cuộn/đổi kích thước để nội dung không bị giữ ở vị trí trước đó.
        ClientSizeChanged += (_, _) => { SurfaceTransition.CancelAll(); if (ready) LayoutScreen(); };
        body.Scroll += (_, _) => SurfaceTransition.CancelAll();
        grid.Scroll += (_, _) => SurfaceTransition.CancelAll();
        // Mỗi lần gõ sẽ đếm lại 180 ms; chỉ lọc khi timer chạy để tránh cập nhật liên tục theo từng phím.
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        debounce.Tick += (_, _) => { debounce.Stop(); RefreshResults(); };
        // Lọc theo khoa
        faculty.SelectedIndexChanged += (_, _) => { if (ready) RefreshResults(); };
        // Lọc theo trạng thái
        status.SelectedIndexChanged += (_, _) => { if (ready) RefreshResults(); };
        // Chuyển dạng xem và bố trí lại danh sách, đồng thời cập nhật mô tả cho công cụ trợ năng.
        view.SelectIndexChanged += (_, _) => {
            view.AccessibleDescription = view.SelectIndex == 0 ? "Đang xem dạng thẻ" : "Đang xem dạng bảng";
            if (!ready) return;
            SurfaceTransition.CancelAll(); LayoutScreen();
            SurfaceTransition.Reveal(view.SelectIndex == 0 ? grid : tableCard, 160);
        };
        // Thêm giảng viên
        hero.AddRequested += () => Edit(null);
        // Bấm thống kê/khoa sẽ đưa người dùng xuống danh sách và áp dụng phạm vi tương ứng.
        stats.ActionRequested += index => {
            ShowDashboardList(assigned: index == 2);
            if (index == 1) { faculty.Focus(); faculty.OpenDropDown(); }
        };
        faculties.FacultyRequested += name => ShowDashboardList(name);
        // Nối nút đặt lại bộ lọc và thao tác chọn thẻ với phần hồ sơ chi tiết.
        clear.Click += (_, _) => ResetFilters();
        grid.Selected += SelectLecturer;
        // Sửa giảng viên
        grid.EditRequested += value => Edit(value);
        grid.MenuRequested += OpenMenu;
        profile.EditRequested += value => Edit(value);
        // Bấm dòng để xem hồ sơ; các nút trong cột Thao tác được chuyển riêng sang xem/sửa/xóa.
        table.CellClick += (_, e) => { if (e.Record is LecturerRow row && e.Column?.Key != "Actions") SelectLecturer(row.Model); };
        table.CellButtonClick += (_, e) =>
        {
            if (e.Record is not LecturerRow row) return;
            switch (e.Btn.Id)
            {
                case "view": SelectLecturer(row.Model); break;
                case "edit": Edit(row.Model); break;
                case "delete": Delete(row.Model); break;
            }
        };
    }
    // Tính lại kết quả tìm/lọc và đồng bộ thống kê, danh sách dạng thẻ/bảng cùng hồ sơ đang chọn.
    private void RefreshResults(bool animate = true)
    {
        SurfaceTransition.CancelAll();
        // Ở chế độ SQL, số lớp phụ trách được tính lại từ kho lớp trước khi lọc giảng viên có lớp.
        if (databaseMode) store.SynchronizeAssignments(scheduleStore.Classes);
        filtered = LecturerQuery.Filter(store.Items, search.Text, faculty.SelectedValue?.ToString() ?? "", status.SelectedValue?.ToString() ?? "");
        if (assignedOnly) filtered = Array.AsReadOnly(filtered.Where(item => item.ActiveClasses > 0).ToArray());
        title.Text = assignedOnly ? "Giảng viên có lớp" : "Danh sách giảng viên";
        // Giữ hồ sơ đang chọn nếu vẫn thuộc kết quả; nếu không thì chọn dòng đầu, hoặc null khi rỗng.
        selected = filtered.FirstOrDefault(item => item.Id == selected?.Id) ?? filtered.FirstOrDefault();
        overview.Refresh(store.Items);
        stats.Refresh(store.Items);
        faculties.Refresh(store.Items);
        // Hai cách xem nhận cùng dữ liệu; bảng chuyển mỗi hồ sơ thành LecturerRow để tạo nội dung cột.
        grid.ShowItems(filtered, selected?.Id, animate && IsHandleCreated && Visible);
        table.DataSource = filtered.Select(item => new LecturerRow(item)).ToArray();
        profile.ShowLecturer(selected, animate);
        LayoutScreen();
        if (animate && tableCard.Visible) SurfaceTransition.Reveal(tableCard, 160);
    }
    #endregion

    #region Nhóm 1 - Bố cục và hiệu ứng hiển thị
    // Chạy hiệu ứng xuất hiện của các khối khi mở trang; chuyển cho trang lớp/phòng nếu trang đó đang hiện.
    internal void StartPageEntrance()
    {
        if (IsDisposed || !Visible) return;
        if (schedulingVisible) { scheduling?.Reveal(); return; }
        SurfaceTransition.CancelAll();
        stats.AnimateFromZero(480);
        overview.AnimateFromZero(470);
        if (!embedded) SurfaceTransition.Reveal(nav, 320);
        SurfaceTransition.Reveal(hero, 360, 60);
        SurfaceTransition.Reveal(overview, 360, 100);
        foreach (var card in stats.Controls.OfType<DashboardStatCard>().ToArray()) AuthCardMotion.Reveal(card, 120);
        AuthCardMotion.Reveal(faculties, 120);
        foreach (var control in new Control[] { title, toolbar, view }) SurfaceTransition.Reveal(control, 320, 220);
        if (grid.Visible) grid.RevealCards();
        else SurfaceTransition.Reveal(tableCard, 320, 260);
        SurfaceTransition.Reveal(profile, 360, 300);
    }
    // Chọn bố cục lớp/phòng hoặc giảng viên theo trang hiện tại, tính kích thước từ vùng khách của form.
    private void LayoutScreen()
    {
        if (schedulingVisible && scheduling != null)
        {
            if (embedded) { scheduling.SetBounds(0, 0, ClientSize.Width, ClientSize.Height); return; }
            titlebar.SetBounds(0, 0, ClientSize.Width, AppTheme.S(36));
            scheduling.SetBounds(0, AppTheme.S(36), ClientSize.Width, Math.Max(1, ClientSize.Height - AppTheme.S(36)));
            return;
        }
        // Gom các thay đổi vị trí vào một lượt bố trí để giảm vẽ lại khi thay đổi kích thước cửa sổ.
        SuspendLayout();
        int pad = AppTheme.S(32), width = ClientSize.Width - pad * 2;
        LayoutNavigation(pad, width);
        body.SetBounds(pad, AppTheme.S(embedded ? 20 : 120), width,
            Math.Max(AppTheme.S(100), ClientSize.Height - AppTheme.S(embedded ? 44 : 144)));
        LayoutDashboard();
        grid.Relayout();
        ResumeLayout(true);
    }
    // Bố trí thanh tiêu đề, thương hiệu, nút chuyển trang và người dùng khi form chạy độc lập.
    private void LayoutNavigation(int pad, int width)
    {
        if (embedded) return;
        titlebar.SetBounds(0, 0, ClientSize.Width, AppTheme.S(36));
        nav.SetBounds(pad, AppTheme.S(40), width, AppTheme.S(64));
        brand.SetBounds(0, AppTheme.S(1), AppTheme.S(170), AppTheme.S(40));
        department.SetBounds(AppTheme.S(2), AppTheme.S(41), AppTheme.S(185), AppTheme.S(18));
        navSelected.SetBounds((width - AppTheme.S(352)) / 2, AppTheme.S(9), AppTheme.S(160), AppTheme.S(42));
        schedulingNav.SetBounds((width - AppTheme.S(352)) / 2 + AppTheme.S(172), AppTheme.S(9), AppTheme.S(180), AppTheme.S(42));
        userName.SetBounds(width - AppTheme.S(68), AppTheme.S(15), AppTheme.S(68), AppTheme.S(30));
        user.SetBounds(width - AppTheme.S(118), AppTheme.S(8), AppTheme.S(44), AppTheme.S(44));
    }
    // Chia vùng thống kê và danh sách thành hai cột; bổ sung thanh cuộn dọc khi nội dung vượt chiều cao.
    private void LayoutDashboard()
    {
        body.SuspendLayout();
        int heroHeight = AppTheme.S(body.Width < AppTheme.S(1200) ? 190 : 210);
        int statsY = heroHeight + AppTheme.S(16), statsHeight = AppTheme.S(108);
        int listY = statsY + statsHeight + AppTheme.S(20);
        int listHeight = Math.Max(AppTheme.S(390), body.Height - listY);
        int contentHeight = listY + listHeight;
        // Trừ bề rộng thanh cuộn và giới hạn cột bên phải để danh sách còn đủ chỗ hiển thị.
        int scrollbar = contentHeight > body.Height ? SystemInformation.GetVerticalScrollBarWidthForDpi(body.DeviceDpi) : 0;
        int width = body.Width - scrollbar, gap = AppTheme.S(20);
        int side = Math.Clamp((width - gap) / 3, AppTheme.S(330), AppTheme.S(450));
        int left = width - gap - side;
        body.AutoScrollMinSize = new(0, contentHeight);
        // Cộng độ lệch cuộn khi đặt vị trí để các khối luôn di chuyển cùng vùng nội dung.
        var offset = body.AutoScrollPosition;
        hero.SetBounds(offset.X, offset.Y, left, heroHeight);
        overview.SetBounds(offset.X + left + gap, offset.Y, side, heroHeight);
        stats.SetBounds(offset.X, offset.Y + statsY, left, statsHeight);
        faculties.SetBounds(offset.X + left + gap, offset.Y + statsY, side, statsHeight);
        // Chỉ hiện hồ sơ bên cạnh ở chế độ thẻ khi đủ rộng; các trường hợp khác dành chỗ cho danh sách.
        bool showSide = width >= AppTheme.S(1000) && filtered.Count > 0 && view.SelectIndex == 0;
        profile.Visible = showSide;
        profile.SetBounds(offset.X + left + gap, offset.Y + listY, side, listHeight);
        listCard.SetBounds(offset.X, offset.Y + listY, showSide ? left : width, listHeight);
        LayoutList();
        body.ResumeLayout(true);
    }
    // Đặt tiêu đề, nút đổi dạng xem và chia chiều ngang thanh công cụ cho tìm kiếm/khoa/trạng thái.
    private void LayoutList()
    {
        int pad = AppTheme.S(20), width = listCard.Width - pad * 2;
        title.SetBounds(pad, AppTheme.S(14), AppTheme.S(254), AppTheme.S(36));
        view.SetBounds(listCard.Width - pad - AppTheme.S(88), AppTheme.S(16), AppTheme.S(88), AppTheme.S(36));
        toolbar.SetBounds(pad, AppTheme.S(60), width, AppTheme.S(42));
        int facultyWidth = AppTheme.S(180), statusWidth = AppTheme.S(162), gap = AppTheme.S(10);
        int searchWidth = Math.Max(AppTheme.S(100), width - facultyWidth - statusWidth - gap * 2);
        search.SetBounds(0, 0, searchWidth, AppTheme.S(42));
        faculty.SetBounds(searchWidth + gap, 0, facultyWidth, AppTheme.S(42));
        status.SetBounds(searchWidth + facultyWidth + gap * 2, 0, statusWidth, AppTheme.S(42));
        LayoutResults(pad, width, AppTheme.S(114));
    }
    // Bố trí vùng kết quả, ẩn bớt cột khi hẹp và hiện hướng dẫn đặt lại bộ lọc nếu không có giảng viên.
    private void LayoutResults(int pad, int width, int y)
    {
        int height = Math.Max(AppTheme.S(160), listCard.Height - y - AppTheme.S(12));
        grid.SetBounds(pad, y, width, height);
        tableCard.SetBounds(pad, y, width, height);
        table.Columns[3].Visible = width >= AppTheme.S(1180);
        table.Columns[5].Visible = width >= AppTheme.S(1080);
        table.Columns[1].SetWidthFill();
        table.Columns[4].Visible = width >= AppTheme.S(900);
        grid.Visible = view.SelectIndex == 0 && filtered.Count > 0;
        tableCard.Visible = view.SelectIndex == 1 && filtered.Count > 0;
        emptyTitle.Visible = emptyDescription.Visible = clear.Visible = filtered.Count == 0;
        emptyTitle.SetBounds(pad, y + AppTheme.S(16), width, AppTheme.S(40));
        emptyDescription.SetBounds(pad, y + AppTheme.S(62), width, AppTheme.S(28));
        clear.SetBounds((listCard.Width - AppTheme.S(176)) / 2, y + AppTheme.S(110), AppTheme.S(176), AppTheme.S(42));
    }
    #endregion

    #region Nhóm 1 - Xem hồ sơ, mở form nhóm 2 và yêu cầu lưu/xóa qua nhóm 3
    // Chọn giảng viên từ kho hiện hành; mở hồ sơ bên cạnh hoặc ngăn kéo nếu cột chi tiết đang bị ẩn.
    private void SelectLecturer(Lecturer item)
    {
        item = store.Items.FirstOrDefault(value => value.Id == item.Id) ?? item;
        selected = item;
        grid.SelectId(item.Id);
        profile.ShowLecturer(item);
        if (!profile.Visible)
        {
            if (profileDrawer is { IsDisposed: false }) return;
            // Ngăn kéo tự co giãn khung ngoài; nội dung hồ sơ đã xử lý DPI nên dùng kích thước cơ sở ở đây.
            var detail = new LecturerProfile { Size = new(350, 580), Tag = new Size(350, 580) };
            if (databaseMode) detail.ClassesProvider = AssignedClasses;
            detail.ShowLecturer(item);
            detail.EditRequested += value => Edit(value);
            profileDrawer = Drawer.open(new Drawer.Config(this, detail) {
                Align = TAlignMini.Right, Mask = true, MaskClosable = true, Dispose = true,
                // Sau khi đóng ngăn kéo mới tiếp tục thao tác sửa để tránh hai cửa sổ chi tiết chồng nhau.
                OnClose = () => {
                    profileDrawer = null;
                    var continuation = afterDrawerClose;
                    afterDrawerClose = null;
                    if (continuation != null && !IsDisposed && !Disposing && IsHandleCreated)
                        BeginInvoke(continuation);
                }
            });
            if (profileDrawer is null) detail.Dispose();
        }
    }
    // Tạo menu ngữ cảnh cho đúng giảng viên vừa bấm: xem hồ sơ, chỉnh sửa hoặc yêu cầu xóa.
    private void OpenMenu(Lecturer lecturer, Control owner)
    {
        menu.Items.Clear();
        menu.Font = AppTheme.Font(10);
        menu.Items.Add("Xem hồ sơ", null, (_, _) => SelectLecturer(lecturer));
        menu.Items.Add("Chỉnh sửa", null, (_, _) => Edit(lecturer));
        var delete = menu.Items.Add("Xóa giảng viên", null, (_, _) => Delete(lecturer));
        delete.ForeColor = AppTheme.Danger;
        menu.Show(owner, new Point(Math.Max(0, owner.Width - AppTheme.S(170)), AppTheme.S(44)));
    }
    // Mở form nhập liệu: item = null là thêm mới, có item là chỉnh sửa hồ sơ hiện có.
    private void Edit(Lecturer? item)
    {
        // Nếu ngăn kéo đang mở thì đóng trước, rồi tìm lại hồ sơ mới nhất và gọi lại thao tác sửa.
        if (profileDrawer is { IsDisposed: false })
        {
            var id = item?.Id;
            afterDrawerClose = () => {
                var latest = id == null ? null : store.Items.FirstOrDefault(value => value.Id == id);
                if (id != null && latest == null) { AntdUI.Message.warn(this, "Hồ sơ này không còn trong danh sách."); return; }
                Edit(latest);
            };
            profileDrawer.Close();
            return;
        }
        if (item != null)
        {
            item = store.Items.FirstOrDefault(value => value.Id == item.Id);
            if (item == null) { AntdUI.Message.warn(this, "Hồ sơ này không còn trong danh sách."); return; }
        }
        // Form nhập liệu gọi quy tắc phân công trước khi lưu để bảo vệ giảng viên đang phụ trách lớp.
        using var editor = new LecturerEditor(store, item,
            (value, isNew) => SchedulingLecturerGuard.ValidationError(scheduleStore, value, isNew), facultyNames);
        CurrentEditor = editor;
        // Chỉ cập nhật lựa chọn/danh sách khi form trả OK, tức thao tác lưu đã thành công.
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            selected = editor.Result;
            RefreshResults();
            if (selected != null) AntdUI.Message.success(this, $"Đã lưu hồ sơ {selected.Name}.");
        }
        CurrentEditor = null;
    }
    // Kiểm tra ràng buộc lớp, xin xác nhận xóa rồi gọi kho dữ liệu và làm mới danh sách.
    private void Delete(Lecturer item)
    {
        // Không cho xóa giảng viên còn liên kết lớp hoặc vẫn có số lớp phụ trách lớn hơn 0.
        var assignmentError = SchedulingLecturerGuard.DeleteError(scheduleStore, item.Id);
        if (assignmentError != null) { AntdUI.Message.warn(this, assignmentError); return; }
        if (item.ActiveClasses > 0)
        {
            AntdUI.Message.warn(this, "Giảng viên đang được phân công lớp. Hãy chuyển phân công trước khi xóa.");
            return;
        }
        // Hộp xác nhận hiển thị đúng tên/mã cần xóa; người dùng hủy thì dừng trước khi gọi TryDelete.
        var config = new Modal.Config(this, "Xóa giảng viên?", $"Xóa hồ sơ {item.Name} ({item.Id}) khỏi danh sách?", TType.Warn)
        { Width = 440, OkText = "Xóa hồ sơ", CancelText = "Hủy", OkType = TTypeMini.Error, CloseIcon = true, MaskClosable = false };
        if (AntdUI.Modal.open(config) != DialogResult.OK) return;
        if (!store.TryDelete(item.Id, out var error)) { AntdUI.Message.error(this, error!); return; }
        RefreshResults();
        AntdUI.Message.success(this, "Đã xóa hồ sơ giảng viên.");
    }
    // Xóa từ khóa và mọi giới hạn tìm kiếm trong một lượt, tránh phát sinh nhiều lần lọc trung gian.
    private void ResetFilters()
    {
        assignedOnly = false;
        ready = false;
        search.Text = "";
        faculty.SelectedIndex = status.SelectedIndex = 0;
        ready = true;
        debounce.Stop();
        RefreshResults();
    }
    // Áp dụng bộ lọc từ thẻ thống kê/khoa, cuộn tới thanh công cụ và đưa con trỏ vào ô tìm kiếm.
    private void ShowDashboardList(string? selectedFaculty = null, bool assigned = false)
    {
        ready = false;
        assignedOnly = assigned;
        search.Text = ""; status.SelectedIndex = 0;
        faculty.CloseDropDown(true);
        faculty.SelectedIndex = selectedFaculty is null ? 0 : faculty.Items.ToList().IndexOf(selectedFaculty);
        ready = true; debounce.Stop();
        RefreshResults();
        body.ScrollControlIntoView(toolbar);
        search.Focus();
    }
    #endregion

    // Khi chuyển màn hình có DPI khác, cập nhật font/kích thước và dựng lại trang lớp/phòng nếu đang mở.
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        SurfaceTransition.CancelAll();
        if (profileDrawer is { IsDisposed: false }) profileDrawer.Close();
        base.OnDpiChanged(e);
        // Nhớ phạm vi/lựa chọn lịch trước khi giải phóng trang cũ để dùng lại khi dựng trang mới.
        CaptureSchedulingView();
        AppTheme.SetScale(e.DeviceDpiNew);
        RefreshControlFonts();
        var workArea = Screen.FromRectangle(e.SuggestedRectangle).WorkingArea;
        ApplyMinimumSize(workArea.Size);
        // Giới hạn kích thước và tọa độ để cửa sổ độc lập không nằm ngoài vùng làm việc của màn hình mới.
        if (!embedded && WindowState == FormWindowState.Normal)
        {
            ClientSize = ClampClientSize(ClientSize, workArea.Size);
            Location = new(Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width)),
                Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height)));
        }
        if (schedulingVisible)
        {
            scheduling = CreateSchedulingPage();
            Controls.Add(scheduling); scheduling.BringToFront(); titlebar.BringToFront();
            LayoutScreen();
        }
        else if (ready) { RefreshResults(); Invalidate(true); }
    }
    // Giới hạn phần nội dung theo màn hình, có chừa chỗ cho viền và thanh tiêu đề hệ thống.
    private static Size ClampClientSize(Size requested, Size available) => new(
        Math.Max(1, Math.Min(requested.Width, available.Width - SystemInformation.FrameBorderSize.Width * 2)),
        Math.Max(1, Math.Min(requested.Height, available.Height - SystemInformation.CaptionHeight - SystemInformation.FrameBorderSize.Height * 2)));

    // Đặt kích thước nhỏ nhất của cửa sổ độc lập nhưng vẫn bảo đảm vừa màn hình hiện tại.
    private void ApplyMinimumSize(Size available)
    {
        if (!embedded) MinimumSize = ClampClientSize(new(AppTheme.S(1100), AppTheme.S(720)), available);
    }

    // Cấp lại font theo tỷ lệ hiện hành và giải phóng các font đã tạo ở lần cập nhật trước.
    private void RefreshControlFonts()
    {
        var previous = scaledFonts.ToArray();
        scaledFonts.Clear();
        scaledFonts.AddRange(AppTheme.RefreshFonts(this));
        foreach (var font in previous) font.Dispose();
    }
    // Khi đóng form, dừng hiệu ứng/timer, đóng ngăn kéo và giải phóng menu cùng font do form sở hữu.
    protected override void Dispose(bool disposing)
    {
        if (disposing) { SurfaceTransition.CancelAll(); pageMotion?.Dispose(); afterDrawerClose = null; profileDrawer?.Dispose(); debounce.Dispose(); menu.Dispose(); }
        base.Dispose(disposing);
        if (disposing) { foreach (var font in scaledFonts) font.Dispose(); scaledFonts.Clear(); }
    }
    // Các điểm truy cập dành cho kiểm chứng tự động: đặt bộ lọc, đổi dạng xem và kiểm tra bố cục/hồ sơ.
    public void SetFiltersForCheck(string query, string selectedFaculty = "Tất cả khoa", string selectedStatus = "Tất cả trạng thái")
    {
        ready = false;
        search.Text = query;
        faculty.SelectedIndex = Math.Max(0, faculty.Items.ToList().IndexOf(selectedFaculty));
        status.SelectedIndex = Math.Max(0, status.Items.ToList().IndexOf(selectedStatus));
        ready = true;
        debounce.Stop();
        RefreshResults();
    }
    public int ResultCount => filtered.Count;
    public void SetTableForCheck(bool value) { view.SelectIndex = value ? 1 : 0; LayoutScreen(); }
    public void ShowProfileForCheck(int index) { selected = filtered[index]; grid.SelectId(selected.Id); profile.ShowLecturer(selected); }
    public void OpenProfileForCheck(int index) => SelectLecturer(filtered[index]);
    public bool HasOpenProfileDrawer => profileDrawer is { IsDisposed: false };
    public void CloseProfileForCheck() => profileDrawer?.Close();
    public bool DashboardScrollsForCheck => body.VerticalScroll.Visible;
    public void ScrollDashboardForCheck() => body.AutoScrollPosition = new(0, body.VerticalScroll.Maximum);
    public bool HasHorizontalScrollForCheck => body.HorizontalScroll.Visible || grid.HorizontalScroll.Visible;
    public bool DashboardAlignedForCheck => hero.Top == body.AutoScrollPosition.Y;
    public bool DashboardViewportFitsForCheck => body.Bottom <= ClientSize.Height - AppTheme.S(24);
    public static Size ClampWindowForCheck(Size requested, Size available) => ClampClientSize(requested, available);

    // Chuyển sang trang lớp/phòng, dùng lại trang đã tạo và thay màu/tiêu đề/hiệu ứng phù hợp.
    internal SchedulingPage ShowScheduling()
    {
        SurfaceTransition.CancelAll();
        scheduling ??= CreateSchedulingPage();
        if (scheduling.Parent == null) Controls.Add(scheduling);
        schedulingVisible = true; nav.Visible = body.Visible = false;
        BackColor = titlebar.BackColor = SchedulingTheme.Canvas;
        titlebar.ForeColor = SchedulingTheme.Ink;
        Text = "Campus · Lớp học & Phòng học";
        scheduling.Visible = true; scheduling.BringToFront(); titlebar.BringToFront();
        pageMotion.SetAmbient(null); LayoutScreen();
        if (Visible) scheduling.Reveal();
        return scheduling;
    }
    // Tạo trang lớp/phòng với kho dữ liệu dùng chung, nguồn danh sách xem lịch và trạng thái đã ghi nhớ.
    private SchedulingPage CreateSchedulingPage()
    {
        var page = new SchedulingPage(this, scheduleStore, directoryLoader: LoadTimetableDirectoryAsync,
            reloadTimetable: databaseRepository != null ? ReloadDatabaseAsync : null);
        if (embedded) page.UseShellNavigation();
        if (schedulingView != null) page.RestoreView(schedulingView);
        return page;
    }
    // Cung cấp sinh viên và đăng ký để trang lớp/phòng tra lịch theo từng đối tượng.
    private async Task<TimetableDirectory> LoadTimetableDirectoryAsync(CancellationToken token)
    {
        if (databaseRepository != null)
        {
            var students = await databaseRepository.LoadTimetableStudentsAsync(token);
            return new(students, timetableRegistrations.Select(r => new TimetableRegistration(
                r.StudentId, r.ClassId, r.Status != "DANG_KY")).ToImmutableArray());
        }
        // Chế độ SQL thiếu nguồn thì báo lỗi; chỉ dùng danh bạ mẫu khi thực sự đang chạy demo trong bộ nhớ.
        if (databaseMode) throw new PersistenceException("Chưa có nguồn dữ liệu sinh viên cho thời khóa biểu. Vui lòng tải lại ứng dụng.");
        return new([new(StudentTimetableDemo.StudentId, StudentTimetableDemo.StudentName, "CNTT24A", "Công nghệ thông tin 24A"),
            new("SV2024002", "Sinh viên mẫu 2", "KT24A", "Kinh tế 24A")],
            StudentTimetableDemo.Registrations.Select(r => new TimetableRegistration(r.StudentId, r.ClassId, r.IsCanceled)).ToImmutableArray());
    }
    // Ghép môn, phòng và buổi học thành mô tả các lớp chưa hủy của giảng viên để hiển thị trong hồ sơ.
    private IReadOnlyList<TeachingClass> AssignedClasses(Lecturer lecturer) => scheduleStore.Classes
        .Where(item => item.LecturerId == lecturer.Id && item.Status != SchedulingLabels.Canceled)
        .Select(item => new TeachingClass(item.Id,
            scheduleStore.Catalog.Courses.First(course => course.Id == item.CourseId).Name,
            string.Join(", ", item.Meetings.Select(meeting => meeting.RoomId).Distinct()),
            item.Meetings.IsEmpty ? "Chưa xếp lịch" : string.Join("; ", item.Meetings.Select(meeting =>
                $"{(meeting.Day == DayOfWeek.Sunday ? "CN" : "T" + ((int)meeting.Day + 1))} · Tiết {meeting.StartPeriod}–{meeting.EndPeriod}"))))
        .ToArray();
    // Lưu trạng thái xem lớp/phòng rồi giải phóng trang cũ để lần mở sau dùng nguồn dữ liệu/DPI mới.
    private void CaptureSchedulingView()
    {
        if (scheduling != null) schedulingView = scheduling.CaptureView();
        scheduling?.Dispose(); scheduling = null;
    }
    // Điểm kiểm chứng việc dựng lại trang lớp/phòng có giữ đúng trạng thái đã lưu hay không.
    internal void RecreateSchedulingForCheck()
    {
        CaptureSchedulingView();
        if (schedulingVisible) ShowScheduling();
    }
    // Quay lại trang giảng viên, phục hồi màu/hiệu ứng và làm mới danh sách từ kho đang dùng chung.
    internal void ShowLecturers()
    {
        SurfaceTransition.CancelAll(); schedulingVisible = false;
        if (scheduling != null) scheduling.Visible = false;
        BackColor = titlebar.BackColor = AppTheme.Canvas; titlebar.ForeColor = AppTheme.Ink;
        Text = "Campus · Quản lý giảng viên";
        nav.Visible = !embedded; body.Visible = true;
        pageMotion.SetAmbient((seconds, enabled) => { hero.ApplyAmbient(seconds, enabled); if (!enabled) overview.ResetHover(); });
        RefreshResults(false); LayoutScreen(); StartPageEntrance();
    }
}
