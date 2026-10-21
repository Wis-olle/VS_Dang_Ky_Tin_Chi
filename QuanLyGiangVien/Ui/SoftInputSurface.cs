namespace QuanLyGiangVien.Ui;

/// <summary>Retains the original TextBox, its bindings, keyboard behavior and automation identity.</summary>
internal sealed class SoftInputSurface : SoftSurface
{
    private readonly Control input;
    private readonly NativeFrameBorder? nativeFrame;

    private SoftInputSurface(Control input)
    {
        this.input = input;
        Name = input.Name + "InputSurface";
        Radius = 9; BorderColor = AppTheme.Line; TabStop = false;
        if (input is TextBox text) text.BorderStyle = BorderStyle.None;
        if (input is ComboBox or DateTimePicker) nativeFrame = new(input);
        input.Dock = DockStyle.None; input.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        input.Margin = Padding.Empty; input.TabIndex = 0;
        Controls.Add(input);
        input.GotFocus += OnAppearanceChanged; input.LostFocus += OnAppearanceChanged;
        input.EnabledChanged += OnAppearanceChanged;
        if (input is TextBox box) box.ReadOnlyChanged += OnAppearanceChanged;
        input.FontChanged += OnInputFontChanged;
        SizeChanged += (_, _) => ArrangeInput();
        Click += (_, _) => input.Focus();
        UpdateAppearance();
    }

    internal static void Enhance(Control input)
    {
        if (input.Parent is null or SoftInputSurface || input.IsDisposed) return;
        var parent = input.Parent;
        int index = parent.Controls.GetChildIndex(input);
        var table = parent as TableLayoutPanel;
        var cell = table?.GetCellPosition(input);
        int columns = table?.GetColumnSpan(input) ?? 1, rows = table?.GetRowSpan(input) ?? 1;
        var bounds = input.Bounds; var dock = input.Dock; var anchor = input.Anchor;
        var margin = input.Margin; int tabIndex = input.TabIndex;
        int height = input is TextBox { Multiline: true } ? Math.Max(AppTheme.S(48), input.Height) : Math.Max(AppTheme.S(36), input.PreferredSize.Height + AppTheme.S(12));
        parent.SuspendLayout();
        try
        {
            parent.Controls.Remove(input);
            var surface = new SoftInputSurface(input)
            {
                Bounds = new(bounds.X, bounds.Y, bounds.Width, height), Dock = dock, Anchor = anchor,
                Margin = margin, TabIndex = tabIndex, MinimumSize = new(input.MinimumSize.Width, height),
                AccessibleName = input.AccessibleName
            };
            parent.Controls.Add(surface); parent.Controls.SetChildIndex(surface, index);
            if (table is not null && cell is {} position)
            { table.SetCellPosition(surface, position); table.SetColumnSpan(surface, columns); table.SetRowSpan(surface, rows); }
            surface.ArrangeInput();
        }
        finally { parent.ResumeLayout(true); }
    }

    private void ArrangeInput()
    {
        if (input.IsDisposed) return;
        int x = AppTheme.S(input is ComboBox or DateTimePicker ? 6 : 10);
        int height = input is TextBox { Multiline: true } ? Math.Max(1, Height - AppTheme.S(16)) : input.PreferredSize.Height;
        input.SetBounds(x, Math.Max(AppTheme.S(5), (Height - height) / 2), Math.Max(1, Width - x * 2), height);
    }

    private void OnAppearanceChanged(object? sender, EventArgs e) => UpdateAppearance();
    private void OnInputFontChanged(object? sender, EventArgs e) => ArrangeInput();
    private void UpdateAppearance()
    {
        BackColor = !input.Enabled || input is TextBox { ReadOnly: true } ? AppTheme.SubtleLilac : Color.White;
        input.BackColor = BackColor;
        BorderColor = input.Focused ? AppTheme.Primary : AppTheme.Line;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            input.GotFocus -= OnAppearanceChanged; input.LostFocus -= OnAppearanceChanged;
            input.EnabledChanged -= OnAppearanceChanged;
            if (input is TextBox text) text.ReadOnlyChanged -= OnAppearanceChanged;
            input.FontChanged -= OnInputFontChanged;
            nativeFrame?.Dispose();
        }
        base.Dispose(disposing);
    }

    // The native combo keeps its popup and keyboard behavior. Only mask the square outer
    // frame; the surrounding surface owns the border and focus indication.
    private sealed class NativeFrameBorder : NativeWindow, IDisposable
    {
        private readonly Control combo;
        internal NativeFrameBorder(Control combo)
        {
            this.combo = combo;
            combo.HandleCreated += OnHandleCreated; combo.HandleDestroyed += OnHandleDestroyed;
            if (combo.IsHandleCreated) AssignHandle(combo.Handle);
        }
        private void OnHandleCreated(object? sender, EventArgs e) => AssignHandle(combo.Handle);
        private void OnHandleDestroyed(object? sender, EventArgs e) => ReleaseHandle();
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is not (0x000F or 0x0317 or 0x0318) || Handle == IntPtr.Zero || combo.Width < 3 || combo.Height < 3) return;
            using var graphics = m.Msg == 0x000F ? Graphics.FromHwnd(Handle) : m.WParam != IntPtr.Zero ? Graphics.FromHdc(m.WParam) : null;
            if (graphics is null) return;
            using var pen = new Pen(combo.BackColor, 2);
            graphics.DrawRectangle(pen, 0, 0, combo.Width - 1, combo.Height - 1);
        }
        public void Dispose()
        {
            combo.HandleCreated -= OnHandleCreated; combo.HandleDestroyed -= OnHandleDestroyed;
            ReleaseHandle();
        }
    }
}
