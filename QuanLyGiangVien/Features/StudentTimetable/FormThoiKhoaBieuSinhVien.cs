using QuanLyGiangVien.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.StudentTimetable.TimetableStyle;

namespace QuanLyGiangVien.Features.StudentTimetable;

/// <summary>Hiển thị lịch sinh viên từ dữ liệu được cấp quyền; không sửa lớp hoặc buổi học.</summary>
internal sealed partial class StudentTimetableForm : AntdUI.Window
{
    // Giữ dữ liệu lịch, danh tính sinh viên và các đăng ký để chỉ hiển thị buổi học thuộc sinh viên hiện tại.
    private readonly ScheduleStore store;
    private readonly bool embedded;
    internal bool IsEmbedded => embedded;
    private readonly IReadOnlyList<StudentRegistration> registrations;
    private readonly string studentId, studentName, studentClass, studentMajor;
    private string FirstName => studentName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "bạn";
    // Form sở hữu bộ điều phối hiệu ứng và ảnh tạo riêng; các control bên dưới tạo nên từng vùng của trang.
    private readonly PageMotionCoordinator motion;
    private readonly List<Image> images = [];
    private Panel scroll = null!;
    private AntdUI.Panel top = null!, intro = null!, nextCard = null!, profile = null!, summary = null!, detail = null!, calendarShell = null!;
    private StudentWeekCalendar calendar = null!;
    private ComboBox semester = null!;
    private Label weekLabel = null!, greeting = null!, introCaption = null!, nextTitle = null!, nextDescription = null!;
    private Label summaryText = null!, detailTitle = null!, detailBody = null!;
    private AntdUI.Button previous = null!, next = null!, today = null!;
    private AntdUI.Button navigation = null!;
    // Lưu tuần đang xem và kết quả truy vấn; cờ building ngăn sự kiện chạy khi giao diện chưa dựng xong.
    private StudentTimetableSnapshot snapshot = null!;
    private DateOnly selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private string semesterId = "HK1-2627";
    private bool building;
    // Cung cấp trạng thái và bề mặt giao diện cho nơi nhúng form cùng bộ kiểm chứng native.
    internal StudentTimetableSnapshot Snapshot => snapshot;
    internal StudentWeekCalendar Calendar => calendar;
    internal Control CalendarSurface => calendarShell;
    internal Control HeaderSurface => top;
    internal AntdUI.Button NavigationButton => navigation;
    internal ComboBox SemesterPicker => semester;
    internal string SelectedDetails => detailBody.Text;
    internal bool MotionIdle => motion.ActiveCount == 0;
    internal bool HasHorizontalScroll => scroll.HorizontalScroll.Visible;
    internal int ScrollOffset => scroll.AutoScrollPosition.Y;
    // Nhận vị trí cuộn theo đơn vị bố cục rồi đổi sang pixel theo tỉ lệ hiển thị hiện tại.
    internal void ScrollPage(int y) { scroll.AutoScrollPosition = new(0, S(y)); }

