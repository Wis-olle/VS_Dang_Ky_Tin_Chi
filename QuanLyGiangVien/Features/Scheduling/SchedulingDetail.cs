using AntdUI;
using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

internal sealed class SchedulingDetail : AntdUI.Panel
{
    private readonly Tag identifier = new() { Text = "CHI TIẾT", Radius = 7, BorderWidth = 0,
        Font = AppTheme.Font(8), BackColor = AppTheme.Canvas, ForeColor = AppTheme.Muted };
    private readonly Tag status = new() { Radius = 7, BorderWidth = 0, Font = AppTheme.Font(7.5f) };
    private readonly AntdUI.Label title = SchedulingTheme.Label("", 12, true);
    private readonly AntdUI.Label subtitle = SchedulingTheme.Label("", 8, color: SchedulingTheme.Muted);
    private readonly AntdUI.Label lecturer = SchedulingTheme.Label("", 9);
    private readonly AntdUI.Label lecturerCaption = SchedulingTheme.Label("Giảng viên", 8, color: SchedulingTheme.Muted);
    private readonly AntdUI.Label seats = SchedulingTheme.Label("", 9);
    private readonly SeatProgress progress = new();
    private readonly SchedulingResourceCover cover = new();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal SchedulingVisualCatalog? Visuals { get; set; }
    internal string? VisualKeyForCheck => cover.VisualKeyForCheck;
    private readonly System.Windows.Forms.Panel scrollBody = new() { AutoScroll = true, BackColor = Color.White, TabStop = true };
    private readonly AntdUI.Label meetingTitle = SchedulingTheme.Label("Lịch học", 8.5f, color: SchedulingTheme.Muted);
    private readonly FlowLayoutPanel meetingCards = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
    private readonly Font meetingFont = AppTheme.Font(8.5f), metadataFont = AppTheme.Font(8);
    private readonly AntdUI.Button related = SchedulingTheme.Button("Xem phòng", "HomeOutlined");
    private readonly AntdUI.Button edit = SchedulingTheme.Button("Chỉnh sửa lớp", "EditOutlined", true);
    private readonly List<Font> ownedFonts = [];
    private CourseClass? currentClass;
    private Room? currentRoom;
    internal event Action<CourseClass>? EditClass;
    internal event Action<Room>? EditRoom;
    internal event Action<string>? RoomSelected;
    internal event Action<string>? RoomScheduleRequested;
    internal int ScheduleBodyHeightForCheck => scrollBody.Height;
    internal (float Displayed, float Target, bool Running) ProgressForCheck => progress.StateForCheck;
    internal IReadOnlyList<Font> OwnedFontResourcesForCheck => ownedFonts.Concat(cover.OwnedFontResourcesForCheck).ToArray();

    internal SchedulingDetail()
    {
        Back = Color.White; BackColor = Color.Transparent; Radius = 18;
        BorderWidth = 0; BorderColor = SchedulingTheme.Line;
        title.TextMultiLine = true; title.AutoEllipsis = false;
        subtitle.TextMultiLine = lecturerCaption.TextMultiLine = meetingTitle.TextMultiLine = false;
        lecturer.TextMultiLine = true; lecturer.AutoEllipsis = true;
        ownedFonts.AddRange([related.Font, edit.Font]);
        related.Font = AppTheme.Font(9); edit.Font = AppTheme.Font(9);
        related.BorderWidth = 0; related.ForeColor = AppTheme.Primary; related.Radius = 9;
        edit.Radius = 10;
        scrollBody.Controls.Add(meetingCards);
        Controls.AddRange([identifier, status, cover, title, subtitle, lecturer, lecturerCaption, seats, progress, meetingTitle, scrollBody, related, edit]);
        foreach (Control control in Controls) if (control is AntdUI.Label or AntdUI.Button) ownedFonts.Add(control.Font);
        ownedFonts.AddRange([identifier.Font, status.Font, meetingFont, metadataFont]);
        scrollBody.Scroll += (_, _) => SurfaceTransition.Cancel(scrollBody);
        related.Click += (_, _) => {
            if (currentClass is { Meetings.IsEmpty: false }) RoomSelected?.Invoke(currentClass.Meetings[0].RoomId);
            else if (currentRoom != null) RoomScheduleRequested?.Invoke(currentRoom.Id);
        };
        edit.Click += (_, _) => { if (currentClass != null) EditClass?.Invoke(currentClass); else if (currentRoom != null) EditRoom?.Invoke(currentRoom); };
        Resize += (_, _) => { SurfaceTransition.Cancel(scrollBody); LayoutDetail(); };
    }

