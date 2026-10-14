using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using Label = AntdUI.Label;

namespace QuanLyGiangVien.Ui;

/// <summary>Auth Form's entrance and diagonal circular hover, rendered without moving live controls.</summary>
internal static class AuthCardMotion
{
    private static readonly ConditionalWeakTable<AntdUI.Panel, Binding> bindings = new();
    private static readonly HashSet<Overlay> overlays = [];
    internal static int ActiveCount => overlays.Count;
    internal static void Bind(AntdUI.Panel target, Color tone, bool hoverEnabled = true, bool entranceEnabled = true)
    {
        if (bindings.TryGetValue(target, out _)) throw new InvalidOperationException("Auth card motion is already bound.");
        bindings.Add(target, new Binding(target, tone, hoverEnabled, entranceEnabled));
    }
    internal static void Reveal(AntdUI.Panel target, int delay = 0) => Get(target).Reveal(delay);
    internal static void SetHovered(AntdUI.Panel target, bool hovered) => Get(target).SetHovered(hovered);
    internal static (float Entrance, float Fill, float Scale, bool Running, int CacheBuilds) State(AntdUI.Panel target)
    {
        var binding = Get(target);
        return (binding.entrance, binding.fill, AuthMotionMath.Scale(binding.zoom), binding.IsRunning, binding.cacheBuilds);
    }
    internal static void SetFrameForCheck(AntdUI.Panel target, float fill, float zoom = 0)
    {
        var binding = Get(target);
        binding.fillMotion.Cancel(false); binding.zoomMotion.Cancel(false);
        binding.fill = Math.Clamp(fill, 0, 1); binding.zoom = Math.Clamp(zoom, 0, 1);
        binding.hovered = fill > 0 || zoom > 0;
        binding.UpdateOverlay();
    }
    private static Binding Get(AntdUI.Panel target) => bindings.TryGetValue(target, out var binding) ? binding :
        throw new InvalidOperationException("The surface has no auth motion binding.");
    internal static void PaintForCapture(Control root, Graphics graphics)
    {
        foreach (var overlay in overlays.ToArray())
        {
            if (overlay.IsDisposed || !overlay.Visible || !root.Contains(overlay)) continue;
            var origin = root.PointToClient(overlay.PointToScreen(Point.Empty));
            var clip = root.ClientRectangle;
            for (Control? ancestor = overlay.Parent; ancestor != null && ancestor != root; ancestor = ancestor.Parent)
                clip.Intersect(new Rectangle(root.PointToClient(ancestor.PointToScreen(Point.Empty)), ancestor.ClientSize));
            clip.Intersect(new Rectangle(origin, overlay.Size));
            var state = graphics.Save();
            graphics.SetClip(clip); graphics.TranslateTransform(origin.X, origin.Y);
            overlay.PaintFrame(graphics); graphics.Restore(state);
        }
    }
    private sealed class Binding : IDisposable
    {
        internal readonly AntdUI.Panel target;
        internal readonly Color tone;
        internal readonly bool hoverEnabled, entranceEnabled;
        internal readonly Motion entryMotion = new(), fillMotion = new(), zoomMotion = new();
        private readonly HashSet<Control> hooked = [];
        private readonly Dictionary<Label, LabelRaster> labels = [];
        private readonly HashSet<Binding> deferredChildren = [];
        private Binding? deferredParent;
        private readonly List<Control> ancestors = [];
        private Form? owner;
        private Overlay? overlay;
        private bool disposed;
        private bool addingOverlay;
        internal bool hovered;
        internal float entrance = 1, fill, zoom;
        internal int cacheBuilds;
        internal bool IsRunning => entryMotion.IsRunning || fillMotion.IsRunning || zoomMotion.IsRunning;
        internal Binding(AntdUI.Panel panel, Color tint, bool hover, bool entry)
        {
            target = panel; tone = tint; hoverEnabled = hover; entranceEnabled = entry;
            Attach(panel);
            panel.Disposed += TargetDisposed;
            panel.ParentChanged += ParentChanged;
            AttachAncestors();
        }
        internal void Reveal(int delay)
        {
            if (disposed || !entranceEnabled) return;
            Reset();
            foreach (var child in hooked.OfType<AntdUI.Panel>().Where(panel => panel != target).ToArray())
                if (bindings.TryGetValue(child, out var binding)) binding.Reset();
            if (!AntdUI.Config.Animation || PageMotionCoordinator.Current == null || !target.Visible) return;
            entrance = 0;
            UpdateOverlay();
            entryMotion.Run(1250, p =>
            {
                entrance = p; UpdateOverlay();
                if (p == 1) ResumeDeferredChildren();
            }, delay, SelectMotionMath.EaseInOut);
        }
        internal void SetHovered(bool value)
        {
            if (disposed || !hoverEnabled || !target.Enabled) return;
            ClearDeferredHover();
            if (value)
                for (var parent = target.Parent; parent != null; parent = parent.Parent)
                    if (parent is AntdUI.Panel panel && bindings.TryGetValue(panel, out var ancestor)
                        && ancestor.entrance < 1)
                    {
                        deferredParent = ancestor; ancestor.deferredChildren.Add(this);
                        return;
                    }
            if (hovered == value) return;
            hovered = value;
            float destination = value ? 1 : 0;
            float fromFill = fill, fromZoom = zoom;
            fillMotion.Run(550, p => { fill = SelectMotionMath.Lerp(fromFill, destination, p); UpdateOverlay(); }, easing: SelectMotionMath.EaseInOut);
            zoomMotion.Run(500, p => { zoom = SelectMotionMath.Lerp(fromZoom, destination, p); UpdateOverlay(); }, easing: SelectMotionMath.EaseInOut);
        }
        private void ClearDeferredHover()
        {
            deferredParent?.deferredChildren.Remove(this);
            deferredParent = null;
        }
        private void ResumeDeferredChildren()
        {
            var pending = deferredChildren.ToArray(); deferredChildren.Clear();
            foreach (var child in pending)
            {
                child.deferredParent = null;
                if (!child.disposed && child.target.Visible && child.target.Enabled) child.SetHovered(true);
            }
        }
        internal void UpdateOverlay()
        {
            if (disposed) return;
            if (entrance == 1 && fill == 0 && zoom == 0 && !hovered && !IsRunning)
            { overlay?.Dispose(); overlay = null; return; }
            if (!target.IsHandleCreated || !target.Visible || target.Width <= 1 || target.Height <= 1) return;
            if (overlay == null)
            {
                AttachAncestors();
                Control host = target.Parent!;
                for (var current = host; current != null; current = current.Parent!)
                    if (current is ScrollableControl { AutoScroll: true }) { host = current.Parent ?? current; break; }
                addingOverlay = true;
                try
                {
                    overlay = new Overlay(this, host);
                    host.Controls.Add(overlay); overlay.BringToFront();
                }
                finally { addingOverlay = false; }
            }
            overlay.Invalidate();
        }
        private void Enter(object? sender, EventArgs e) => SetHovered(true);
        private void Leave(object? sender, EventArgs e)
        {
            if (!target.IsHandleCreated || !target.RectangleToScreen(target.ClientRectangle).Contains(Cursor.Position)) SetHovered(false);
        }
        private void Attach(Control control)
        {
            if (!hooked.Add(control)) return;
            if (hoverEnabled) { control.MouseEnter += Enter; control.MouseLeave += Leave; }
            control.ControlAdded += Added; control.ControlRemoved += Removed;
            control.Invalidated += Invalidated;
            control.TextChanged += ContentChanged; control.FontChanged += ContentChanged;
            control.ForeColorChanged += ContentChanged; control.BackColorChanged += ContentChanged;
            control.SizeChanged += GeometryChanged; control.LocationChanged += GeometryChanged;
            control.VisibleChanged += VisibilityChanged;
            foreach (Control child in control.Controls) Attach(child);
        }
        private void Detach(Control control)
        {
            foreach (Control child in control.Controls) Detach(child);
            if (!hooked.Remove(control)) return;
            control.MouseEnter -= Enter; control.MouseLeave -= Leave;
            control.ControlAdded -= Added; control.ControlRemoved -= Removed;
            control.Invalidated -= Invalidated;
            control.TextChanged -= ContentChanged; control.FontChanged -= ContentChanged;
            control.ForeColorChanged -= ContentChanged; control.BackColorChanged -= ContentChanged;
            control.SizeChanged -= GeometryChanged; control.LocationChanged -= GeometryChanged;
            control.VisibleChanged -= VisibilityChanged;
            if (control is Label label && labels.Remove(label, out var raster)) raster.Dispose();
        }
        private void Added(object? sender, ControlEventArgs e) { if (e.Control != null) Attach(e.Control); overlay?.Invalidate(); }
        private void Removed(object? sender, ControlEventArgs e) { if (e.Control != null) Detach(e.Control); overlay?.Invalidate(); }
        private void Invalidated(object? sender, InvalidateEventArgs e) => overlay?.Invalidate();
        private void ContentChanged(object? sender, EventArgs e)
        {
            if (sender is Label label && labels.Remove(label, out var raster)) raster.Dispose();
            overlay?.Invalidate();
        }
        private void VisibilityChanged(object? sender, EventArgs e)
        {
            if (!target.Visible) Reset(); else overlay?.Invalidate();
        }
        private void GeometryChanged(object? sender, EventArgs e)
        {
            if (sender == target && !addingOverlay) Reset();
            ClearLabels(); overlay?.Invalidate();
        }
        private void ParentChanged(object? sender, EventArgs e) { Reset(); AttachAncestors(); }
        private void AttachAncestors()
        {
            DetachAncestors();
            for (Control? control = target.Parent; control != null; control = control.Parent)
            {
                ancestors.Add(control);
                control.LocationChanged += AncestorChanged; control.SizeChanged += AncestorChanged;
                control.VisibleChanged += AncestorChanged;
                if (control is ScrollableControl scrolling) scrolling.Scroll += Scrolled;
            }
            owner = target.FindForm();
            if (owner != null) owner.Resize += OwnerChanged;
        }
        private void DetachAncestors()
        {
            foreach (var control in ancestors)
            {
                control.LocationChanged -= AncestorChanged; control.SizeChanged -= AncestorChanged;
                control.VisibleChanged -= AncestorChanged;
                if (control is ScrollableControl scrolling) scrolling.Scroll -= Scrolled;
            }
            ancestors.Clear();
            if (owner != null) owner.Resize -= OwnerChanged;
            owner = null;
        }
        private void AncestorChanged(object? sender, EventArgs e) { if (!addingOverlay) Reset(); }
        private void Scrolled(object? sender, ScrollEventArgs e) => Reset();
        private void OwnerChanged(object? sender, EventArgs e) { if (owner?.WindowState == FormWindowState.Minimized) Reset(); }
        private void Reset()
        {
            ClearDeferredHover();
            foreach (var child in deferredChildren.ToArray()) child.ClearDeferredHover();
            entryMotion.Cancel(false); fillMotion.Cancel(false); zoomMotion.Cancel(false);
            entrance = 1; fill = zoom = 0; hovered = false;
            overlay?.Dispose(); overlay = null;
        }
        private void ClearLabels() { foreach (var raster in labels.Values) raster.Dispose(); labels.Clear(); }
        internal void PaintChildren(Control parent, Graphics graphics, Action<Label, Graphics> paintNativeLabel)
        {
            foreach (Control child in parent.Controls.Cast<Control>().Reverse())
            {
                if (!child.Visible || child.Width <= 0 || child.Height <= 0) continue;
                var state = graphics.Save(); graphics.TranslateTransform(child.Left, child.Top);
                graphics.SetClip(child.ClientRectangle, CombineMode.Intersect);
                if (child is Label) { /* Text is rasterized at the final zoom after the backdrop. */ }
                else if (child is AnimatedDashboardIcon icon) icon.PaintForMotion(graphics);
                else
                {
                    if (child is AntdUI.Panel panel && panel.Back is { } color && color.ToArgb() != Color.White.ToArgb())
                    {
                        using var path = AppTheme.Round(new RectangleF(0, 0, panel.Width - 1, panel.Height - 1), panel.Radius * AppTheme.K);
                        using var brush = new SolidBrush(color); graphics.FillPath(brush, path);
                    }
                    PaintChildren(child, graphics, paintNativeLabel);
                }
                graphics.Restore(state);
            }
        }
        internal void PaintText(Control parent, Graphics graphics, Point offset, PointF origin, float scale,
            float opacity, ImageAttributes alpha, Action<Label, Graphics> paintNativeLabel)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible || child.Width <= 0 || child.Height <= 0) continue;
                var location = new Point(offset.X + child.Left, offset.Y + child.Top);
                if (child is Label label)
                {
                    if (!labels.TryGetValue(label, out var raster) || !raster.Matches(label, scale, graphics.DpiX, graphics.DpiY))
                    {
                        raster?.Dispose();
                        labels.Remove(label);
                        raster = new(label, scale, graphics.DpiX, graphics.DpiY, paintNativeLabel);
                        labels.Add(label, raster); cacheBuilds++;
                    }
                    var at = Point.Round(new PointF(origin.X + location.X * scale, origin.Y + location.Y * scale));
                    if (opacity == 1) graphics.DrawImageUnscaled(raster.bitmap, at);
                    else graphics.DrawImage(raster.bitmap, new Rectangle(at, raster.bitmap.Size),
                        0, 0, raster.bitmap.Width, raster.bitmap.Height, GraphicsUnit.Pixel, alpha);
                }
                else PaintText(child, graphics, location, origin, scale, opacity, alpha, paintNativeLabel);
            }
        }
        private void TargetDisposed(object? sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            if (disposed) return;
            Reset(); disposed = true;
            target.Disposed -= TargetDisposed; target.ParentChanged -= ParentChanged;
            Detach(target); DetachAncestors(); ClearLabels();
            entryMotion.Dispose(); fillMotion.Dispose(); zoomMotion.Dispose(); bindings.Remove(target);
        }
    }
    private sealed class LabelRaster : IDisposable
    {
        internal readonly Bitmap bitmap;
        private readonly string? text;
        private readonly Font font;
        private readonly Color color;
        private readonly ContentAlignment alignment;
        private readonly bool multiline, ellipsis;
        private readonly float scale;
        internal LabelRaster(Label label, float scale, float dpiX, float dpiY, Action<Label, Graphics> paintNativeLabel)
        {
            this.scale = scale;
            text = label.Text; font = label.Font; color = label.ForeColor ?? AppTheme.Ink; alignment = label.TextAlign;
            multiline = label.TextMultiLine; ellipsis = label.AutoEllipsis;
            bitmap = new((int)Math.Ceiling(label.Width * scale), (int)Math.Ceiling(label.Height * scale), PixelFormat.Format32bppPArgb);
            try
            {
                bitmap.SetResolution(dpiX, dpiY);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.Transparent); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.ScaleTransform(scale, scale);
                paintNativeLabel(label, graphics);
            }
            catch { bitmap.Dispose(); throw; }
        }
        internal bool Matches(Label label, float zoom, float dpiX, float dpiY) => scale == zoom && text == label.Text && font == label.Font && color == (label.ForeColor ?? AppTheme.Ink) &&
            alignment == label.TextAlign && multiline == label.TextMultiLine && ellipsis == label.AutoEllipsis &&
            bitmap.Width == (int)Math.Ceiling(label.Width * zoom) && bitmap.Height == (int)Math.Ceiling(label.Height * zoom) &&
            Math.Abs(bitmap.HorizontalResolution - dpiX) < .01f && Math.Abs(bitmap.VerticalResolution - dpiY) < .01f;
        public void Dispose() => bitmap.Dispose();
    }
    private sealed class Overlay : Control
    {
        private readonly Binding binding;
        private readonly Bitmap background, frame;
        private readonly ImageAttributes alpha = new();
        private readonly ColorMatrix matrix = new();
        private readonly Action<Label, Graphics> paintNativeLabel;
        private readonly Point origin;
        private bool released;
        internal Overlay(Binding owner, Control host)
        {
            binding = owner;
            paintNativeLabel = PaintNativeLabel;
            var target = binding.target;
            var position = host.PointToClient(target.PointToScreen(Point.Empty));
            int padX = (int)Math.Ceiling(target.Width * .025f);
            int padY = (int)Math.Ceiling(target.Height * .025f) + (binding.entranceEnabled ? AppTheme.S(28) : 0);
            var rectangle = new Rectangle(position.X - padX, position.Y - padY, target.Width + padX * 2, target.Height + padY * 2);
            rectangle.Intersect(host.ClientRectangle);
            for (Control? ancestor = target.Parent; ancestor != null && ancestor != host; ancestor = ancestor.Parent)
            {
                if (ancestor is ScrollableControl { AutoScroll: true })
                {
                    var viewport = new Rectangle(host.PointToClient(ancestor.PointToScreen(Point.Empty)), ancestor.ClientSize);
                    // A fully visible card may zoom into the empty horizontal canvas gutter.
                    // Partially scrolled cards still clip exactly at the viewport edge.
                    if (position.X >= viewport.Left && position.X + target.Width <= viewport.Right)
                        viewport.Inflate(padX, 0);
                    rectangle.Intersect(viewport);
                }
                foreach (Control sibling in ancestor.Controls)
                {
                    if (!sibling.Visible || sibling == target || sibling.Contains(target) || sibling is Overlay) continue;
                    var point = host.PointToClient(sibling.PointToScreen(Point.Empty));
                    var area = new Rectangle(point, sibling.Size);
                    if (area.Bottom > position.Y && area.Top < position.Y + target.Height)
                    {
                        if (area.Right <= position.X && area.Right > rectangle.Left)
                            rectangle = Rectangle.FromLTRB(area.Right, rectangle.Top, rectangle.Right, rectangle.Bottom);
                        if (area.Left >= position.X + target.Width && area.Left < rectangle.Right)
                            rectangle = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, area.Left, rectangle.Bottom);
                    }
                    if (area.Right <= rectangle.Left || area.Left >= rectangle.Right) continue;
                    if (area.Bottom <= position.Y && area.Bottom > rectangle.Top)
                        rectangle = Rectangle.FromLTRB(rectangle.Left, area.Bottom, rectangle.Right, rectangle.Bottom);
                    if (area.Top >= position.Y + target.Height && area.Top < rectangle.Bottom)
                        rectangle = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, area.Top);
                }
            }
            Bounds = rectangle;
            origin = new(position.X - Left, position.Y - Top);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false; Enabled = false; BackColor = AppTheme.Canvas;
            AccessibleRole = AccessibleRole.None;
            background = new(Width, Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(background))
            {
                g.Clear(AppTheme.Canvas); g.SmoothingMode = SmoothingMode.AntiAlias;
                PaintBackdrop(host, host, g);
                using var original = AppTheme.Round(new RectangleF(origin.X, origin.Y, target.Width, target.Height), target.Radius * AppTheme.K);
                Color underlay = target.Parent!.BackColor;
                if (underlay.A < 255) underlay = AppTheme.Canvas;
                using var canvas = new SolidBrush(underlay); g.FillPath(canvas, original);
            }
            frame = new(target.Width, target.Height, PixelFormat.Format32bppPArgb);
            overlays.Add(this);
        }
        private void PaintBackdrop(Control current, Control host, Graphics graphics)
        {
            foreach (Control child in current.Controls.Cast<Control>().Reverse())
            {
                if (!child.Visible || child == binding.target || child is Overlay) continue;
                var position = host.PointToClient(child.PointToScreen(Point.Empty));
                var rectangle = new Rectangle(position.X - Left, position.Y - Top, child.Width, child.Height);
                if (!rectangle.IntersectsWith(ClientRectangle)) continue;
                if (child is AntdUI.Panel panel)
                {
                    using var shape = new GraphicsPath();
                    if (panel.Radius <= 0) shape.AddRectangle(rectangle);
                    else { using var rounded = AppTheme.Round(rectangle, panel.Radius * AppTheme.K); shape.AddPath(rounded, false); }
                    if (panel.BackExtend == AppTheme.HeroGradient)
                    {
                        using var gradient = new LinearGradientBrush(rectangle, ColorTranslator.FromHtml("#E2D6FC"), AppTheme.Peach, 0f);
                        graphics.FillPath(gradient, shape);
                    }
                    else { using var brush = new SolidBrush(panel.Back ?? Color.White); graphics.FillPath(brush, shape); }
                }
                PaintBackdrop(child, host, graphics);
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e) => PaintFrame(e.Graphics);
        private void PaintNativeLabel(Label label, Graphics graphics) => InvokePaint(label, new PaintEventArgs(graphics, label.ClientRectangle));
        internal void PaintFrame(Graphics graphics)
        {
            var target = binding.target;
            graphics.DrawImageUnscaled(background, Point.Empty);
            frame.SetResolution(graphics.DpiX, graphics.DpiY);
            using (var g = Graphics.FromImage(frame))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                using var shape = AppTheme.Round(new RectangleF(0, 0, frame.Width - 1, frame.Height - 1), target.Radius * AppTheme.K);
                using var white = new SolidBrush(Color.White); g.FillPath(white, shape);
                var state = g.Save(); g.SetClip(shape, CombineMode.Intersect);
                if (binding.fill > 0)
                {
                    var ellipse = AuthMotionMath.Ellipse(frame.Width, frame.Height, binding.fill);
                    using var brush = new SolidBrush(binding.tone);
                    g.FillEllipse(brush, ellipse.X, ellipse.Y, ellipse.Width, ellipse.Height);
                }
                binding.PaintChildren(target, g, paintNativeLabel); g.Restore(state);
            }
            float scale = AuthMotionMath.Scale(binding.zoom);
            var destination = new RectangleF(origin.X + (frame.Width - frame.Width * scale) / 2,
                origin.Y + (frame.Height - frame.Height * scale) / 2 + AuthMotionMath.EntranceOffset(binding.entrance, 25 * AppTheme.K),
                frame.Width * scale, frame.Height * scale);
            if (scale == 1 && binding.entrance == 1)
            {
                graphics.DrawImageUnscaled(frame, origin);
            }
            else
            {
                matrix.Matrix33 = binding.entrance; alpha.SetColorMatrix(matrix);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                PointF[] corners = [new(destination.Left, destination.Top), new(destination.Right, destination.Top), new(destination.Left, destination.Bottom)];
                graphics.DrawImage(frame, corners, new RectangleF(0, 0, frame.Width, frame.Height), GraphicsUnit.Pixel, alpha);
            }
            var textState = graphics.Save();
            using var textClip = AppTheme.Round(destination, target.Radius * AppTheme.K * scale);
            graphics.SetClip(textClip, CombineMode.Intersect);
            binding.PaintText(target, graphics, Point.Empty, destination.Location, scale, binding.entrance, alpha, paintNativeLabel);
            graphics.Restore(textState);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !released) { released = true; overlays.Remove(this); background.Dispose(); frame.Dispose(); alpha.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
