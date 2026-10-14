using System.Diagnostics;
using System.Drawing.Imaging;

namespace QuanLyGiangVien.Ui;

/// <summary>Single-snapshot, click-through fades; live controls keep their original bounds.</summary>
internal static class SurfaceTransition
{
    private static readonly Dictionary<Control, Overlay> active = [];
    internal static int ActiveCount => active.Count;
    internal static Bitmap? Capture(Control target)
    {
        if (!AntdUI.Config.Animation || !target.IsHandleCreated || !target.Visible || target.Width < 1 || target.Height < 1) return null;
        Bitmap? bitmap = null;
        try { bitmap = new(target.Width, target.Height); target.DrawToBitmap(bitmap, target.ClientRectangle); return bitmap; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        { bitmap?.Dispose(); Trace.TraceWarning("Surface transition capture unavailable: {0}", e.Message); return null; }
    }
    internal static void Reveal(Control target, int milliseconds = 360, int delayMilliseconds = 0) => Crossfade(target, null, milliseconds, delayMilliseconds);
    internal static void Crossfade(Control target, Bitmap? previous, int milliseconds = 220, int delayMilliseconds = 0)
    {
        Cancel(target);
        var next = Capture(target);
        if (next == null || target.Parent == null) { previous?.Dispose(); return; }
        var overlay = new Overlay(target, previous, next);
        active[target] = overlay;
        target.Parent.Controls.Add(overlay); overlay.BringToFront();
        overlay.Start(milliseconds, delayMilliseconds);
    }
    internal static void Cancel(Control target) { if (active.Remove(target, out var overlay)) overlay.Dispose(); }
    internal static void CancelAll() { foreach (var key in active.Keys.ToArray()) Cancel(key); }
    internal static void PaintForCapture(Control root, Graphics graphics)
    {
        // DrawToBitmap may print siblings in reverse z-order. Repaint the actual overlays last.
        foreach (var overlay in active.Values.ToArray())
        {
            if (overlay.IsDisposed || !overlay.Visible || !root.Contains(overlay)) continue;
            var origin = root.PointToClient(overlay.PointToScreen(Point.Empty));
            var clip = root.ClientRectangle;
            for (Control? ancestor = overlay.Parent; ancestor != null && ancestor != root; ancestor = ancestor.Parent)
            {
                var point = root.PointToClient(ancestor.PointToScreen(Point.Empty));
                clip.Intersect(new Rectangle(point, ancestor.ClientSize));
            }
            clip.Intersect(new Rectangle(origin, overlay.Size));
            var state = graphics.Save();
            graphics.SetClip(clip);
            graphics.TranslateTransform(origin.X, origin.Y);
            overlay.PaintSnapshot(graphics);
            graphics.Restore(state);
        }
    }
    private sealed class Overlay : Control
    {
        private readonly Control target;
        private readonly Bitmap? previous;
        private readonly Bitmap next;
        private readonly Motion motion = new();
        private float progress;
        private bool released;
        internal Overlay(Control control, Bitmap? before, Bitmap after)
        {
            target = control; previous = before; next = after;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false; Enabled = false; BackColor = Background(control.Parent!); Bounds = control.Bounds;
            target.LocationChanged += TargetChanged; target.SizeChanged += TargetChanged;
            target.VisibleChanged += TargetChanged; target.Disposed += TargetChanged;
        }
        internal void Start(int duration, int delay) => motion.Run(duration, p => {
            progress = p; if (p >= 1) Cancel(target); else Invalidate();
        }, delay);
        private void TargetChanged(object? sender, EventArgs e) => Cancel(target);
        private static Color Background(Control control)
        {
            if (control is AntdUI.Panel panel && panel.Back is { A: 255 } color) return color;
            if (control.BackColor.A == 255) return control.BackColor;
            return control.Parent == null ? AppTheme.Canvas : Background(control.Parent);
        }
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
            => PaintSnapshot(e.Graphics);
        internal void PaintSnapshot(Graphics graphics)
        {
            using var background = new SolidBrush(BackColor);
            graphics.FillRectangle(background, ClientRectangle);
            if (previous != null) Draw(graphics, previous, 1 - progress, -AppTheme.S(4) * progress, 0);
            Draw(graphics, next, progress, previous == null ? 0 : AppTheme.S(8) * (1 - progress), previous == null ? AppTheme.S(8) * (1 - progress) : 0);
        }
        private static void Draw(Graphics graphics, Bitmap bitmap, float alpha, float x, float y)
        {
            using var attributes = new ImageAttributes(); attributes.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
            graphics.DrawImage(bitmap, new Rectangle((int)Math.Round(x), (int)Math.Round(y), bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !released)
            {
                released = true;
                if (active.TryGetValue(target, out var overlay) && ReferenceEquals(overlay, this)) active.Remove(target);
                target.LocationChanged -= TargetChanged; target.SizeChanged -= TargetChanged;
                target.VisibleChanged -= TargetChanged; target.Disposed -= TargetChanged;
                motion.Dispose(); previous?.Dispose(); next.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
