using System.Reflection;

namespace QuanLyGiangVien.Ui;

/// <summary>One native data/binding model, with the lecturer select's existing presentation.</summary>
internal sealed class ComboSelectAdapter : IDisposable
{
    private static readonly MethodInfo committed = typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private readonly ComboBox source;
    private readonly AnimatedSelect face;
    private readonly Control surface;
    private readonly ComboWindow window;
    private readonly bool originalTabStop;
    private readonly AccessibleRole originalRole;
    private bool synchronizing, queued, disposed;

    internal ComboSelectAdapter(ComboBox source, Control surface)
    {
        this.source = source; this.surface = surface; originalTabStop = source.TabStop; originalRole = source.AccessibleRole;
        face = new AnimatedSelect { Name = source.Name + "Select", TabIndex = 0, TabStop = originalTabStop, Dock = DockStyle.Fill };
        source.TabStop = false; source.AccessibleRole = AccessibleRole.None;
        surface.Controls.Add(face); face.BringToFront();
        source.SelectedIndexChanged += SourceChanged; source.DataSourceChanged += SourceChanged;
        source.DisplayMemberChanged += SourceChanged; source.ValueMemberChanged += SourceChanged;
        source.TextChanged += SourceChanged; source.FontChanged += SourceChanged;
        source.EnabledChanged += SourceChanged; source.VisibleChanged += SourceChanged;
        source.FormattingEnabledChanged += SourceChanged; source.FormatStringChanged += SourceChanged;
        source.FormatInfoChanged += SourceChanged;
        source.GotFocus += SourceFocused;
        source.Disposed += SourceDisposed;
        face.SelectedIndexChanged += UserSelected;
        face.MouseDown += PreparePopup;
        face.Enter += SourceChanged;
        surface.BackColorChanged += SourceChanged;
        window = new(source, this);
        Refresh();
    }

    private void SourceFocused(object? sender, EventArgs e)
    {
        if (!disposed && face.CanFocus) face.Focus();
    }
    private void SourceDisposed(object? sender, EventArgs e) => Dispose();
    private void PreparePopup(object? sender, MouseEventArgs e) => Refresh();
    private void SourceChanged(object? sender, EventArgs e) => Refresh();
    private void UserSelected(object? sender, EventArgs e)
    {
        if (disposed || synchronizing || !source.Enabled || face.SelectedIndex < 0 || face.SelectedIndex >= source.Items.Count) return;
        if (source.SelectedIndex == face.SelectedIndex) return;
        try
        {
            synchronizing = true;
            source.SelectedIndex = face.SelectedIndex;
            // Preserve the native user-commit event as well as SelectedIndexChanged.
            committed.Invoke(source, [EventArgs.Empty]);
        }
        finally { synchronizing = false; Refresh(); }
    }

    private void Refresh()
    {
        if (disposed || source.IsDisposed || face.IsDisposed || synchronizing) return;
        face.Font = source.Font; face.ForeColor = source.ForeColor;
        face.BackColor = surface.BackColor;
        face.Enabled = source.Enabled; face.Visible = source.Visible;
        face.AccessibleName = source.AccessibleName ?? source.Name;
        face.AccessibleDescription = source.AccessibleDescription;
        var labels = source.Items.Cast<object>().Select(item => source.GetItemText(item) ?? "").ToArray();
        face.SetSnapshot(labels, source.SelectedIndex);
        face.BringToFront();
    }

    private void QueueRefresh()
    {
        if (disposed || queued || !source.IsHandleCreated || source.IsDisposed) return;
        queued = true;
        source.BeginInvoke(() => { queued = false; if (!disposed) Refresh(); });
    }

    private sealed class ComboWindow : NativeWindow, IDisposable
    {
        private readonly ComboBox source;
        private readonly ComboSelectAdapter owner;
        internal ComboWindow(ComboBox source, ComboSelectAdapter owner)
        {
            this.source = source; this.owner = owner;
            source.HandleCreated += HandleCreated; source.HandleDestroyed += HandleDestroyed;
            if (source.IsHandleCreated) AssignHandle(source.Handle);
        }
        private void HandleCreated(object? sender, EventArgs e) { AssignHandle(source.Handle); owner.QueueRefresh(); }
        private void HandleDestroyed(object? sender, EventArgs e) { owner.face.CloseDropDown(true); ReleaseHandle(); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x014F) // CB_SHOWDROPDOWN: never expose the old native popup.
            {
                owner.Refresh();
                if (m.WParam != IntPtr.Zero) owner.face.OpenDropDown(); else owner.face.CloseDropDown(true);
                m.Result = IntPtr.Zero; return;
            }
            if (m.Msg == 0x0157) { m.Result = owner.face.IsDropDownOpen ? (IntPtr)1 : IntPtr.Zero; return; }
            if (m.Msg is 0x0201 or 0x0203)
            {
                owner.Refresh(); owner.face.Focus();
                if (owner.face.IsDropDownOpen) owner.face.CloseDropDown(); else owner.face.OpenDropDown();
                m.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref m);
            // Items mutations have no managed collection event. Wait until the managed list
            // has caught up with the CB_* message before taking its display snapshot.
            if (m.Msg is 0x0143 or 0x0144 or 0x014A or 0x014B or 0x014E or 0x0151) owner.QueueRefresh();
        }
        public void Dispose()
        {
            source.HandleCreated -= HandleCreated; source.HandleDestroyed -= HandleDestroyed;
            ReleaseHandle();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; window.Dispose();
        source.SelectedIndexChanged -= SourceChanged; source.DataSourceChanged -= SourceChanged;
        source.DisplayMemberChanged -= SourceChanged; source.ValueMemberChanged -= SourceChanged;
        source.TextChanged -= SourceChanged; source.FontChanged -= SourceChanged;
        source.EnabledChanged -= SourceChanged; source.VisibleChanged -= SourceChanged;
        source.FormattingEnabledChanged -= SourceChanged; source.FormatStringChanged -= SourceChanged;
        source.FormatInfoChanged -= SourceChanged; source.GotFocus -= SourceFocused;
        source.Disposed -= SourceDisposed;
        face.SelectedIndexChanged -= UserSelected; face.MouseDown -= PreparePopup; face.Enter -= SourceChanged;
        surface.BackColorChanged -= SourceChanged;
        if (!source.IsDisposed) { source.TabStop = originalTabStop; source.AccessibleRole = originalRole; }
        face.Dispose();
    }
}
