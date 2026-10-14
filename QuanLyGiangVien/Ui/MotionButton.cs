using System.Drawing.Drawing2D;

namespace QuanLyGiangVien.Ui;

/// <summary>Native one-shot light sweep inspired by Magic UI's MIT Shimmer Button.</summary>
internal sealed class MotionButton : AntdUI.Button
{
    private readonly Motion shine = new();
    private float progress = 1;
    internal bool IsShining => shine.IsRunning;
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); PlayShine(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); PlayShine(); }
    internal void PlayShine()
    {
        if (Type != AntdUI.TTypeMini.Primary || !Enabled || !Visible) return;
        shine.Run(600, p => { progress = p; Invalidate(); });
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (progress >= 1 || Width < 12 || Height < 12) return;
        var state = e.Graphics.Save();
        using var clip = AppTheme.Round(new RectangleF(4, 4, Width - 8, Height - 8), AppTheme.S(Radius)); e.Graphics.SetClip(clip);
        int band = Math.Max(AppTheme.S(42), Width / 3), x = (int)(-band + (Width + band * 2) * progress);
        using var brush = new LinearGradientBrush(new Rectangle(x, 0, band, Height), Color.Transparent, Color.Transparent, 0f);
        brush.InterpolationColors = new ColorBlend { Colors = [Color.FromArgb(0, Color.White), Color.FromArgb(55, Color.White), Color.FromArgb(0, Color.White)], Positions = [0, .5f, 1] };
        e.Graphics.FillRectangle(brush, x, 0, band, Height); e.Graphics.Restore(state);
    }
    protected override void Dispose(bool disposing) { if (disposing) shine.Dispose(); base.Dispose(disposing); }
}
