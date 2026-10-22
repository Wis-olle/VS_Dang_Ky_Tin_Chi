using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace QuanLyGiangVien.Ui;

/// <summary>Native adaptation of the animated-select interaction referenced in ASSET_SOURCES.md.</summary>
internal sealed class AnimatedSelect : Control
{
    private static AnimatedSelect? active;
    private readonly Motion caretMotion = new(), labelMotion = new(), hoverMotion = new();
    private string[] items = [];
    private Bitmap[] labels = [];
    private int selectedIndex = -1;
    private float labelPosition, caretProgress, hover;
    private bool opened;
    private string searchPrefix = "";
    private long lastSearchTick;
    private SelectPopup? popup;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Radius { get; set; } = 10;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color BorderColor { get; set; } = AppTheme.Line;
    internal IReadOnlyList<string> Items => Array.AsReadOnly(items);
    internal string? SelectedValue => selectedIndex < 0 ? null : items[selectedIndex];
    internal bool IsDropDownOpen => opened;
    internal bool HasPopup => popup is { IsDisposed: false };
    internal float CaretProgress => caretProgress;
    internal float LabelPosition => labelPosition;
    internal bool HasRunningMotion => caretMotion.IsRunning || labelMotion.IsRunning || hoverMotion.IsRunning || popup?.IsAnimating == true;
    internal SelectPopup? PopupForCheck => popup;
    public event EventHandler? SelectedIndexChanged;

