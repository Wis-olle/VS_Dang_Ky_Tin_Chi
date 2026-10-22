using AntdUI;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed partial class SchedulingPage
{
    // Sắp xếp giao diện quản lý lớp và phòng
    private void LayoutPage()
    {
        if (Width < 1 || Height < 1) return;
        int s(int n) => AppTheme.S(n);
        SuspendLayout(); content.SuspendLayout();
        // Quy đổi kích thước theo DPI; thanh tiêu đề giữ học kỳ và nút thao tác ở bên phải.
        int pageWidth = Math.Max(s(800), Width - s(56));
        navigation.SetBounds(s(28), s(8), pageWidth, s(70));
        breadcrumb.SetBounds(0, 0, Math.Max(1, pageWidth - s(550)), s(18));
        pageTitle.SetBounds(0, s(22), Math.Max(1, pageWidth - s(550)), s(38));
        teachers.SetBounds(pageWidth - s(530), s(24), s(140), s(38));
        semester.SetBounds(pageWidth - s(378), s(24), s(220), s(38));
        add.SetBounds(pageWidth - s(146), s(24), s(146), s(38));
        content.SetBounds(s(28), s(84), pageWidth, Math.Max(1, Height - s(108)));
        // Chừa trước bề rộng thanh cuộn dọc để thay đổi kích thước không tạo cuộn ngang ngoài ý muốn.
        int width = content.Width - SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi) - s(4);
        int gap = s(18), side = width >= s(1200) ? s(320) : s(300), left = width - side - gap;
        bool inline = width >= s(900);
        // Màn hình rộng đặt chi tiết bên cạnh bảng; màn hình hẹp đưa chi tiết xuống dưới.
        // Tab lịch ẩn chi tiết và phần xem trước để dành toàn bộ chiều ngang cho thời khóa biểu.
        int listY = s(202), listHeight = Math.Max(s(480), content.Height - listY);
        bool showDetail = tabs.SelectIndex != 2;
        int analyticsY = inline && showDetail ? listY : listY + listHeight + gap;
        int detailY = inline ? analyticsY + s(124) + gap : listY + listHeight + gap;
        int weeklyY = showDetail ? detailY + s(466) + gap : inline ? listY : listY + listHeight + gap;
        int total = showDetail ? Math.Max(listY + listHeight, weeklyY + s(202)) : inline ? analyticsY + s(124) : listY + listHeight;
        content.AutoScrollMinSize = new(0, total);
        var scroll = content.AutoScrollPosition;
        // Tọa độ control con luôn cộng vị trí cuộn hiện tại để bố cục không bị nhảy khi cuộn trang.
        void Place(Control c, int x, int y, int w, int h) => c.SetBounds(scroll.X + x, scroll.Y + y, w, h);
        int statsWidth = width;
        Place(statsCard, 0, 0, statsWidth, s(188));
        int statGap = s(12), statWidth = (statsWidth - statGap * 3) / 4;
        // Chia đều bốn thẻ thống kê; thẻ cuối nhận phần lẻ còn lại để không hở mép phải.
        for (int i = 0; i < 4; i++) stats[i].SetBounds(i * (statWidth + statGap), s(4),
            i == 3 ? statsWidth - i * (statWidth + statGap) : statWidth, s(180));
        analyticsCard.Visible = inline;
        Place(analyticsCard, inline ? left + gap : 0, analyticsY, side, s(124));
        Place(listCard, 0, listY, inline && showDetail ? left : width, listHeight);
        detail.Visible = showDetail;
        weeklyCard.Visible = showDetail;
        Place(detail, inline ? left + gap : 0, detailY, inline ? side : width, s(466));
        Place(weeklyCard, inline ? left + gap : 0, weeklyY, inline ? side : width, s(202));
        int agendaWidth = weeklyCard.Width;
        weeklyTitle.SetBounds(s(16), s(10), agendaWidth - s(32), s(24));
        weekCaption.SetBounds(s(16), s(33), agendaWidth - s(32), s(18));
        for (int i = 0; i < 3; i++) previews[i].SetBounds(s(12), s(54 + i * 48), agendaWidth - s(24), s(46));
        LayoutWorkspace();
        content.ResumeLayout(true); ResumeLayout(false);
    }

    // Sắp xếp danh sách và khung chi tiết
    private void LayoutWorkspace()
    {
        int s(int n) => AppTheme.S(n);
        int pad = s(24), width = Math.Max(1, listCard.Width - pad * 2);
        bool schedule = tabs.SelectIndex == 2;
        int tabsWidth = Math.Min(s(380), width - s(190));
        listTitle.SetBounds(pad, s(12), width - tabsWidth - s(14), s(34));
        tabs.SetBounds(listCard.Width - pad - tabsWidth, s(8), tabsWidth, s(42));
        // Danh sách dùng tìm kiếm/bộ lọc; tab lịch thay bằng chọn đối tượng và điều hướng tuần.
        search.Visible = filter.Visible = !schedule;
        timetableScope.Visible = timetableSubject.Visible = reloadTimetableSubjects.Visible = schedule;
        previousWeek.Visible = nextWeek.Visible = currentWeek.Visible = weekLabel.Visible = schedule;
        if (schedule)
        {
            previousWeek.SetBounds(pad, s(68), s(34), s(34));
            nextWeek.SetBounds(pad + s(40), s(68), s(34), s(34));
            weekLabel.SetBounds(pad + s(90), s(68), s(230), s(34));
            currentWeek.SetBounds(pad + s(330), s(68), s(110), s(34));
            timetableScope.SetBounds(pad, s(110), s(180), s(34));
            timetableSubject.SetBounds(pad + s(192), s(110), Math.Max(s(240), width - s(324)), s(34));
            reloadTimetableSubjects.SetBounds(listCard.Width - pad - s(120), s(110), s(120), s(34));
        }
        else
        {
            search.SetBounds(pad, s(60), Math.Max(s(180), width - s(200)), s(38));
            filter.SetBounds(listCard.Width - pad - s(184), s(60), s(184), s(38));
        }
        int resultsY = s(schedule ? 156 : 114), resultsHeight = Math.Max(s(180), listCard.Height - resultsY - s(20));
        // Bảng, lịch và trạng thái không có kết quả dùng chung vùng nội dung bên dưới công cụ.
        table.SetBounds(pad, resultsY, width, resultsHeight);
        calendar.SetBounds(pad, resultsY, width, resultsHeight);
        emptyArtwork.SetBounds((listCard.Width - s(112)) / 2, resultsY + s(24), s(112), s(76));
        empty.SetBounds(pad, resultsY + s(114), width, s(64));
        reset.SetBounds((listCard.Width - s(170)) / 2, resultsY + s(190), s(170), s(38));
        // Ẩn cột phụ khi thiếu chỗ; cột học phần hoặc tòa nhà giãn lấp phần ngang còn lại.
        if (tabs.SelectIndex == 0 && table.Columns.Count >= 8)
        {
            table.Columns[3].Visible = width >= s(770);
            table.Columns[5].Visible = width >= s(890);
            table.Columns[2].SetWidthFill();
        }
        else if (tabs.SelectIndex == 1 && table.Columns.Count >= 7) table.Columns[2].SetWidthFill();
        table.LoadLayout();
    }

    // Tạo cột bảng lớp hoặc phòng học
    private void ConfigureTable()
    {
        table.Columns = tabs.SelectIndex == 1 ? new ColumnCollection {
            new Column("Artwork", "") { Width = "62" },
            new Column("Id", "Phòng") { Width = "86" }, new Column("Building", "Tòa nhà") { Width = "fill" },
            new Column("Type", "Loại phòng") { Width = "140" }, new Column("Capacity", "Chỗ") { Width = "65", Align = ColumnAlign.Center },
            new Column("Status", "Trạng thái") { Width = "118" }, new Column("Actions", "") { Width = "96", Align = ColumnAlign.Left }
        } : new ColumnCollection {
            new Column("Artwork", "") { Width = "62" },
            new Column("Id", "Mã lớp") { Width = "118" }, new Column("Course", "Học phần") { Width = "fill", Ellipsis = true },
            new Column("Lecturer", "Giảng viên") { Width = "165", Ellipsis = true }, new Column("Seats", "Sĩ số") { Width = "64", Align = ColumnAlign.Center },
            new Column("Schedule", "Lịch / Phòng") { Width = "150", Ellipsis = true }, new Column("Status", "Trạng thái") { Width = "105" },
            new Column("Actions", "") { Width = "96", Align = ColumnAlign.Left }
        };
    }
}
