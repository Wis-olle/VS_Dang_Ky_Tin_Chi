using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Features.Scheduling;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.StudentTimetable.TimetableStyle;

namespace QuanLyGiangVien.Features.StudentTimetable;

// Control tự vẽ lưới tuần và quản lý các nút buổi học; chỉ phát sự kiện chọn, không chỉnh sửa dữ liệu lịch.
internal sealed class StudentWeekCalendar : Control
{
    // Lịch sở hữu các thẻ, font và kho ảnh môn học; các thẻ chỉ mượn bitmap từ kho ảnh này.
    private readonly List<SessionCard> cards = [];
    private readonly SchedulingVisualCatalog visuals = new();
    private readonly Font dayFont = AppTheme.Font(9, true), smallFont = AppTheme.Font(8);
    private DateOnly monday;
    // Cung cấp sự kiện và trạng thái cần thiết cho form cha và bộ kiểm chứng giao diện.
    internal event Action<ScheduledOccurrence>? SessionSelected;
    internal IReadOnlyList<SessionCard> Cards => cards;
    internal int GridTop => S(64);
    // Bật vẽ đệm và tự vẽ lại khi thay đổi kích thước để lưới tuần hiển thị ổn định.
    internal StudentWeekCalendar()
    {
        DoubleBuffered = true; BackColor = Color.White; TabStop = false;
        SetStyle(ControlStyles.ResizeRedraw, true);
        AccessibleName = "Thời khóa biểu tuần, từ thứ hai đến chủ nhật";
    }
    // Thay toàn bộ thẻ theo tuần mới, giữ kho ảnh dùng chung và chọn mặc định buổi học đầu tiên nếu có.
    internal void SetWeek(ScheduleStore store, DateOnly start, IReadOnlyList<ScheduledOccurrence> occurrences)
    {
        SuspendLayout();
        // Giải phóng các nút của tuần cũ trước khi tạo danh sách mới; ảnh môn học vẫn được kho visuals giữ lại.
        foreach (var card in cards) card.Dispose();
        cards.Clear(); monday = start;
        foreach (var occurrence in occurrences)
        {
            // Ghép tên môn, giảng viên và ảnh môn để tạo một nút có thể chọn cho từng lần học cụ thể.
            var course = store.Catalog.Courses.First(c => c.Id == occurrence.Class.CourseId);
            var lecturer = store.Catalog.Lecturers.FirstOrDefault(l => l.Id == occurrence.LecturerId)?.Name ?? occurrence.LecturerId;
            var artwork = visuals.GetCourse(course);
            var card = new SessionCard(occurrence, course.Name, lecturer, artwork.Image, artwork.Key);
            card.Click += (_, _) => SelectSession(card);
            cards.Add(card); Controls.Add(card);
        }
        if (cards.Count > 0) cards[0].Selected = true;
        ResumeLayout(); LayoutCards(); Invalidate();
    }
    // Đánh dấu duy nhất một thẻ được chọn, yêu cầu vẽ lại và báo buổi học cho form cha cập nhật chi tiết.
    internal void SelectSession(SessionCard selected)
    {
        foreach (var card in cards) { card.Selected = card == selected; card.Invalidate(); }
        SessionSelected?.Invoke(selected.Occurrence);
    }
    // Khi lưới đổi kích thước, tính lại vị trí thẻ thay vì giữ tọa độ của kích thước cũ.
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutCards(); }
    // Quy đổi ngày và tiết thành tọa độ; các buổi trùng tiết trong cùng ngày được chia thành các làn cạnh nhau.
    private void LayoutCards()
    {
        float column = Math.Max(1, Width - S(52)) / 7f;
        float row = Math.Max(1, Height - GridTop - S(8)) / 12f;
        // Xét từng ngày riêng, xếp buổi theo tiết bắt đầu và tái sử dụng làn đã kết thúc trước buổi kế tiếp.
        foreach (var dayCards in cards.GroupBy(c => c.Occurrence.Date))
        {
            var lanes = new List<int>();
            var assignments = new List<(SessionCard Card, int Lane)>();
            foreach (var card in dayCards.OrderBy(c => c.Occurrence.Meeting.StartPeriod).ThenBy(c => c.Occurrence.Class.Id))
            {
                var meeting = card.Occurrence.Meeting;
                int lane = lanes.FindIndex(end => end < meeting.StartPeriod);
                if (lane < 0) { lane = lanes.Count; lanes.Add(meeting.EndPeriod); }
                else lanes[lane] = meeting.EndPeriod;
                assignments.Add((card, lane));
            }
            foreach (var (card, lane) in assignments)
            {
                // Chia đều bề rộng cột theo số làn của ngày; chiều cao thẻ phủ đủ số tiết từ đầu đến cuối buổi.
                int day = card.Occurrence.Date.DayNumber - monday.DayNumber;
                var meeting = card.Occurrence.Meeting;
                float laneWidth = column / lanes.Count;
                card.SetBounds((int)(S(52) + day * column + lane * laneWidth + S(4)),
                    (int)(GridTop + (meeting.StartPeriod - 1) * row + S(4)),
                    Math.Max(1, (int)laneWidth - S(8)), Math.Max(1, (int)((meeting.EndPeriod - meeting.StartPeriod + 1) * row) - S(8)));
            }
        }
    }
    // Vẽ bảy cột ngày, mười hai hàng tiết, đánh dấu hôm nay và thông báo khi tuần không có buổi học.
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float col = Math.Max(1, Width - S(52)) / 7f;
        float row = Math.Max(1, Height - GridTop - S(8)) / 12f;
        using var line = new Pen(Line);
        // Tiêu đề mỗi ngày gồm thứ và ngày/tháng; ngày hiện tại có nền nhấn riêng để dễ nhận biết.
        for (int day = 0; day < 7; day++)
        {
            var date = monday.AddDays(day);
            var rect = new Rectangle((int)(S(52) + day * col), S(8), (int)col, S(44));
            if (date == DateOnly.FromDateTime(DateTime.Today))
            {
                using var path = AppTheme.Round(rect, S(8)); using var fill = new SolidBrush(Soft); g.FillPath(fill, path);
            }
            TextRenderer.DrawText(g, day == 6 ? "CN" : $"Thứ {day + 2}", dayFont,
                rect with { Height = S(22) }, PurpleDark, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, date.ToString("dd/MM"), smallFont,
                rect with { Y = S(30), Height = S(18) }, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            g.DrawLine(line, S(52) + day * col, GridTop, S(52) + day * col, Height - S(8));
        }
        for (int period = 0; period <= 12; period++)
        {
            float y = GridTop + period * row;
            g.DrawLine(line, S(52), y, Width, y);
            if (period < 12) TextRenderer.DrawText(g, $"{period + 1:00}", smallFont,
                new Rectangle(0, (int)y, S(44), (int)row), Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        TextRenderer.DrawText(g, "Tiết", smallFont, new Rectangle(0, S(18), S(48), S(28)), Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        // Hiện hướng dẫn ngay trên vùng lưới trống, không tạo thêm thẻ giả cho tuần không có lịch.
        if (cards.Count == 0)
        {
            using var fill = new SolidBrush(Color.White);
            var area = new Rectangle(S(100), S(160), Width - S(150), S(100)); g.FillRectangle(fill, area);
            TextRenderer.DrawText(g, "Tuần này chưa có lịch học\nCác lớp đã đăng ký sẽ hiển thị tại đây.", dayFont,
                area, Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
    // Giải phóng font, để lớp cha đóng các thẻ trước, rồi mới hủy kho ảnh mà các thẻ đã mượn.
    protected override void Dispose(bool disposing)
    {
        if (disposing) { dayFont.Dispose(); smallFont.Dispose(); }
        base.Dispose(disposing);
        if (disposing) visuals.Dispose();
    }

    // Một nút tự vẽ đại diện cho một buổi học, hỗ trợ chuột, bàn phím, tooltip và trình đọc màn hình.
    internal sealed class SessionCard : Button
    {
        internal ScheduledOccurrence Occurrence { get; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal bool Selected { get; set; }
        private readonly string course, lecturer;
        // Bitmap thuộc kho ảnh của lịch cha; thẻ giữ tham chiếu để vẽ và không tự Dispose ảnh.
        private readonly Bitmap? artwork;
        internal string? ArtworkKey { get; }
        internal Bitmap? ArtworkImage => artwork;
        internal bool HasArtwork => artwork != null;
        // Chọn kích thước/vị trí biểu tượng theo diện tích thẻ; ẩn biểu tượng nếu thẻ quá hẹp hoặc quá thấp.
        internal Rectangle ArtworkBounds
        {
            get
            {
                if (artwork == null || Width < S(64) || Height < S(28)) return Rectangle.Empty;
                if (Height < S(60)) return Width < S(88) ? Rectangle.Empty : new(S(6), (Height - S(18)) / 2, S(18), S(18));
                return new(Width - S(30), S(7), S(22), S(22));
            }
        }
        private readonly Font title = AppTheme.Font(9, true), small = AppTheme.Font(7.5f);
        // Tài nguyên riêng của thẻ phục vụ đổi màu khi rê chuột và hiển thị toàn bộ thông tin qua tooltip.
        private readonly Motion hover = new();
        private readonly ToolTip tooltip = new() { InitialDelay = 350, AutoPopDelay = 10000 };
        private float tint;
        internal bool IsAnimating => hover.IsRunning;
        // Điểm gọi dành cho kiểm chứng native: mô phỏng rê chuột và kích hoạt nút bằng phím Space.
        internal void HoverForCheck(bool entered) => Animate(entered ? 1 : 0);
        internal void SpaceForCheck()
        {
            OnKeyDown(new KeyEventArgs(Keys.Space)); OnKeyUp(new KeyEventArgs(Keys.Space));
        }
        // Gắn dữ liệu buổi học và mô tả truy cập; giữ hành vi Button để Enter/Space có thể mở chi tiết.
        internal SessionCard(ScheduledOccurrence occurrence, string course, string lecturer, Bitmap? artwork = null, string? artworkKey = null)
        {
            Occurrence = occurrence; this.course = course; this.lecturer = lecturer;
            this.artwork = artwork; ArtworkKey = artworkKey;
            DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Color.White; Cursor = Cursors.Hand; TabStop = true;
            AccessibleName = $"{course}, {occurrence.Date:dd/MM/yyyy}, tiết {occurrence.Meeting.StartPeriod} đến {occurrence.Meeting.EndPeriod}, phòng {occurrence.RoomId}, {lecturer}";
            AccessibleDescription = "Bấm hoặc Enter/Space để xem chi tiết buổi học.";
            tooltip.SetToolTip(this, AccessibleName);
        }
        // Nội suy mức màu hover trong 160 ms; khi thẻ ẩn hoặc bị vô hiệu hóa thì cập nhật ngay, không chạy hiệu ứng.
        private void Animate(float target)
        {
            float start = tint;
            if (!Visible || !Enabled) { hover.Cancel(false); tint = target; return; }
            hover.Run(160, p => { tint = start + (target - start) * p; Invalidate(); });
        }
        // Chuột vào/ra đổi mức hover; đổi focus yêu cầu vẽ lại viền để người dùng bàn phím biết nút đang chọn.
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Animate(1); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Animate(0); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        // Dừng hiệu ứng và đưa màu hover về ban đầu khi thẻ không còn hiển thị.
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (!Visible) { hover.Cancel(false); tint = 0; } }
        // Vẽ nền, viền và nội dung theo trạng thái chọn cùng kích thước hiện tại của thẻ.
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Color.White); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 3 || Height < 3) return;
            var background = Selected ? SelectedFill : SessionHover(tint);
            using var path = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), S(8));
            using var fill = new SolidBrush(background); g.FillPath(fill, path);
            using var border = new Pen(Selected ? SelectedFill : SessionLine); g.DrawPath(border, path);
            var color = Selected ? Color.White : Ink; var muted = Selected ? Color.White : Muted;
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            // Thẻ quá hẹp vẫn chọn được và có thông tin qua tooltip/trình đọc màn hình, nhưng không vẽ chữ chật chội.
            if (Width < S(28)) return;
            // Buổi học có thẻ thấp chỉ vẽ tên môn trên một dòng và biểu tượng nhỏ nếu còn đủ chỗ.
            if (Height < S(60))
            {
                var icon = ArtworkBounds;
                DrawArtwork(g, icon);
                int textLeft = icon.IsEmpty ? S(6) : icon.Right + S(6);
                TextRenderer.DrawText(g, course, small, new Rectangle(textLeft, 0, Math.Max(1, Width - textLeft - S(6)), Height), color,
                    flags | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -S(3), -S(3)), color, background);
                return;
            }
            var artBounds = ArtworkBounds;
            // Thẻ đủ cao hiển thị nhãn phòng và ảnh môn ở hàng trên, chừa phần dưới cho tên môn cùng tiết học.
            int pillWidth = Math.Min(S(52), Width - S(16) - (artBounds.IsEmpty ? 0 : S(28)));
            using var pill = AppTheme.Round(new RectangleF(S(8), S(8), pillWidth, S(20)), S(8));
            using var pillFill = new SolidBrush(Selected ? Color.White : SelectedFill);
            g.FillPath(pillFill, pill);
            TextRenderer.DrawText(g, Occurrence.RoomId, small, new Rectangle(S(9), S(8), Math.Max(1, pillWidth - S(2)), S(20)),
                Selected ? SessionInk : Color.White, flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            DrawArtwork(g, artBounds);
            // Tùy chiều cao còn lại, vẽ tên môn nhiều dòng kèm tiết học hoặc dùng tên rút gọn trên một dòng.
            if (Height >= S(75))
            {
                TextRenderer.DrawText(g, course, title, new Rectangle(S(10), S(32), Width - S(20), Math.Min(S(46), Height - S(55))),
                    color, flags | TextFormatFlags.WordBreak);
                TextRenderer.DrawText(g, $"Tiết {Occurrence.Meeting.StartPeriod}–{Occurrence.Meeting.EndPeriod}", small,
                    new Rectangle(S(10), Height - S(23), Width - S(20), S(18)), muted, flags);
            }
            else TextRenderer.DrawText(g, course, small, new Rectangle(S(10), S(29), Width - S(20), S(20)), color, flags);
            if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -S(4), -S(4)), color, background);
        }
        // Co ảnh theo đúng tỉ lệ và căn giữa ô biểu tượng; phục hồi trạng thái Graphics sau khi vẽ chất lượng cao.
        private void DrawArtwork(Graphics graphics, Rectangle bounds)
        {
            if (artwork == null || bounds.IsEmpty) return;
            float scale = Math.Min(bounds.Width / (float)artwork.Width, bounds.Height / (float)artwork.Height);
            var target = new RectangleF(bounds.X + (bounds.Width - artwork.Width * scale) / 2,
                bounds.Y + (bounds.Height - artwork.Height * scale) / 2, artwork.Width * scale, artwork.Height * scale);
            var state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(artwork, target);
            graphics.Restore(state);
        }
        // Hủy hiệu ứng, tooltip và font riêng; bitmap mượn vẫn do kho ảnh của lịch cha quản lý.
        protected override void Dispose(bool disposing)
        {
            if (disposing) { hover.Dispose(); tooltip.Dispose(); title.Dispose(); small.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