    internal void ShowClass(CourseClass? item, ScheduleStore store, bool animate)
    {
        var before = animate ? SurfaceTransition.Capture(scrollBody) : null;
        currentClass = item; currentRoom = null;
        identifier.Text = item?.Id ?? "CHI TIẾT LỚP";
        status.Text = item?.Status ?? ""; status.Visible = item != null;
        status.BackColor = item?.Status == SchedulingLabels.Canceled ? SchedulingTheme.Rose : item?.Status == SchedulingLabels.Closed ? SchedulingTheme.Amber : SchedulingTheme.Mint;
        status.ForeColor = item?.Status == SchedulingLabels.Canceled ? SchedulingTheme.Danger : item?.Status == SchedulingLabels.Closed ? SchedulingTheme.Ink : SchedulingTheme.Green;
        var course = store.Catalog.Courses.FirstOrDefault(c => c.Id == item?.CourseId);
        cover.SetVisual(item == null ? null : Visuals?.GetCourse(course), false);
        var teacher = store.Catalog.Lecturers.FirstOrDefault(l => l.Id == item?.LecturerId);
        title.Text = course?.Name ?? "Chọn một lớp học";
        title.AccessibleName = title.Text;
        subtitle.Text = item == null ? "Thông tin lớp sẽ hiển thị tại đây." : $"{item.CourseId} · {course?.Credits} tín chỉ";
        lecturer.Visible = lecturerCaption.Visible = item != null;
        lecturerCaption.Text = "Giảng viên";
        lecturer.Text = teacher?.Name ?? "Chưa phân công";
        seats.Text = item == null ? "" : $"Sĩ số       {item.Enrolled} / {item.Capacity} sinh viên";
        progress.Visible = item != null;
        progress.SetValue(item == null || item.Capacity == 0 ? 0 : (float)item.Enrolled / item.Capacity, animate);
        meetingTitle.Text = item == null ? "Lịch học" : $"Lịch học · {item.Meetings.Length} buổi";
        SetMeetingCards(item == null ? [] : item.Meetings.IsEmpty
            ? [("Chưa xếp lịch", "Chỉnh sửa lớp để chọn phòng và buổi học.")]
            : item.Meetings.Select(m => ($"{CourseClassRow.Day(m.Day)} · Tiết {m.StartPeriod}–{m.EndPeriod} · {m.RoomId}",
                $"{m.StartDate:dd/MM} – {m.EndDate:dd/MM/yyyy}")), SchedulingTheme.Cyan);
        related.Text = "Xem phòng học"; related.Visible = item is { Meetings.IsEmpty: false };
        edit.Text = "Chỉnh sửa lớp"; edit.Visible = item != null;
        scrollBody.AutoScrollPosition = Point.Empty;
        LayoutDetail();
        if (animate) SurfaceTransition.Crossfade(scrollBody, before, 200);
    }

