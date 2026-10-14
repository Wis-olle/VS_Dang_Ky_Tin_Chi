namespace QuanLyGiangVien.Ui;

/// <summary>Fixed native click surface; child artwork and labels share its action.</summary>
internal class DashboardActionPanel : AntdUI.Panel
{
    private readonly List<Control> clickTargets = [];
    private bool keyPressed;
    internal DashboardActionPanel()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.PushButton;
    }
    internal void BindChildClicks()
    {
        void Bind(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (!clickTargets.Contains(child)) { clickTargets.Add(child); child.Click += ChildClick; }
                Bind(child);
            }
        }
        Bind(this);
    }
    private void ChildClick(object? sender, EventArgs e)
    {
        if (sender is Control { Enabled: true, Visible: true }) OnClick(e);
    }
    protected override void OnClick(EventArgs e)
    {
        if (!Enabled || !Visible || IsDisposed || Disposing) return;
        base.OnClick(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Modifiers != Keys.None || e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        bool activate = !keyPressed && e.KeyCode == Keys.Enter;
        keyPressed = true; e.Handled = e.SuppressKeyPress = true;
        if (activate) OnClick(EventArgs.Empty);
    }
    protected override bool IsInputKey(Keys keyData) =>
        ((keyData & Keys.Modifiers) == Keys.None && (keyData & Keys.KeyCode) is Keys.Enter or Keys.Space)
        || base.IsInputKey(keyData);
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        bool activate = keyPressed && e.KeyCode == Keys.Space && e.Modifiers == Keys.None;
        keyPressed = false; e.Handled = true;
        if (activate) OnClick(EventArgs.Empty);
    }
    protected override void OnLostFocus(EventArgs e) { keyPressed = false; base.OnLostFocus(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Focused && ShowFocusCues && Width > 8 && Height > 8)
            ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8), AppTheme.Primary, Color.White);
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new ActionAccessibility(this);
    private sealed class ActionAccessibility(DashboardActionPanel owner) : ControlAccessibleObject(owner)
    {
        public override string DefaultAction => "Mở";
        public override void DoDefaultAction() => owner.OnClick(EventArgs.Empty);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { foreach (var child in clickTargets) child.Click -= ChildClick; clickTargets.Clear(); }
        base.Dispose(disposing);
    }
}
