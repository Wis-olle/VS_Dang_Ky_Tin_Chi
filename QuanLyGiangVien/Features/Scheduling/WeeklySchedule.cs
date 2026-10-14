using System.Drawing.Drawing2D;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Grouped daily sessions remain distinct when several rooms teach at the same time.</summary>
internal sealed class WeeklySchedule : UserControl
{
    private readonly System.Windows.Forms.Panel[] days = Enumerable.Range(0, 7).Select(_ => new DayPanel { AutoScroll = true, BackColor = Color.White }).ToArray();
    private readonly List<Font> fonts = [];
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal SchedulingVisualCatalog? Visuals { get; set; }
    internal event Action<CourseClass>? ClassSelected;
    internal int OccurrenceCount { get; private set; }
    internal bool CardsReadableForCheck => days.SelectMany(day => day.Controls.OfType<System.Windows.Forms.Button>()).All(button => {
        if (button is SessionButton sessionButton) return sessionButton.ContentFitsForCheck;
        using var graphics = button.CreateGraphics();
        // The borderless flat Button still has 2px layout padding and a 2px text inset per edge.
        int height = TextRenderer.MeasureText(graphics, button.Text, button.Font,
            new Size(Math.Max(1, button.Width - button.Padding.Horizontal - 8), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
        return height + button.Padding.Vertical + 8 <= button.Height;
    });
    internal void ScrollFirstDayForCheck() => days[0].AutoScrollPosition = new(0, days[0].VerticalScroll.Maximum);
    internal void ActivateFirstSessionForCheck() => days.SelectMany(day => day.Controls.OfType<System.Windows.Forms.Button>()).First().PerformClick();
    internal bool FirstDayAlignedForCheck => days[0].Controls.OfType<System.Windows.Forms.Button>().FirstOrDefault() is { } button
        && button.Top - days[0].AutoScrollPosition.Y == AppTheme.S(44);
    internal int VisualThumbnailsForCheck => days.SelectMany(day => day.Controls.OfType<SessionButton>()).Count(button => button.HasThumbnail);
    internal bool ThumbnailsFitForCheck => days.SelectMany(day => day.Controls.OfType<SessionButton>()).All(button => button.ThumbnailFitsForCheck);

    internal WeeklySchedule()
    {
        BackColor = Color.White;
        Controls.AddRange(days);
        AccessibleName = "Lịch lớp học theo tuần";
        Resize += (_, _) => LayoutDays();
    }
    internal void SetData(ScheduleStore store, IReadOnlyList<ScheduledOccurrence> sessions, DateOnly monday)
    {
        foreach (var day in days)
        {
            foreach (var control in day.Controls.Cast<Control>().ToArray()) control.Dispose();
        }
        foreach (var font in fonts) font.Dispose(); fonts.Clear();
        OccurrenceCount = sessions.Count;
        for (int i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            var title = SchedulingTheme.Label((i == 6 ? "Chủ nhật" : $"Thứ {i + 2}") + $" · {date:dd/MM}", 9, true);
            title.TextMultiLine = false;
            fonts.Add(title.Font); title.Dock = DockStyle.Top; title.Height = AppTheme.S(38);
            days[i].Controls.Add(title);
            int y = AppTheme.S(44);
            foreach (var session in sessions.Where(s => s.Date == date).OrderBy(s => s.Meeting.StartPeriod).ThenBy(s => s.RoomId))
            {
                var course = store.Catalog.Courses.First(c => c.Id == session.Class.CourseId);
                var tone = session.Meeting.StartPeriod <= 4 ? SchedulingTheme.Cyan : session.Meeting.StartPeriod <= 8 ? SchedulingTheme.Lavender : SchedulingTheme.Rose;
                var button = new SessionButton(tone, Visuals?.GetCourse(course).Image) {
                    Text = $"{course.Name}\n{session.Class.Id}\n{session.RoomId} · Tiết {session.Meeting.StartPeriod}–{session.Meeting.EndPeriod}",
                    Font = AppTheme.Font(9), ForeColor = SchedulingTheme.Ink,
                    FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, Padding = new Padding(AppTheme.S(8)),
                    AccessibleName = $"{course.Name}, {date:dd/MM}, phòng {session.RoomId}, tiết {session.Meeting.StartPeriod} đến {session.Meeting.EndPeriod}",
                    Tag = session.Class, Cursor = Cursors.Hand
                };
                fonts.Add(button.Font); button.FlatAppearance.BorderSize = 0;
                button.SetBounds(AppTheme.S(3), y, Math.Max(AppTheme.S(80), days[i].ClientSize.Width - AppTheme.S(6)), AppTheme.S(106));
                button.Click += (_, _) => ClassSelected?.Invoke(session.Class);
                days[i].Controls.Add(button); y += AppTheme.S(116);
            }
            if (y == AppTheme.S(44))
            {
                var empty = SchedulingTheme.Label("Chưa có lịch", 8, color: SchedulingTheme.Muted);
                fonts.Add(empty.Font); empty.SetBounds(AppTheme.S(5), y, AppTheme.S(125), AppTheme.S(30)); days[i].Controls.Add(empty);
            }
            days[i].AutoScrollMinSize = new(0, y);
        }
        LayoutDays();
    }
    private void LayoutDays()
    {
        int gap = AppTheme.S(8), width = Math.Max(1, (Width - gap * 6) / 7);
        using var graphics = CreateGraphics();
        for (int i = 0; i < 7; i++)
        {
            days[i].SuspendLayout();
            days[i].SetBounds(i * (width + gap), 0, width, Height);
            var scroll = days[i].AutoScrollPosition;
            // A stable gutter avoids changing the text width when a day's scrollbar appears.
            int cardWidth = Math.Max(1, width - AppTheme.S(6)
                - SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi));
            int y = AppTheme.S(44);
            foreach (var button in days[i].Controls.OfType<System.Windows.Forms.Button>())
            {
                int height;
                if (button is SessionButton sessionButton) height = Math.Max(AppTheme.S(106), sessionButton.RequiredHeightForWidth(graphics, cardWidth));
                else {
                    int textHeight = TextRenderer.MeasureText(graphics, button.Text, button.Font,
                        new Size(Math.Max(1, cardWidth - button.Padding.Horizontal - 8), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                    height = Math.Max(AppTheme.S(106), textHeight + button.Padding.Vertical + 8);
                }
                button.SetBounds(scroll.X + AppTheme.S(3), scroll.Y + y, cardWidth, height);
                y += height + AppTheme.S(10);
            }
            days[i].AutoScrollMinSize = new(0, y);
            days[i].ResumeLayout(true);
        }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { foreach (var font in fonts) font.Dispose(); fonts.Clear(); }
    }

    private sealed class DayPanel : System.Windows.Forms.Panel
    {
        internal DayPanel() => SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var border = new Pen(Color.FromArgb(100, SchedulingTheme.Line));
            e.Graphics.DrawLine(border, Width - 1, AppTheme.S(44), Width - 1, Height);
        }
    }

    /// <summary>Rounded paint preserves the standard Button's activation and tab navigation.</summary>
    private sealed class SessionButton : System.Windows.Forms.Button
    {
        private const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private readonly Motion hoverMotion = new();
        private readonly Color tone;
        // Borrowed from the page-owned catalog; session controls never dispose this bitmap.
        private readonly Bitmap? thumbnail;
        private Font titleFont = AppTheme.Font(9, true), metadataFont = AppTheme.Font(8);
        private string title = "", metadata = "", header = "";
        private Rectangle titleBounds, metadataBounds, thumbnailBounds, headerBounds;
        private int layoutWidth = -1;
        private float layoutScale = -1, layoutDpi = -1, tint;
        private bool hovered, pressed;

        internal SessionButton(Color tone, Bitmap? thumbnail)
        {
            this.tone = tone; this.thumbnail = thumbnail;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            UseVisualStyleBackColor = false;
        }
        internal bool HasThumbnail => thumbnail != null;
        internal bool ThumbnailFitsForCheck
        {
            get {
                using var graphics = CreateGraphics();
                EnsureTextLayout(graphics, Width);
                return thumbnail == null || (thumbnailBounds.Bottom < titleBounds.Top
                    && thumbnailBounds.Right <= Width - AppTheme.S(12)
                    && headerBounds.Right + AppTheme.S(6) <= thumbnailBounds.Left
                    && thumbnailBounds.Bottom < metadataBounds.Top);
            }
        }
        internal bool ContentFitsForCheck
        {
            get {
                using var graphics = CreateGraphics();
                EnsureTextLayout(graphics, Width);
                return metadataBounds.Bottom + AppTheme.S(12) <= Height && (header.Length == 0 ||
                    TextRenderer.MeasureText(graphics, header, metadataFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width <= headerBounds.Width);
            }
        }
        internal int RequiredHeightForWidth(Graphics graphics, int width)
        {
            EnsureTextLayout(graphics, width);
            return metadataBounds.Bottom + AppTheme.S(12);
        }
        private void EnsureTextLayout(Graphics graphics, int width)
        {
            bool dpiChanged = layoutScale != AppTheme.K || layoutDpi != graphics.DpiX;
            if (!dpiChanged && layoutWidth == width) return;
            if (dpiChanged) {
                titleFont.Dispose(); metadataFont.Dispose();
                titleFont = AppTheme.Font(9, true); metadataFont = AppTheme.Font(8);
            }
            layoutScale = AppTheme.K; layoutDpi = graphics.DpiX; layoutWidth = width;
            int pad = AppTheme.S(12), textWidth = Math.Max(1, width - pad * 2);
            int titleHeight = TextRenderer.MeasureText(graphics, title, titleFont, new Size(textWidth, int.MaxValue), Wrap).Height;
            int metadataHeight = TextRenderer.MeasureText(graphics, metadata, metadataFont, new Size(textWidth, int.MaxValue), Wrap).Height;
            titleBounds = new Rectangle(pad, pad + (thumbnail == null ? 0 : AppTheme.S(32)), textWidth, titleHeight);
            thumbnailBounds = thumbnail == null ? Rectangle.Empty : new Rectangle(width - pad - AppTheme.S(24), pad, AppTheme.S(24), AppTheme.S(24));
            headerBounds = thumbnail == null ? Rectangle.Empty : new Rectangle(pad, pad, Math.Max(1, textWidth - AppTheme.S(30)), AppTheme.S(24));
            metadataBounds = new Rectangle(pad, titleBounds.Bottom + AppTheme.S(6), textWidth, metadataHeight);
        }
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            string text = Text.Replace("\r", "", StringComparison.Ordinal);
            int separator = text.IndexOf('\n');
            title = separator < 0 ? text : text[..separator];
            metadata = separator < 0 ? "" : text[(separator + 1)..];
            int headerSeparator = metadata.IndexOf('\n');
            header = thumbnail != null && headerSeparator >= 0 ? metadata[..headerSeparator] : "";
            if (header.Length > 0) metadata = metadata[(headerSeparator + 1)..];
            layoutWidth = -1;
            Invalidate();
        }
        private void AnimateTint()
        {
            float from = tint, target = Enabled && (hovered || Focused) ? 1 : 0;
            hoverMotion.Run(180, p => { tint = from + (target - from) * p; Invalidate(); });
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; AnimateTint(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; AnimateTint(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); AnimateTint(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); pressed = false; AnimateTint(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { pressed = false; Invalidate(); } }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } }
        protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); if (e.KeyCode == Keys.Space) { pressed = false; Invalidate(); } }
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) { hoverMotion.Cancel(false); pressed = false; tint = 0; }
            Invalidate();
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) { hoverMotion.Cancel(false); hovered = pressed = false; tint = 0; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            var graphics = e.Graphics;
            // Flat Button repainting can skip OnPaintBackground; cover the buffer outside the rounded path too.
            using (var background = new SolidBrush(BackColor))
                graphics.FillRectangle(background, ClientRectangle);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            EnsureTextLayout(graphics, Width);
            using var shape = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), AppTheme.S(12));
            float amount = pressed && Enabled ? 1 : .72f + tint * .2f;
            using var fill = new SolidBrush(Blend(Color.White, tone, amount));
            using var border = new Pen(Blend(Color.White, SchedulingTheme.Line, .55f));
            graphics.FillPath(fill, shape); graphics.DrawPath(border, shape);
            DrawThumbnail(graphics);
            if (header.Length > 0) TextRenderer.DrawText(graphics, header, metadataFont, headerBounds, SchedulingTheme.Muted,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(graphics, title, titleFont, titleBounds, Enabled ? SchedulingTheme.Ink : SchedulingTheme.Muted, Wrap);
            TextRenderer.DrawText(graphics, metadata, metadataFont, metadataBounds, SchedulingTheme.Muted, Wrap);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(graphics,
                new Rectangle(AppTheme.S(4), AppTheme.S(4), Width - AppTheme.S(8), Height - AppTheme.S(8)), SchedulingTheme.Primary, BackColor);
        }
        private void DrawThumbnail(Graphics graphics)
        {
            if (thumbnail == null || thumbnail.Width <= 0 || thumbnail.Height <= 0) return;
            float scale = Math.Min(thumbnailBounds.Width / (float)thumbnail.Width, thumbnailBounds.Height / (float)thumbnail.Height);
            var destination = new RectangleF(thumbnailBounds.X + (thumbnailBounds.Width - thumbnail.Width * scale) / 2,
                thumbnailBounds.Y + (thumbnailBounds.Height - thumbnail.Height * scale) / 2,
                thumbnail.Width * scale, thumbnail.Height * scale);
            var state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(thumbnail, destination);
            graphics.Restore(state);
        }
        private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
            (int)(from.R + (to.R - from.R) * amount), (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));
        protected override void Dispose(bool disposing)
        {
            if (disposing) { hoverMotion.Dispose(); titleFont.Dispose(); metadataFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