    internal void ShowRoom(Room? room, ScheduleStore store, bool animate)
    {
        var before = animate ? SurfaceTransition.Capture(scrollBody) : null;
        currentRoom = room; currentClass = null;
        cover.SetVisual(room == null ? null : Visuals?.GetRoom(room), true);
        identifier.Text = room?.Id ?? "CHI TIẾT PHÒNG";
        status.Text = room?.Status ?? ""; status.Visible = room != null;
        status.BackColor = room?.Status == SchedulingLabels.Maintenance ? SchedulingTheme.Amber : SchedulingTheme.Mint;
        status.ForeColor = room?.Status == SchedulingLabels.Maintenance ? SchedulingTheme.Ink : SchedulingTheme.Green;
        title.Text = room == null ? "Chọn một phòng" : $"Phòng {room.Id}";
        title.AccessibleName = title.Text;
        subtitle.Text = room == null ? "Thông tin phòng sẽ hiển thị tại đây." : room.Type;
        lecturer.Visible = lecturerCaption.Visible = room != null;
        lecturerCaption.Text = "Tòa nhà";
        lecturer.Text = room?.Building ?? "";
        seats.Text = room == null ? "" : $"Sức chứa       {room.Capacity} chỗ";
        progress.Visible = false;
        var classes = store.Classes.Where(c => c.Status != SchedulingLabels.Canceled && c.Meetings.Any(m => m.RoomId == room?.Id)).ToArray();
        meetingTitle.Text = $"Lớp sử dụng phòng · {classes.Length}";
        SetMeetingCards(room == null ? [] : classes.Length == 0 ? [("Chưa có lớp", "Phòng chưa được phân công lớp.")]
            : classes.Select(c => (store.Catalog.Courses.FirstOrDefault(x => x.Id == c.CourseId)?.Name ?? c.CourseId,
                $"{c.Id} · {c.Enrolled}/{c.Capacity} sinh viên")), SchedulingTheme.Lavender);
        related.Text = "Xem lịch sử dụng"; related.Visible = room != null;
        edit.Text = "Chỉnh sửa phòng"; edit.Visible = room != null;
        scrollBody.AutoScrollPosition = Point.Empty;
        LayoutDetail();
        if (animate) SurfaceTransition.Crossfade(scrollBody, before, 200);
    }

