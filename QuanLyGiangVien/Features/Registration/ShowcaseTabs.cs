using System.Runtime.CompilerServices;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

/// <summary>Retains the actual TabControl and pages for native automation and keyboard navigation.</summary>
internal static class ShowcaseTabs
{
    private static readonly ConditionalWeakTable<TabControl, Panel> hosts = new();
    internal static void Enhance(TabControl tabs)
    {
        if (tabs.IsDisposed || tabs.Parent is null || hosts.TryGetValue(tabs, out _)) return;
        Wrap(tabs, true);
    }

    internal static Panel HideHeader(TabControl tabs)
    {
        if (hosts.TryGetValue(tabs, out var existing)) return existing;
        return Wrap(tabs, false);
    }

    private static Panel Wrap(TabControl tabs, bool showNavigation)
    {
        var parent = tabs.Parent;
        int index = parent?.Controls.GetChildIndex(tabs) ?? 0;
        var table = parent as TableLayoutPanel;
        var cell = table?.GetCellPosition(tabs);
        int columnSpan = table?.GetColumnSpan(tabs) ?? 1, rowSpan = table?.GetRowSpan(tabs) ?? 1;
        var host = new Panel { Name = tabs.Name + "Surface", Dock = tabs.Dock, Bounds = tabs.Bounds,
            BackColor = Color.White, Margin = tabs.Margin, Padding = Padding.Empty };
        hosts.Add(tabs, host);
        var viewport = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty };
        tabs.Dock = DockStyle.None; tabs.TabStop = false;
        viewport.Controls.Add(tabs);
        host.Controls.Add(viewport);
        if (showNavigation) host.Controls.Add(new Navigation(tabs) { Dock = DockStyle.Top });
        if (parent is not null)
        {
            parent.Controls.Add(host); parent.Controls.SetChildIndex(host, index);
            if (table is not null && cell is {} position)
            { table.SetCellPosition(host, position); table.SetColumnSpan(host, columnSpan); table.SetRowSpan(host, rowSpan); }
        }
        bool arranging = false, queued = false;
        void LayoutTabs()
        {
            if (arranging || tabs.IsDisposed || viewport.IsDisposed) return;
            arranging = true;
            try
            {
                // Measure the native page, including all header rows and border insets.
                // A width change can change a multiline header, so settle the measurement.
                for (int pass = 0; pass < 4; pass++)
                {
                    var page = tabs.DisplayRectangle;
                    var bounds = new Rectangle(-page.Left, -page.Top,
                        Math.Max(1, viewport.ClientSize.Width + tabs.Width - page.Width),
                        Math.Max(1, viewport.ClientSize.Height + tabs.Height - page.Height));
                    if (tabs.Bounds == bounds) break;
                    tabs.Bounds = bounds;
                }
            }
            finally { arranging = false; }
        }
        void RefreshLayout(object? sender, EventArgs e)
        {
            if (arranging || viewport.IsDisposed || tabs.IsDisposed) return;
            LayoutTabs();
            if (queued || !viewport.IsHandleCreated) return;
            queued = true;
            // Tab insertion/font inheritance may finish after the managed event.
            viewport.BeginInvoke(() => { queued = false; LayoutTabs(); });
        }
        viewport.SizeChanged += RefreshLayout;
        viewport.HandleCreated += RefreshLayout;
        tabs.HandleCreated += RefreshLayout;
        tabs.FontChanged += RefreshLayout;
        tabs.Layout += (sender, e) => RefreshLayout(sender, e);
        tabs.ControlAdded += (sender, e) => RefreshLayout(sender, e);
        tabs.ControlRemoved += (sender, e) => RefreshLayout(sender, e);
        tabs.SelectedIndexChanged += RefreshLayout;
        tabs.DpiChangedAfterParent += RefreshLayout;
        host.VisibleChanged += RefreshLayout;
        LayoutTabs();
        return host;
    }

    private sealed class Navigation : Panel
    {
        private readonly TabControl tabs;
        private readonly List<Button> buttons = [];
        private readonly Button overflow;
        private readonly ContextMenuStrip menu = new();
        private bool rebuilding;

        internal Navigation(TabControl tabs)
        {
            this.tabs = tabs;
            Height = AppTheme.S(46); BackColor = Color.White;
            Padding = new(AppTheme.S(5), AppTheme.S(3), AppTheme.S(5), AppTheme.S(4));
            overflow = Ui.Button("Các mục ▾"); overflow.AutoSize = false;
            overflow.Name = "btnSectionOverflow"; overflow.AccessibleName = "Chọn mục khác";
            Controls.Add(overflow);
            overflow.Click += (_, _) => menu.Show(overflow, new Point(0, overflow.Height));
            tabs.SelectedIndexChanged += OnSelectionChanged;
            tabs.ControlAdded += OnPagesChanged; tabs.ControlRemoved += OnPagesChanged;
            SizeChanged += (_, _) => Arrange();
            Rebuild();
        }

        private void OnSelectionChanged(object? sender, EventArgs e) => Arrange();
        private void OnPagesChanged(object? sender, ControlEventArgs e) => Rebuild();
        private void Rebuild()
        {
            if (rebuilding || IsDisposed) return;
            rebuilding = true;
            foreach (var button in buttons) { Controls.Remove(button); button.Dispose(); }
            buttons.Clear();
            foreach (TabPage page in tabs.TabPages)
            {
                var button = Ui.Button(page.Text); button.AutoSize = false; button.Tag = page;
                button.Name = "btnSection" + page.TabIndex; button.AccessibleName = page.Text;
                button.Click += (_, _) => tabs.SelectedTab = page;
                buttons.Add(button); Controls.Add(button);
            }
            rebuilding = false; Arrange();
        }

        private void Arrange()
        {
            if (rebuilding || IsDisposed) return;
            const int gap = 4;
            int available = ClientSize.Width - Padding.Horizontal;
            int menuWidth = AppTheme.S(104), x = Padding.Left, height = Height - Padding.Vertical;
            var widths = buttons.Select(b => TextRenderer.MeasureText(b.Text, b.Font).Width + AppTheme.S(28)).ToArray();
            bool needsMenu = widths.Sum() + AppTheme.S(gap) * Math.Max(0, buttons.Count - 1) > available;
            int limit = available - (needsMenu ? menuWidth + AppTheme.S(gap) : 0);
            var visible = new List<int>();
            for (int i = 0, used = 0; i < widths.Length; i++)
            {
                if (used + widths[i] > limit) break;
                visible.Add(i); used += widths[i] + AppTheme.S(gap);
            }
            int selected = tabs.SelectedIndex;
            if (selected >= 0 && !visible.Contains(selected))
            {
                while (visible.Count > 0 && visible.Sum(i => widths[i] + AppTheme.S(gap)) + widths[selected] > limit) visible.RemoveAt(visible.Count - 1);
                visible.Add(selected);
            }
            foreach (var item in menu.Items.Cast<ToolStripItem>().ToArray()) item.Dispose();
            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i]; button.Visible = visible.Contains(i);
                if (!button.Visible) { var page = (TabPage)button.Tag!; menu.Items.Add(page.Text, null, (_, _) => tabs.SelectedTab = page); continue; }
                button.SetBounds(x, Padding.Top, Math.Min(widths[i], Math.Max(1, limit)), height);
                button.BackColor = i == selected ? AppTheme.AccentSoft : Color.White;
                button.ForeColor = i == selected ? AppTheme.PrimaryPressed : AppTheme.Muted;
                x += button.Width + AppTheme.S(gap);
            }
            overflow.Visible = needsMenu;
            overflow.SetBounds(Math.Max(Padding.Left, ClientSize.Width - Padding.Right - menuWidth), Padding.Top, menuWidth, height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tabs.SelectedIndexChanged -= OnSelectionChanged;
                tabs.ControlAdded -= OnPagesChanged; tabs.ControlRemoved -= OnPagesChanged;
                menu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