    // Chọn nguồn dữ liệu, xác định sinh viên và dựng form ở chế độ cửa sổ riêng hoặc nhúng trong ứng dụng.
    internal StudentTimetableForm(DatabaseSnapshot? database = null, bool embedded = false, StudentTimetableData? readModel = null)
    {
        this.embedded = embedded;
        if (embedded) { TopLevel = false; FormBorderStyle = FormBorderStyle.None; Dock = DockStyle.Fill; }
        // Ưu tiên mô hình đọc đã được cấp quyền; chuẩn hóa đăng ký bị hủy trước khi đưa vào phần truy vấn lịch.
        if (readModel != null)
        {
            store = readModel.CreateStore();
            studentId = readModel.Identity.StudentId!; studentName = readModel.Identity.DisplayName;
            studentClass = readModel.Identity.ClassId ?? "Chưa có lớp hành chính";
            studentMajor = readModel.Identity.MajorName ?? "Chưa có thông tin";
            registrations = readModel.Registrations.Select(item => new StudentRegistration(item.StudentId, item.ClassId, item.Status == "DA_HUY")).ToArray();
            semesterId = store.Catalog.Semesters.First().Id;
        }
        else if (database != null)
        {
            // Nguồn SQL phải mang danh tính sinh viên có mã liên kết; từ chối dùng tài khoản khác để xem lịch.
            if (database.Identity.Role != "SINH_VIEN" || string.IsNullOrWhiteSpace(database.Identity.StudentId))
                throw new PersistenceException("Thời khóa biểu SQL cần tài khoản sinh viên đã được liên kết với SQL login. Dùng --demo để xem bản mẫu.");
            store = database.CreateStores().Scheduling;
            studentId = database.Identity.StudentId; studentName = database.Identity.DisplayName;
            studentClass = database.Identity.ClassId ?? "Chưa có lớp hành chính";
            studentMajor = database.Identity.MajorName ?? "Chưa có thông tin";
            registrations = database.Registrations.Select(item => new StudentRegistration(item.StudentId, item.ClassId,
                item.Status == "DA_HUY")).ToArray();
            semesterId = store.Catalog.Semesters.First().Id;
        }
        else
        {
            // Không truyền nguồn SQL thì dùng dữ liệu mẫu để có thể xem trước trang thời khóa biểu.
            store = SchedulingDemo.Create(); registrations = StudentTimetableDemo.Registrations;
            studentId = StudentTimetableDemo.StudentId; studentName = StudentTimetableDemo.StudentName;
            studentClass = "Lớp CNTT K24 · Khóa 2024"; studentMajor = "Công nghệ thông tin";
        }
        // Chủ động áp dụng tỉ lệ DPI và giới hạn kích thước cửa sổ theo vùng làm việc của màn hình.
        AutoHandDpi = false; AutoScaleMode = AutoScaleMode.None; AppTheme.SetScale(DeviceDpi);
        Text = "Campus · Thời khóa biểu sinh viên"; BackColor = Canvas;
        StartPosition = FormStartPosition.CenterScreen;
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (!embedded) MinimumSize = new(Math.Min(S(1020), workArea.Width), Math.Min(S(700), workArea.Height));
        ClientSize = new(Math.Min(S(1440), workArea.Width - S(24)), Math.Min(S(960), workArea.Height - S(48)));
        motion = new(this); Build();
        // Khi mở hoặc đổi kích thước, cập nhật bố cục; khi ẩn trang, dừng hiệu ứng của khung lịch.
        Shown += (_, _) => { LayoutPage(); SurfaceTransition.Reveal(intro, 320); SurfaceTransition.Reveal(calendarShell, 360, 60); };
        SizeChanged += (_, _) => { if (!building) LayoutPage(); };
        VisibleChanged += (_, _) => { if (!Visible) SurfaceTransition.Cancel(calendarShell); };
        StudentTimetableUiCheck.Attach(this);
    }
    #region Nhóm 1 - Dựng trang và các nút thao tác xem lịch
    // Dựng toàn bộ vùng giao diện trong một đợt, sau đó nạp tuần đầu tiên mà không chạy hiệu ứng chuyển tuần.
    private void Build()
    {
        building = true;
        SuspendLayout();
        BuildHeader();
        // Nội dung cuộn độc lập với thanh đầu trang; dừng hiệu ứng khi cuộn để tránh dùng ảnh chuyển cảnh cũ.
        scroll = new Panel { BackColor = Canvas, AutoScroll = true };
        scroll.Scroll += (_, _) => { SurfaceTransition.Cancel(calendarShell); SurfaceTransition.Cancel(intro); };
        Controls.Add(scroll); top.BringToFront();
        BuildIntro(); BuildSidebar(); BuildCalendar();
        building = false; ResumeLayout(); LayoutPage(); RefreshWeek(false);
    }
    // Tạo thanh thương hiệu, tên sinh viên và nút quay về đầu trang; ẩn thanh này khi form được nhúng.
    private void BuildHeader()
    {
        top = Card(); top.Radius = 0; top.Back = Canvas;
        var caption = new AntdUI.PageHeader { Text = " ", ShowButton = true, ShowIcon = false, BackColor = Canvas };
        OwnFont(caption, 8); caption.Dock = DockStyle.Top; caption.Height = S(30); top.Controls.Add(caption);
        Put(top, Picture("graduation-cap.png"), 28, 40, 30, 30);
        Put(top, Label("campus.", 18, true), 68, 35, 130, 38);
        Put(top, Label("CỔNG SINH VIÊN", 8, true, Muted), 205, 42, 150, 26);
        // Chuyển SVG lịch thành biểu tượng nét và tô theo màu chủ đạo trước khi gắn vào nút điều hướng.
        var icon = System.Xml.Linq.XElement.Parse(Icon("calendar-days"));
        var outline = new System.Xml.Linq.XElement(icon.Name.Namespace + "g",
            new System.Xml.Linq.XAttribute("fill", "none"), icon.Elements().ToArray());
        icon.ReplaceNodes(outline);
        var iconColor = AppTheme.PrimaryPressed;
        var iconSvg = icon.ToString().Replace("currentColor", $"#{iconColor.R:X2}{iconColor.G:X2}{iconColor.B:X2}", StringComparison.Ordinal);
        navigation = AppTheme.Button("Thời khóa biểu", iconSvg);
        navigation.IconSize = new Size(14, 14);
        // Font của nút được giải phóng theo vòng đời nút để không giữ tài nguyên đồ họa sau khi đóng form.
        var navigationFont = navigation.Font;
        navigation.Disposed += (_, _) => navigationFont.Dispose();
        navigation.Name = "studentTimetableNav";
        navigation.DefaultBack = navigation.DefaultBorderColor = AppTheme.AccentSoft;
        navigation.ForeColor = AppTheme.PrimaryPressed; navigation.BorderWidth = 0;
        navigation.AccessibleName = "Thời khóa biểu";
        navigation.AccessibleDescription = "Trang hiện tại. Bấm để trở về đầu trang, giữ nguyên học kỳ và tuần.";
        navigation.Click += (_, _) => ScrollPage(0);
        Put(top, navigation, 380, 33, 180, 42);
        var user = Label(studentName, 9, true); user.Name = "headerUser";
        top.Controls.Add(user); Controls.Add(top);
        top.Visible = !embedded;
    }
    // Tạo lời chào, ảnh minh họa và thẻ buổi học sắp tới; nội dung lịch được điền khi cập nhật tuần.
    private void BuildIntro()
    {
        greeting = Label("Thời khóa biểu của bạn", 21, true);
        introCaption = Label("Theo dõi lịch học, sẵn sàng cho mỗi buổi lên lớp.", 9, false, Muted);
        scroll.Controls.AddRange([greeting, introCaption]);
        intro = Card(); nextCard = Card();
        intro.Back = AppTheme.Peach; nextCard.Back = AppTheme.Cyan;
        Put(intro, Label("MỘT TUẦN HỌC TẬP MỚI", 8, true, PurpleDark), 22, 17, 240, 24);
        Put(intro, Label($"Chào {FirstName}, học tốt nhé!", 16, true), 22, 45, 310, 32);
        Put(intro, Label("Lịch học của các lớp bạn đã đăng ký.", 9, false, Muted), 22, 83, 300, 23);
        var art = Picture("programming.png"); art.Name = "introArt"; intro.Controls.Add(art);
        Put(nextCard, Label("LỊCH HỌC SẮP TỚI", 8, true, Teal), 20, 17, 240, 24);
        nextTitle = Label("", 11, true); nextDescription = Label("", 9, false, Muted);
        Put(nextCard, nextTitle, 20, 47, 300, 27); Put(nextCard, nextDescription, 20, 79, 320, 43);
        scroll.Controls.AddRange([intro, nextCard]);
    }
    // Tạo khung lịch, bộ chọn học kỳ, nút chuyển tuần và liên kết sự kiện chọn một buổi học với phần chi tiết.
    private void BuildCalendar()
    {
        calendarShell = Card(); scroll.Controls.Add(calendarShell);
        Put(calendarShell, Label("Lịch học trong tuần", 13, true), 22, 16, 270, 30);
        weekLabel = Label("", 9, false, Muted); Put(calendarShell, weekLabel, 22, 48, 350, 23);
        semester = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
            BackColor = Color.White, ForeColor = Ink, AccessibleName = "Chọn học kỳ" };
        OwnFont(semester, 9);
        semester.Items.AddRange(store.Catalog.Semesters.Select(s => s.Name).Cast<object>().ToArray());
        semester.SelectedIndex = store.Catalog.Semesters.ToList().FindIndex(s => s.Id == semesterId);
        // Đổi học kỳ sẽ lấy ngày hiện tại làm mốc và tính lại tuần; bỏ qua sự kiện phát sinh lúc dựng giao diện.
        semester.SelectedIndexChanged += (_, _) => {
            if (building || semester.SelectedIndex < 0) return;
            semesterId = store.Catalog.Semesters[semester.SelectedIndex].Id;
            selectedDate = DateOnly.FromDateTime(DateTime.Today); RefreshWeek();
        };
        calendarShell.Controls.Add(semester);
        SoftInputSurface.Enhance(semester);
        previous = Button("", "chevron-left"); previous.AccessibleName = "Tuần trước";
        next = Button("", "chevron-right"); next.AccessibleName = "Tuần sau";
        // Về tuần hiện tại
        today = Button("Tuần hiện tại"); today.Click += (_, _) => { selectedDate = DateOnly.FromDateTime(DateTime.Today); RefreshWeek(); };
        // Xem tuần trước hoặc tuần sau
        previous.Click += (_, _) => ChangeWeek(-1); next.Click += (_, _) => ChangeWeek(1);
        calendarShell.Controls.AddRange([previous, today, next]);
        calendar = new StudentWeekCalendar(); calendar.SessionSelected += ShowDetails;
        calendarShell.Controls.Add(calendar);
    }
    #endregion

    #region Nhóm 2 - Chuyển tuần, cập nhật lịch và xem chi tiết (không ghi SQL)
    // Chuyển lùi hoặc tiến đúng bảy ngày nếu tuần đích còn nằm trong giới hạn học kỳ của snapshot hiện tại.
    internal void ChangeWeek(int direction)
    {
        if ((direction < 0 && !snapshot.CanGoPrevious) || (direction > 0 && !snapshot.CanGoNext)) return;
        selectedDate = snapshot.WeekStart.AddDays(direction < 0 ? -7 : 7); RefreshWeek();
    }
    // Dùng một ngày làm mốc để phần domain xác định tuần tương ứng và cập nhật toàn bộ thông tin trên trang.
    internal void SelectDate(DateOnly date) { selectedDate = date; RefreshWeek(); }
    // Tạo snapshot chỉ đọc cho học kỳ và tuần đang chọn, rồi đồng bộ lịch, thống kê và thẻ thông tin.
    internal void RefreshWeek(bool animate = true)
    {
        // Chụp lịch cũ khi cần chuyển cảnh; truy vấn domain lọc lớp theo đăng ký của đúng sinh viên.
        var before = animate ? SurfaceTransition.Capture(calendar) : null;
        snapshot = Domain.StudentTimetable.Create(store, registrations,
            studentId, semesterId, selectedDate);
        calendar.SetWeek(store, snapshot.WeekStart, snapshot.Occurrences);
        weekLabel.Text = $"{snapshot.WeekStart:dd/MM} – {snapshot.WeekEnd:dd/MM/yyyy}  ·  {snapshot.Occurrences.Length} buổi học";
        previous.Enabled = snapshot.CanGoPrevious; next.Enabled = snapshot.CanGoNext;
        // Thống kê học phần, tín chỉ của học kỳ và số buổi học thực sự xuất hiện trong tuần đang xem.
        summaryText.Text = $"{snapshot.CourseCount:00}  học phần\n\n{snapshot.TotalCredits:00}  tín chỉ\n\n{snapshot.Occurrences.Length:00}  buổi học trong tuần";
        var current = DateOnly.FromDateTime(DateTime.Today);
        // Tìm buổi học từ hôm nay đến hết học kỳ trong các lớp sinh viên đã đăng ký, độc lập với tuần đang xem.
        var upcoming = Domain.StudentTimetable.Create(store, registrations,
            studentId, semesterId, current);
        var candidate = SchedulingQuery.Occurrences(store, current < snapshot.Semester.StartDate ? snapshot.Semester.StartDate : current,
            snapshot.Semester.EndDate).FirstOrDefault(o => upcoming.Classes.Any(c => c.Id == o.Class.Id) && o.Date >= current);
        nextTitle.Text = candidate == null ? "Chưa có buổi học sắp tới" : store.Catalog.Courses.First(c => c.Id == candidate.Class.CourseId).Name;
        nextDescription.Text = candidate == null ? "Bạn có thể xem lịch ở các tuần khác." : $"{candidate.Date:dd/MM/yyyy} · Tiết {candidate.Meeting.StartPeriod}–{candidate.Meeting.EndPeriod}\nPhòng {candidate.RoomId}";
        // Mặc định mở chi tiết buổi đầu của tuần; tuần trống có thông báo hướng dẫn thay vì giữ nội dung cũ.
        if (snapshot.Occurrences.Length > 0) ShowDetails(snapshot.Occurrences[0]);
        else { detailTitle.Text = "Chưa có buổi học"; detailBody.Text = "Chọn học kỳ hoặc chuyển tuần để xem lịch học của bạn."; }
        if (animate) SurfaceTransition.Crossfade(calendar, before, 200);
    }
    // Tra tên môn, số tín chỉ và giảng viên để trình bày đầy đủ buổi học được chọn ở thẻ bên phải.
    private void ShowDetails(ScheduledOccurrence occurrence)
    {
        var course = store.Catalog.Courses.First(c => c.Id == occurrence.Class.CourseId);
        var lecturer = store.Catalog.Lecturers.FirstOrDefault(l => l.Id == occurrence.LecturerId);
        detailTitle.Text = course.Name;
        detailBody.Text = $"{occurrence.Class.Id} · {course.Credits} tín chỉ\n\n{occurrence.Date:dd/MM/yyyy}\nTiết {occurrence.Meeting.StartPeriod} – {occurrence.Meeting.EndPeriod}\n\nPhòng {occurrence.RoomId}\n\n{lecturer?.Name ?? occurrence.LecturerId}";
    }
    #endregion

    // Khi chuyển DPI, hủy hiệu ứng, giải phóng control và ảnh cũ rồi dựng lại theo tỉ lệ mới.
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (building || scroll == null) return;
        SurfaceTransition.Cancel(calendar); SurfaceTransition.Cancel(calendarShell); SurfaceTransition.Cancel(intro);
        building = true; foreach (Control control in Controls.Cast<Control>().ToArray()) control.Dispose();
        foreach (var image in images) image.Dispose(); images.Clear();
        AppTheme.SetScale(DeviceDpi); if (!embedded) MinimumSize = new(S(1020), S(700)); Build();
    }
    // Dừng hiệu ứng, đóng control con và giải phóng bộ điều phối cùng ảnh mà form sở hữu.
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SurfaceTransition.Cancel(calendar); SurfaceTransition.Cancel(calendarShell); SurfaceTransition.Cancel(intro);
            motion.Dispose(); base.Dispose(true);
            foreach (var image in images) image.Dispose(); images.Clear();
        }
        else base.Dispose(false);
    }
}