    private void LayoutDetail()
    {
        int s(int n) => AppTheme.S(n);
        int pad = s(18), width = Math.Max(1, Width - pad * 2);
        int titleHeight = Math.Clamp(TextRenderer.MeasureText(title.Text, title.Font,
            new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl |
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height, s(28), s(48));
        int identifierWidth = TextRenderer.MeasureText(identifier.Text, identifier.Font,
            Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + s(20);
        int statusWidth = Math.Clamp(TextRenderer.MeasureText(status.Text, status.Font,
            Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + s(22), s(80), s(112));
        identifier.SetBounds(pad, s(16), Math.Clamp(identifierWidth, s(72), Math.Max(s(72), width - statusWidth - s(10))), s(22));
        status.SetBounds(Width - pad - statusWidth, s(16), statusWidth, s(22));
        cover.SetBounds(pad, s(44), width, s(68));
        title.SetBounds(pad, s(124), width, titleHeight);
        subtitle.SetBounds(pad, title.Bottom + s(2), width, s(16));
        int factsY = Math.Max(s(184), subtitle.Bottom + s(8));
        lecturerCaption.SetBounds(pad, factsY + s(2), s(72), s(18));
        lecturer.SetBounds(pad + s(80), factsY, Math.Max(1, width - s(80)), s(28));
        int seatsY = factsY + s(36);
        seats.SetBounds(pad, seatsY, width, s(22));
        progress.SetBounds(pad, seatsY + s(26), width, s(5));
        meetingTitle.SetBounds(pad, seatsY + s(progress.Visible ? 39 : 29), width, s(18));
        int meetingsY = meetingTitle.Bottom + s(3), footerY = Height - s(84);
        scrollBody.SetBounds(pad, meetingsY, width, Math.Max(1, footerY - meetingsY - s(4)));
        // Reserve the native scrollbar width even before it appears, so wrapping is stable.
        int contentWidth = Math.Max(1, width - SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi) - s(3));
        int contentHeight = Math.Max(1, meetingCards.Controls.Count * s(50));
        meetingCards.SetBounds(scrollBody.AutoScrollPosition.X, scrollBody.AutoScrollPosition.Y, contentWidth, contentHeight);
        foreach (Control card in meetingCards.Controls)
        {
            card.Width = contentWidth;
            foreach (Control label in card.Controls.OfType<AntdUI.Label>()) label.Width = Math.Max(1, contentWidth - s(24));
        }
        scrollBody.AutoScrollMinSize = new Size(0, contentHeight);
        related.SetBounds(pad, footerY, width, s(28));
        edit.SetBounds(pad, footerY + s(34), width, s(36));
    }

    private void SetMeetingCards(IEnumerable<(string Title, string Metadata)> rows, Color tone)
    {
        foreach (Control control in meetingCards.Controls.Cast<Control>().ToArray()) control.Dispose();
        foreach (var row in rows)
        {
            var card = new AntdUI.Panel { Back = Color.White, BackColor = Color.White, Radius = 8, BorderWidth = 0, BorderColor = tone,
                Height = AppTheme.S(44), Margin = new(0, 0, 0, AppTheme.S(6)), AccessibleName = row.Title + "; " + row.Metadata };
            var heading = new AntdUI.Label { Text = row.Title, Font = meetingFont, ForeColor = SchedulingTheme.Ink, BackColor = Color.Transparent, TextMultiLine = false, AutoEllipsis = true };
            var metadata = new AntdUI.Label { Text = row.Metadata, Font = metadataFont, ForeColor = SchedulingTheme.Muted, BackColor = Color.Transparent, TextMultiLine = false, AutoEllipsis = true };
            var marker = new System.Windows.Forms.Panel { BackColor = tone, TabStop = false };
            marker.SetBounds(AppTheme.S(3), AppTheme.S(8), AppTheme.S(3), AppTheme.S(28));
            heading.SetBounds(AppTheme.S(12), AppTheme.S(3), 1, AppTheme.S(22));
            metadata.SetBounds(AppTheme.S(12), AppTheme.S(25), 1, AppTheme.S(18));
            card.Controls.AddRange([marker, heading, metadata]); meetingCards.Controls.Add(card);
        }
    }

    private sealed class SeatProgress : Control
    {
        private readonly Motion fillMotion = new();
        private float value, displayed;

        internal (float Displayed, float Target, bool Running) StateForCheck => (displayed, value, fillMotion.IsRunning);
        internal void Settle() { fillMotion.Cancel(); displayed = value; }
        internal void SetValue(float value, bool animate)
        {
            float next = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
            if (this.value == next) return;
            this.value = next;
            AccessibleDescription = $"{this.value:P0} sức chứa";
            if (!animate || !Visible)
            {
                fillMotion.Cancel(false); displayed = next; Invalidate(); return;
            }
            float from = displayed;
            fillMotion.Run(360, progress => { displayed = from + (next - from) * progress; Invalidate(); });
        }

        internal SeatProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            AccessibleName = "Mức sử dụng sĩ số";
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 0 || Height <= 0) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width, Height);
            using var track = RoundedBar(bounds);
            using var background = new SolidBrush(SchedulingTheme.Lavender);
            using var fill = new SolidBrush(SchedulingTheme.Primary);
            e.Graphics.FillPath(background, track);
            if (displayed <= 0) return;
            var state = e.Graphics.Save();
            e.Graphics.SetClip(track, CombineMode.Intersect);
            using var filled = RoundedBar(new RectangleF(0, 0, Width * displayed, Height));
            e.Graphics.FillPath(fill, filled);
            e.Graphics.Restore(state);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) Settle();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fillMotion.Dispose();
            base.Dispose(disposing);
        }

        private static GraphicsPath RoundedBar(RectangleF bounds)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(bounds.Width, bounds.Height);
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible && progress != null) { progress.Settle(); SurfaceTransition.Cancel(scrollBody); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) SurfaceTransition.Cancel(scrollBody);
        base.Dispose(disposing);
        if (disposing) { foreach (var font in ownedFonts.Distinct<Font>(ReferenceEqualityComparer.Instance)) font.Dispose(); }
    }
}
