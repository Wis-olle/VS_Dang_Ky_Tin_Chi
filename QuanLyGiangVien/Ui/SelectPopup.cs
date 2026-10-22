using System.Drawing.Drawing2D;

namespace QuanLyGiangVien.Ui;

/// <summary>A non-activating native popup; only its paint layer slides, keeping pointer bounds stable.</summary>
internal sealed class SelectPopup : Form, IMessageFilter
{
    private readonly AnimatedSelect anchor;
    private readonly Form owner;
    private readonly Motion reveal = new(), hoverMotion = new();
    private readonly List<Control> ancestors = [];
    private Bitmap[] labels = [];
    private float progress, hover, hoverTarget;
    private int highlighted, firstVisible, visibleRows;
    private bool dismissing, filterAttached;
    private float RowHeight => 28 * AppTheme.K;
    private float Inset => 4 * AppTheme.K;
    private float Slide => 10 * AppTheme.K;
    internal bool IsAnimating => reveal.IsRunning || hoverMotion.IsRunning;
    internal float RevealProgress => progress;
    internal int HighlightedIndex => highlighted;
    protected override bool ShowWithoutActivation => true;
    internal SelectPopup(AnimatedSelect control, Form form)
    {
        anchor = control; owner = form; highlighted = control.SelectedIndex;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        AccessibleName = anchor.AccessibleName;
        AccessibleRole = AccessibleRole.List;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    internal void Open()
    {
        dismissing = false;
        if (!Visible)
        {
            PlacePopup();
            if (AntdUI.Config.Animation && PageMotionCoordinator.Current != null) Opacity = .01;
            Show(owner);
        }
        if (!filterAttached) AttachHandlers();
        AnimateTo(1);
    }
    private void PlacePopup()
    {
        var face = anchor.FaceBounds;
        var topLeft = anchor.PointToScreen(new((int)face.Left, (int)face.Top));
        var work = Screen.FromControl(anchor).WorkingArea;
        int width = Math.Min((int)face.Width, work.Width);
        int desiredHeight = (int)Math.Ceiling(Math.Min(9, anchor.Items.Count) * RowHeight + Inset * 2 + Slide);
        int below = work.Bottom - topLeft.Y - (int)face.Height - AppTheme.S(8);
        int above = topLeft.Y - work.Top - AppTheme.S(8);
        bool useBelow = below >= desiredHeight || below >= above;
        int available = Math.Max(1, useBelow ? below : above);
        visibleRows = Math.Clamp((int)((available - Inset * 2 - Slide) / RowHeight), 1, Math.Min(9, anchor.Items.Count));
        int height = Math.Min(available, (int)Math.Ceiling(visibleRows * RowHeight + Inset * 2 + Slide));
        int y = useBelow ? topLeft.Y + (int)face.Height + AppTheme.S(8) - (int)Slide : topLeft.Y - AppTheme.S(8) - height;
        Bounds = new(Math.Clamp(topLeft.X, work.Left, work.Right - width), Math.Clamp(y, work.Top, work.Bottom - height), width, height);
        using (var shape = AppTheme.Round(new RectangleF(0, Slide, Width, Height - Slide), 10 * AppTheme.K))
            Region = new Region(shape);
        EnsureHighlightedVisible();
    }
    private void AnimateTo(float target)
    {
        float from = progress;
        reveal.Run(200, p =>
        {
            progress = SelectMotionMath.Lerp(from, target, p);
            Opacity = Math.Max(.01, progress);
            Invalidate();
            if (p >= 1 && dismissing) Close();
        });
    }
    internal void Dismiss(bool immediately)
    {
        if (IsDisposed) return;
        dismissing = true;
        DetachHandlers();
        hoverMotion.Cancel(false);
        if (immediately) { reveal.Cancel(false); Close(); }
        else AnimateTo(0);
    }
    private void AttachHandlers()
    {
        Application.AddMessageFilter(this); filterAttached = true;
        owner.Deactivate += OwnerChanged;
        owner.LocationChanged += OwnerChanged;
        owner.SizeChanged += OwnerChanged;
        for (Control? ancestor = anchor.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            ancestors.Add(ancestor);
            ancestor.LocationChanged += OwnerChanged;
            ancestor.SizeChanged += OwnerChanged;
            if (ancestor is ScrollableControl scrolling) scrolling.Scroll += AncestorScrolled;
        }
    }
    private void DetachHandlers()
    {
        if (filterAttached) { Application.RemoveMessageFilter(this); filterAttached = false; }
        owner.Deactivate -= OwnerChanged;
        owner.LocationChanged -= OwnerChanged;
        owner.SizeChanged -= OwnerChanged;
        foreach (var ancestor in ancestors)
        {
            ancestor.LocationChanged -= OwnerChanged;
            ancestor.SizeChanged -= OwnerChanged;
            if (ancestor is ScrollableControl scrolling) scrolling.Scroll -= AncestorScrolled;
        }
        ancestors.Clear();
    }
    private void OwnerChanged(object? sender, EventArgs e) => anchor.CloseDropDown(true);
    private void AncestorScrolled(object? sender, ScrollEventArgs e) => anchor.CloseDropDown(true);
    public bool PreFilterMessage(ref Message m)
    {
        if (dismissing || IsDisposed) return false;
        if (m.Msg is 0x0201 or 0x0204 or 0x0207 or 0x00A1 or 0x00A4 or 0x0246)
        {
            var target = Control.FromChildHandle(m.HWnd);
            if (!BelongsTo(target, this) && !BelongsTo(target, anchor)) anchor.CloseDropDown();
        }
        else if (m.Msg == 0x020A && !BelongsTo(Control.FromChildHandle(m.HWnd), this)) anchor.CloseDropDown(true);
        return false;
    }
    private static bool BelongsTo(Control? control, Control root)
    {
        for (var current = control; current != null; current = current.Parent)
            if (current == root) return true;
        return false;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE: keyboard focus remains on the trigger.
        base.WndProc(ref m);
    }
    internal void Navigate(Keys key)
    {
        highlighted = key switch
        {
            Keys.Home => 0, Keys.End => anchor.Items.Count - 1,
            Keys.Up => Math.Max(0, highlighted - 1), _ => Math.Min(anchor.Items.Count - 1, highlighted + 1)
        };
        EnsureHighlightedVisible(); SetHover(1); Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Focus, highlighted);
    }
    internal void Highlight(int index)
    {
        if (index < 0 || index >= anchor.Items.Count) return;
        highlighted = index; EnsureHighlightedVisible(); SetHover(1); Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Focus, highlighted);
    }
    private void EnsureHighlightedVisible() => firstVisible = Math.Clamp(
        highlighted < firstVisible ? highlighted : highlighted >= firstVisible + visibleRows ? highlighted - visibleRows + 1 : firstVisible,
        0, Math.Max(0, anchor.Items.Count - visibleRows));
    internal void CommitHighlighted()
    {
        if (dismissing) return;
        if (highlighted < 0 || highlighted >= anchor.Items.Count) { anchor.CloseDropDown(); return; }
        int chosen = highlighted;
        anchor.CloseDropDown();
        anchor.SelectedIndex = chosen;
    }
    private int RowAt(Point point)
    {
        // Final row geometry is fixed. Pointer commits are enabled only after the reveal settles.
        if (point.X < Inset || point.X >= Width - Inset || point.Y < Slide + Inset) return -1;
        int row = (int)((point.Y - Slide - Inset) / RowHeight);
        return row < visibleRows && row + firstVisible < anchor.Items.Count ? row + firstVisible : -1;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = RowAt(e.Location);
        if (index < 0) { SetHover(0); return; }
        if (index != highlighted) { highlighted = index; hoverMotion.Cancel(false); hover = 0; }
        SetHover(1);
    }
    private void SetHover(float target)
    {
        if ((hoverMotion.IsRunning && hoverTarget == target) || (!hoverMotion.IsRunning && Math.Abs(hover - target) < .001f)) return;
        hoverTarget = target;
        float from = hover;
        hoverMotion.Run(200, p => { hover = SelectMotionMath.Lerp(from, target, p); Invalidate(); });
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverMotion.Cancel(false); SetHover(0); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || dismissing || reveal.IsRunning) return;
        int index = RowAt(e.Location);
        if (index >= 0) { highlighted = index; CommitHighlighted(); }
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        firstVisible = Math.Clamp(firstVisible + (e.Delta < 0 ? 1 : -1), 0, Math.Max(0, anchor.Items.Count - visibleRows));
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float top = Slide - SelectMotionMath.PopupOffset(progress, Slide);
        var face = new RectangleF(.5f * AppTheme.K, top, Width - AppTheme.K, Height - Slide - AppTheme.K);
        using var path = AppTheme.Round(face, 10 * AppTheme.K);
        using var white = new SolidBrush(Color.White);
        using var border = new Pen(AppTheme.Line, AppTheme.K);
        g.FillPath(white, path); g.DrawPath(border, path);
        if (labels.Length == 0) BuildLabels(g);
        var state = g.Save(); g.SetClip(path, CombineMode.Intersect);
        for (int row = 0; row < visibleRows; row++)
        {
            int index = firstVisible + row;
            var rectangle = new RectangleF(Inset, top + Inset + row * RowHeight, Width - Inset * 2, RowHeight);
            if (index == anchor.SelectedIndex || index == highlighted)
            {
                using var round = AppTheme.Round(rectangle, 8 * AppTheme.K);
                using var brush = new SolidBrush(index == anchor.SelectedIndex ? Color.FromArgb(255, AppTheme.Canvas) : Color.FromArgb((int)(hover * 255), AppTheme.Canvas));
                g.FillPath(brush, round);
            }
            g.DrawImage(labels[index], new RectangleF(rectangle.X + 8 * AppTheme.K, rectangle.Y, labels[index].Width, labels[index].Height));
        }
        g.Restore(state);
    }
    private void BuildLabels(Graphics target)
    {
        labels = anchor.Items.Select(text =>
        {
            var bitmap = new Bitmap(Math.Max(1, Width - (int)(24 * AppTheme.K)), Math.Max(1, (int)Math.Ceiling(RowHeight)));
            bitmap.SetResolution(target.DpiX, target.DpiY);
            using var g = Graphics.FromImage(bitmap);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(AppTheme.Ink);
            using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(text, anchor.Font, brush, new RectangleF(0, 0, bitmap.Width, bitmap.Height), format);
            return bitmap;
        }).ToArray();
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new PopupAccessibleObject(this);
    private sealed class PopupAccessibleObject(SelectPopup popup) : ControlAccessibleObject(popup)
    {
        public override AccessibleRole Role => AccessibleRole.List;
        public override int GetChildCount() => popup.IsDisposed ? 0 : popup.anchor.Items.Count;
        public override AccessibleObject? GetChild(int index) => index >= 0 && index < GetChildCount() ? new OptionAccessibleObject(popup, index, this) : null;
        public override AccessibleObject? GetFocused() => GetChild(popup.highlighted);
        public override AccessibleObject? GetSelected() => GetChild(popup.anchor.SelectedIndex);
    }
    private sealed class OptionAccessibleObject(SelectPopup popup, int index, AccessibleObject parent) : AccessibleObject
    {
        private readonly string label = popup.anchor.Items[index];
        public override AccessibleObject Parent => parent;
        public override string? Name { get => label; set { } }
        public override AccessibleRole Role => AccessibleRole.ListItem;
        public override string? DefaultAction => "Chọn";
        public override Rectangle Bounds => popup.IsDisposed ? Rectangle.Empty : popup.RectangleToScreen(new((int)popup.Inset,
            (int)(popup.Slide + popup.Inset + (index - popup.firstVisible) * popup.RowHeight),
            (int)(popup.Width - popup.Inset * 2), (int)popup.RowHeight));
        public override AccessibleStates State => popup.IsDisposed ? AccessibleStates.Invisible : AccessibleStates.Selectable | AccessibleStates.Focusable |
            (index == popup.anchor.SelectedIndex ? AccessibleStates.Selected : AccessibleStates.None) |
            (index == popup.highlighted ? AccessibleStates.Focused : AccessibleStates.None) |
            (index < popup.firstVisible || index >= popup.firstVisible + popup.visibleRows ? AccessibleStates.Offscreen : AccessibleStates.None);
        public override void DoDefaultAction()
        {
            if (popup.dismissing || popup.IsDisposed) return;
            popup.highlighted = index;
            popup.CommitHighlighted();
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachHandlers(); reveal.Dispose(); hoverMotion.Dispose();
            foreach (var bitmap in labels) bitmap.Dispose(); labels = [];
        }
        base.Dispose(disposing);
    }
}
