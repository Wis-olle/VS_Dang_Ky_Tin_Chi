using AntdUI;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Fixed native tabs with a finite sliding indicator inspired by SmoothUI Animated Tabs.</summary>
internal sealed class SchedulingTabs : UserControl
{
    private readonly NavigationButton[] buttons;
    private readonly Font labelFont = AppTheme.Font(8.5f);
    private readonly Font selectedFont = AppTheme.Font(8.5f, true);
    private readonly Motion underlineMotion = new();
    private readonly string[] quietIcons = new[] { "book-open", "door-open", "calendar-days" }
        .Select(key => SchedulingTheme.Icon(key, AppTheme.Muted)).ToArray();
    private readonly string[] activeIcons = new[] { "book-open", "door-open", "calendar-days" }
        .Select(key => SchedulingTheme.Icon(key, AppTheme.Primary)).ToArray();
    private RectangleF underline, underlineTarget;
    private int selectedIndex;
    internal event EventHandler? SelectIndexChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int SelectIndex
    {
        get => selectedIndex;
        set
        {
            int next = Math.Clamp(value, 0, 2);
            if (next == selectedIndex) return;
            selectedIndex = next; UpdateSelection(true);
            SelectIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal SchedulingTabs()
    {
        BackColor = Color.White;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        buttons = new[] { "Lớp học phần", "Phòng học", "Lịch tuần" }.Select((text, index) => {
            var button = new NavigationButton {
                Text = text, Font = labelFont, Radius = 8, BorderWidth = 0,
                DefaultBack = Color.White, DefaultBorderColor = Color.Transparent,
                BackHover = AppTheme.Canvas, BackActive = AppTheme.AccentSoft,
                IconSvg = quietIcons[index], IconSize = new Size(14, 14),
                AccessibleName = text, TabStop = true
            };
            button.Click += (_, _) => SelectIndex = index;
            button.KeyDown += (_, e) => {
                if (e.KeyCode is not (Keys.Left or Keys.Right or Keys.Home or Keys.End)) return;
                int next = e.KeyCode switch { Keys.Left => SelectIndex - 1, Keys.Right => SelectIndex + 1,
                    Keys.Home => 0, Keys.End => 2, _ => SelectIndex };
                SelectIndex = next; FocusSelectedTab(); e.Handled = true; e.SuppressKeyPress = true;
            };
            return button;
        }).ToArray();
        Controls.AddRange(buttons);
        Resize += (_, _) => {
            int width = Math.Max(1, Width / 3);
            for (int i = 0; i < buttons.Length; i++) buttons[i].SetBounds(i * width, 0,
                i == 2 ? Width - i * width : width, Math.Max(1, Height - AppTheme.S(5)));
            MoveUnderline(false);
        };
        UpdateSelection(false);
    }

    private void UpdateSelection(bool animate)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].ForeColor = i == selectedIndex ? AppTheme.Primary : AppTheme.Muted;
            buttons[i].Font = i == selectedIndex ? selectedFont : labelFont;
            buttons[i].IconSvg = i == selectedIndex ? activeIcons[i] : quietIcons[i];
            buttons[i].TabStop = i == selectedIndex;
        }
        MoveUnderline(animate);
    }

    private RectangleF MeasureUnderline()
    {
        var active = buttons[selectedIndex];
        var text = TextRenderer.MeasureText(active.Text, active.Font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        // AntdUI sizes IconSize in logical pixels and its gap as a fraction of the native text height.
        int labelWidth = text.Width + AppTheme.S(14) + (int)Math.Ceiling(text.Height * active.IconGap);
        int width = Math.Min(Math.Max(1, active.Width - AppTheme.S(20)), labelWidth + AppTheme.S(8));
        float thickness = Math.Max(1, AppTheme.S(2));
        return new(active.Left + (active.Width - width) / 2f, Height - AppTheme.S(3), width, thickness);
    }

    private void MoveUnderline(bool animate)
    {
        underlineMotion.Cancel(false);
        underlineTarget = MeasureUnderline();
        if (!animate || !Visible || !Enabled || !IsHandleCreated || underline.IsEmpty)
        { underline = underlineTarget; Invalidate(); return; }
        var from = underline;
        var target = underlineTarget;
        // Retarget from the current float rectangle during rapid reversal; native buttons never move.
        underlineMotion.Run(220, p => {
            underline = new(from.X + (target.X - from.X) * p, target.Y,
                from.Width + (target.Width - from.Width) * p, target.Height);
            Invalidate();
        });
    }

    internal RectangleF UnderlineRectForCheck() => underline;
    internal RectangleF UnderlineTargetForCheck() => underlineTarget;
    internal bool UnderlineMotionRunningForCheck() => underlineMotion.IsRunning;
    internal bool RovingTabStopForCheck() => buttons.Count(button => button.TabStop) == 1 && buttons[selectedIndex].TabStop;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (buttons != null) MoveUnderline(false);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (buttons != null && !Enabled) MoveUnderline(false);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        underlineMotion.Cancel();
        base.OnHandleDestroyed(e);
    }

    private void FocusSelectedTab() => buttons[SelectIndex].Focus();
    internal bool AcceptsNavigationKeysForCheck => buttons.All(button => button.AcceptsNavigationKeys);
    internal void NavigateForCheck(Keys key) => buttons[SelectIndex].DispatchNavigation(key);

    private sealed class NavigationButton : AntdUI.Button
    {
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is
            Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);
        internal bool AcceptsNavigationKeys => new[] { Keys.Left, Keys.Right, Keys.Home, Keys.End }.All(IsInputKey);
        internal void DispatchNavigation(Keys key)
        {
            var message = System.Windows.Forms.Message.Create(Handle, 0x0100, (IntPtr)(int)key, IntPtr.Zero);
            WndProc(ref message);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (underline.Width <= 0 || underline.Height <= 0) return;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = AppTheme.Round(underline, underline.Height / 2);
        using var fill = new SolidBrush(AppTheme.Primary);
        e.Graphics.FillPath(fill, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) underlineMotion.Dispose();
        base.Dispose(disposing);
        if (disposing) { labelFont.Dispose(); selectedFont.Dispose(); }
    }
}
