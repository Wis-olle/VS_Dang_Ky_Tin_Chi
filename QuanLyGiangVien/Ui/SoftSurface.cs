using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace QuanLyGiangVien.Ui;

/// <summary>A buffered native panel with antialiased corners and a fixed layout rectangle.</summary>
internal class SoftSurface : Panel
{
    private int radius = 14;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Radius { get => radius; set { radius = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color BorderColor { get; set; } = Color.Empty;

    internal SoftSurface()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? AppTheme.Canvas);
        if (Width < 3 || Height < 3) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = AppTheme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), AppTheme.S(Radius));
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, shape);
        if (BorderColor.IsEmpty) return;
        using var line = new Pen(BorderColor, AppTheme.S(1));
        e.Graphics.DrawPath(line, shape);
    }
}
