using System.Runtime.CompilerServices;

namespace QuanLyGiangVien.Ui;

internal static class SurfaceHover
{
    private static readonly ConditionalWeakTable<AntdUI.Panel, HoverBinding> bindings = new();
    internal static void Bind(AntdUI.Panel panel, Color tone)
    {
        ObjectDisposedException.ThrowIf(panel.IsDisposed, panel);
        if (bindings.TryGetValue(panel, out _)) throw new InvalidOperationException("The surface already has a hover binding.");
        var binding = new HoverBinding(panel, tone);
        bindings.Add(panel, binding);
    }
    internal static void SetHovered(AntdUI.Panel panel, bool hovered)
    {
        if (!bindings.TryGetValue(panel, out var binding)) throw new InvalidOperationException("The surface has no hover binding.");
        binding.SetHovered(hovered);
    }
    private sealed class HoverBinding
    {
        private readonly AntdUI.Panel panel;
        private readonly Color original, tone;
        private readonly Motion motion = new();
        private readonly List<Control> targets = [];
        private float amount;
        private bool disposed;
        private bool hovered;
        internal HoverBinding(AntdUI.Panel panel, Color tone)
        {
            this.panel = panel; this.tone = tone;
            original = panel.Back ?? Color.White;
            Attach(panel);
            panel.Disposed += Dispose;
        }
        internal void SetHovered(bool nextHovered)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (hovered == nextHovered) return;
            hovered = nextHovered;
            float from = amount;
            float target = nextHovered ? 1 : 0;
            motion.Run(160, p => { amount = from + (target - from) * p; panel.Back = Blend(original, tone, amount); });
        }
        private void Enter(object? sender, EventArgs e) => SetHovered(true);
        private void Leave(object? sender, EventArgs e)
        {
            if (!panel.IsHandleCreated || !panel.ClientRectangle.Contains(panel.PointToClient(Cursor.Position))) SetHovered(false);
        }
        private void Attach(Control control)
        {
            targets.Add(control);
            control.MouseEnter += Enter; control.MouseLeave += Leave;
            foreach (Control child in control.Controls) Attach(child);
        }
        private void Dispose(object? sender, EventArgs e)
        {
            if (disposed) return;
            disposed = true; motion.Dispose();
            panel.Disposed -= Dispose;
            foreach (var target in targets) { target.MouseEnter -= Enter; target.MouseLeave -= Leave; }
            targets.Clear(); bindings.Remove(panel);
        }
    }
    private static Color Blend(Color a, Color b, float p) => Color.FromArgb(
        (int)Math.Round(a.R + (b.R - a.R) * p), (int)Math.Round(a.G + (b.G - a.G) * p), (int)Math.Round(a.B + (b.B - a.B) * p));
}
