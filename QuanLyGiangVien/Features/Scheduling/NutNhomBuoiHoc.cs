using System.Drawing.Drawing2D;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>A bounded native summary; every underlying occurrence remains in its details.</summary>
internal sealed class WeeklyGroupButton : Button
{
    private readonly int count, start, end;
    private readonly bool warning;
    private readonly Font title = AppTheme.Font(9, true), small = AppTheme.Font(8);

    internal WeeklyGroupButton(int count, int start, int end, bool warning)
    {
        this.count = count; this.start = start; this.end = end; this.warning = warning;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Color.White; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
        Text = $"{count} buổi giao giờ";
        AccessibleName = $"{Text}, tiết {start} đến {end}";
        AccessibleDescription = (warning ? "Có xung đột. " : "")
            + "Bấm hoặc Enter/Space để mở danh sách đầy đủ từng buổi học.";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        var g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        int s(int value) => AppTheme.S(value);
        using var shape = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), s(10));
        using var fill = new SolidBrush(warning ? AppTheme.AccentSoft : SchedulingTheme.Lavender);
        using var border = new Pen(warning ? SchedulingTheme.Danger : SchedulingTheme.Line, warning ? 1.5f : 1);
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        var ink = warning ? SchedulingTheme.Danger : SchedulingTheme.Ink;
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        int width = Math.Max(1, Width - s(18));
        TextRenderer.DrawText(g, Text, title, new Rectangle(s(9), s(6), width, s(18)), ink,
            flags | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, $"Tiết {start}–{end} · Xem chi tiết", small,
            new Rectangle(s(9), s(24), width, s(16)), SchedulingTheme.Muted, flags | TextFormatFlags.SingleLine);
        if (Height >= s(84)) TextRenderer.DrawText(g, warning ? "Cần kiểm tra lịch" : "Xem từng buổi học", small,
            new Rectangle(s(9), s(51), width, Height - s(59)), SchedulingTheme.Muted, flags | TextFormatFlags.WordBreak);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,
            Rectangle.Inflate(ClientRectangle, -s(4), -s(4)), ink, BackColor);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { title.Dispose(); small.Dispose(); }
        base.Dispose(disposing);
    }
}
