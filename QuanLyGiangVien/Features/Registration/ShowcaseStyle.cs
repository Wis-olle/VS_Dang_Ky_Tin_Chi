using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

/// <summary>Shared native WinForms presentation; does not replace controls or their bindings.</summary>
internal static class ShowcaseStyle
{
    private static readonly ConditionalWeakTable<Control, object> styled = new();

    internal static void Apply(Control root)
    {
        if (root.IsDisposed) return;
        if (!styled.TryGetValue(root, out _))
        {
            styled.Add(root, new());
            ApplyOne(root);
            root.ControlAdded += (_, e) => { if (e.Control is {} added) Apply(added); };
        }
        foreach (var child in root.Controls.Cast<Control>().ToArray()) Apply(child);
        if (root is TabControl tabs) ShowcaseTabs.Enhance(tabs);
    }

    private static void ApplyOne(Control control)
    {
        // AntdUI and existing illustrated controls own their complete rendering.
        if (control.GetType().Namespace?.StartsWith("AntdUI", StringComparison.Ordinal) == true) return;
        switch (control)
        {
            case DataGridView grid:
                grid.BackgroundColor = Color.White;
                grid.GridColor = Color.FromArgb(244, 235, 238);
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                grid.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.TableHeader;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.Ink;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppTheme.TableHeader;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppTheme.Ink;
                grid.DefaultCellStyle.BackColor = Color.White;
                grid.DefaultCellStyle.ForeColor = AppTheme.Ink;
                grid.DefaultCellStyle.SelectionBackColor = AppTheme.Selection;
                grid.DefaultCellStyle.SelectionForeColor = AppTheme.Ink;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 251, 251);
                grid.RowTemplate.MinimumHeight = AppTheme.S(38);
                grid.RowTemplate.Height = AppTheme.S(42);
                grid.RowHeadersVisible = false;
                grid.EnableHeadersVisualStyles = false;
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = AppTheme.Line;
                button.FlatAppearance.MouseOverBackColor = button.BackColor == AppTheme.Primary ? AppTheme.PrimaryHover : AppTheme.AccentSoft;
                button.FlatAppearance.MouseDownBackColor = button.BackColor == AppTheme.Primary ? AppTheme.PrimaryPressed : AppTheme.Peach;
                button.UseVisualStyleBackColor = false;
                break;
            case TextBox text:
                text.ForeColor = AppTheme.Ink;
                // Read-only multiline outputs own their visibility and responsive height.
                if (text is { Multiline: true, ReadOnly: true })
                { text.BorderStyle = BorderStyle.None; text.BackColor = AppTheme.SubtleLilac; }
                else SoftInputSurface.Enhance(text);
                break;
            case ComboBox combo:
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Color.White;
                combo.ForeColor = AppTheme.Ink;
                combo.IntegralHeight = false;
                combo.DropDownHeight = AppTheme.S(280);
                // The hidden legacy picker remains outside layout and keyboard navigation.
                if (combo.Name != "cboRegistrationModule") SoftInputSurface.Enhance(combo);
                break;
            case DateTimePicker picker:
                SoftInputSurface.Enhance(picker);
                break;
            case TabPage page:
                page.BackColor = Color.White;
                page.UseVisualStyleBackColor = false;
                break;
            case Label label when label.BackColor == AppTheme.Lilac && label.Parent?.BackColor != AppTheme.Lilac:
                label.BackColor = AppTheme.SubtleLilac;
                break;
        }
    }
}

/// <summary>A normal Button with a fixed hit box and a rounded, keyboard accessible surface.</summary>
internal sealed class ShowcaseButton : Button
{
    private bool hovered, pressed;
    internal bool Primary;

    internal ShowcaseButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width < 3 || ClientSize.Height < 3) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.White);
        var fill = !Enabled ? Color.FromArgb(245, 241, 242) : Primary
            ? pressed ? AppTheme.PrimaryPressed : hovered ? AppTheme.PrimaryHover : AppTheme.Primary
            : pressed ? AppTheme.Peach : hovered ? AppTheme.AccentSoft : BackColor;
        using var path = AppTheme.Round(new RectangleF(1, 1, Width - 3, Height - 3), AppTheme.S(11));
        using var brush = new SolidBrush(fill);
        using var border = new Pen(Focused && ShowFocusCues ? AppTheme.Primary : Primary && Enabled ? fill : AppTheme.Line, AppTheme.S(Focused && ShowFocusCues ? 2 : 1));
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);
        var ink = !Enabled ? AppTheme.Muted : Primary ? Color.White : ForeColor;
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -AppTheme.S(9), -AppTheme.S(3)), ink,
            (TextAlign is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft ? TextFormatFlags.Left : TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
    }
}