    internal AnimatedSelect()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.White;
        ForeColor = AppTheme.Ink;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.ComboBox;
    }
    internal void SetItems(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var replacement = values.ToArray();
        if (replacement.Length == 0 || replacement.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Select options must contain non-empty labels.", nameof(values));
        CloseDropDown(true);
        items = replacement;
        selectedIndex = -1;
        SelectedIndex = 0;
        ClearLabels();
    }
    // Binding adapters mirror a native source without selecting its first item or firing user events.
    internal void SetSnapshot(IReadOnlyList<string> values, int selection)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (selection < -1 || selection >= values.Count) throw new ArgumentOutOfRangeException(nameof(selection));
        if (selectedIndex == selection && items.SequenceEqual(values)) return;
        CloseDropDown(true);
        labelMotion.Cancel(false);
        items = values.ToArray(); selectedIndex = selection;
        Text = selection < 0 ? "" : items[selection]; labelPosition = Math.Max(0, selection);
        ClearLabels(); Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < 0 || value >= items.Length) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == selectedIndex) return;
            bool initial = selectedIndex < 0;
            selectedIndex = value;
            Text = items[value]; // Accessible value follows the data immediately, independent of the paint animation.
            if (initial || !Visible || !IsHandleCreated) { labelMotion.Cancel(false); labelPosition = value; }
            else
            {
                float from = labelPosition;
                labelMotion.Run(300, progress => { labelPosition = SelectMotionMath.Lerp(from, value, progress); Invalidate(); },
                    easing: SelectMotionMath.EaseInOut);
            }
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    internal void OpenDropDown()
    {
        if (!Enabled || items.Length == 0 || FindForm() is not { Visible: true } owner) return;
        if (opened) return;
        active?.CloseDropDown(true);
        active = this;
        opened = true;
        Focus();
        AnimateCaret(1);
        if (popup is { IsDisposed: false }) popup.Open();
        else
        {
            popup = new SelectPopup(this, owner);
            popup.FormClosed += PopupClosed;
            popup.Open();
        }
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        Invalidate();
    }
    internal void CloseDropDown(bool immediately = false)
    {
        if (!opened && popup == null && caretProgress == 0) return;
        opened = false;
        if (active == this) active = null;
        if (immediately) { caretMotion.Cancel(false); caretProgress = 0; }
        else AnimateCaret(0);
        popup?.Dismiss(immediately);
        if (!IsDisposed) { Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); }
    }
    private void PopupClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not SelectPopup closed) return;
        closed.FormClosed -= PopupClosed;
        if (popup != closed) return;
        popup = null;
        if (opened) CloseDropDown(true);
    }
    private void AnimateCaret(float target)
    {
        float from = caretProgress;
        caretMotion.Run(200, p => { caretProgress = SelectMotionMath.Lerp(from, target, p); Invalidate(); },
            easing: SelectMotionMath.EaseInOut);
    }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (opened) CloseDropDown(); else OpenDropDown();
    }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (HandleSelectionKey(keyData)) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }
    internal bool HandleSelectionKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        if (!Enabled || items.Length == 0) return false;
        if (key == Keys.Tab) { CloseDropDown(true); return false; }
        if (key == Keys.Escape && opened) { CloseDropDown(); return true; }
        if (key == Keys.F4) { if (opened) CloseDropDown(); else OpenDropDown(); return true; }
        if (key is Keys.Enter or Keys.Space)
        {
            if (opened) popup?.CommitHighlighted(); else OpenDropDown();
            return true;
        }
        if (key is not (Keys.Up or Keys.Down or Keys.Home or Keys.End)) return false;
        if (opened) popup?.Navigate(key);
        else if ((keyData & Keys.Alt) != 0) OpenDropDown();
        else SelectedIndex = key switch
        {
            Keys.Home => 0, Keys.End => items.Length - 1,
            Keys.Up => Math.Max(0, selectedIndex - 1), _ => Math.Min(items.Length - 1, selectedIndex + 1)
        };
        return true;
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); SetHover(1); }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!Enabled || items.Length == 0 || char.IsControl(e.KeyChar) || e.KeyChar == ' ') return;
        long now = Environment.TickCount64;
        bool continuing = now - lastSearchTick <= 900 && searchPrefix.Length > 0;
        searchPrefix = continuing ? searchPrefix + e.KeyChar : e.KeyChar.ToString();
        lastSearchTick = now;
        int start = opened ? popup?.HighlightedIndex ?? selectedIndex : selectedIndex;
        int Find(string prefix, bool includeCurrent) => Enumerable.Range(0, items.Length)
            .Select(n => ((includeCurrent && start >= 0 ? start : start + 1) + n) % items.Length)
            .FirstOrDefault(i => items[i].StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase), -1);
        int found = Find(searchPrefix, continuing);
        if (found < 0) { searchPrefix = e.KeyChar.ToString(); found = Find(searchPrefix, false); }
        if (found >= 0) { if (opened) popup?.Highlight(found); else SelectedIndex = found; }
        e.Handled = true;
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHover(0); }
    private void SetHover(float target)
    {
        float from = hover;
        hoverMotion.Run(200, p => { hover = SelectMotionMath.Lerp(from, target, p); Invalidate(); });
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); CloseDropDown(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) CloseDropDown(true); ClearLabels(); Invalidate(); }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); ClearLabels(); Invalidate(); }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { CloseDropDown(true); labelMotion.Cancel(); hoverMotion.Cancel(false); hover = 0; }
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); GeometryChanged(); }
    protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); CloseDropDown(true); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); GeometryChanged(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); GeometryChanged(); }
    private void GeometryChanged() { CloseDropDown(true); ClearLabels(); Invalidate(); }
    internal RectangleF FaceBounds => new(4 * AppTheme.K, 4 * AppTheme.K,
        Math.Max(1, Width - 8 * AppTheme.K), Math.Max(1, Height - 8 * AppTheme.K));
    private RectangleF TextBounds => new(FaceBounds.X + 7 * AppTheme.K, FaceBounds.Y,
        Math.Max(1, FaceBounds.Width - 29 * AppTheme.K), FaceBounds.Height);
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var face = FaceBounds;
        if (Focused || opened)
        {
            var halo = face; halo.Inflate(3 * AppTheme.K, 3 * AppTheme.K);
            using var path = AppTheme.Round(halo, (Radius + 3) * AppTheme.K);
            using var tint = new SolidBrush(Color.FromArgb(100, AppTheme.AccentSoft));
            g.FillPath(tint, path);
        }
        using (var path = AppTheme.Round(face, Radius * AppTheme.K))
        using (var fill = new SolidBrush(Enabled ? Color.White : AppTheme.SubtleLilac))
        using (var tint = new SolidBrush(Color.FromArgb((int)(hover * 95), AppTheme.Canvas)))
        using (var border = new Pen(opened || Focused ? AppTheme.HoverLine : BorderColor, AppTheme.K))
        { g.FillPath(fill, path); g.FillPath(tint, path); g.DrawPath(border, path); }
        PaintLabels(g);
        var state = g.Save();
        g.TranslateTransform(face.Right - 12 * AppTheme.K, face.Top + face.Height / 2);
        g.RotateTransform(caretProgress * 180);
        using (var pen = new Pen(Enabled ? Color.FromArgb(175, AppTheme.Muted) : AppTheme.Line, 1.5f * AppTheme.K)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(pen, new PointF[] { new(-3.4f * AppTheme.K, -1.7f * AppTheme.K), new(0, 1.7f * AppTheme.K), new(3.4f * AppTheme.K, -1.7f * AppTheme.K) });
        g.Restore(state);
    }
    private void PaintLabels(Graphics g)
    {
        if (selectedIndex < 0) return;
        var area = TextBounds;
        if (labels.Length != items.Length) BuildLabels(g, area.Size);
        var state = g.Save();
        g.SetClip(area, CombineMode.Intersect);
        float offset = SelectMotionMath.ListOffset(labelPosition, area.Height, 8 * AppTheme.K);
        for (int i = 0; i < labels.Length; i++)
        {
            float y = area.Y + i * (area.Height + 8 * AppTheme.K) + offset;
            if (y + area.Height <= area.Top || y >= area.Bottom) continue;
            g.DrawImage(labels[i], new RectangleF(area.X, y, area.Width, area.Height),
                new RectangleF(0, 0, labels[i].Width, labels[i].Height), GraphicsUnit.Pixel);
        }
        g.Restore(state);
    }
    private void BuildLabels(Graphics g, SizeF size)
    {
        ClearLabels();
        labels = items.Select(text => RenderLabel(g, text, size)).ToArray();
    }
    private Bitmap RenderLabel(Graphics target, string text, SizeF size)
    {
        var bitmap = new Bitmap(Math.Max(1, (int)Math.Ceiling(size.Width)), Math.Max(1, (int)Math.Ceiling(size.Height)));
        bitmap.SetResolution(target.DpiX, target.DpiY);
        using var g = Graphics.FromImage(bitmap);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(Enabled ? ForeColor : AppTheme.Muted);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, Font, brush, new RectangleF(0, 0, size.Width, size.Height), format);
        return bitmap;
    }
    private void ClearLabels() { foreach (var bitmap in labels) bitmap.Dispose(); labels = []; }
    protected override AccessibleObject CreateAccessibilityInstance() => new SelectAccessibleObject(this);
    private sealed class SelectAccessibleObject(AnimatedSelect owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ComboBox;
        public override string? Value { get => owner.SelectedValue; set { } }
        public override string? DefaultAction => owner.opened ? "Đóng danh sách" : "Mở danh sách";
        public override AccessibleStates State => base.State | AccessibleStates.Focusable |
            (owner.opened ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
        public override void DoDefaultAction() { if (owner.opened) owner.CloseDropDown(); else owner.OpenDropDown(); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseDropDown(true);
            caretMotion.Dispose(); labelMotion.Dispose(); hoverMotion.Dispose(); ClearLabels();
        }
        base.Dispose(disposing);
    }
}
