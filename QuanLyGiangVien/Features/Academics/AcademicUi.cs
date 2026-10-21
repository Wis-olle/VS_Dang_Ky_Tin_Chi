using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Academics;

internal static class AcademicUi
{
    internal static int S(int value) => AppTheme.S(value);
    internal static Button Button(string text, bool primary = false)
    {
        var button = new QuanLyGiangVien.Features.Registration.ShowcaseButton
        {
            Primary = primary,
            Text = text, AutoSize = true, MinimumSize = new(S(96), S(36)),
            Padding = new(S(10), S(4), S(10), S(4)), Margin = new(0, 0, S(8), S(8)),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? AppTheme.Primary : Color.White,
            ForeColor = primary ? Color.White : AppTheme.Ink, Cursor = Cursors.Hand,
            AccessibleName = text, UseMnemonic = true
        };
        button.EnabledChanged += (_, _) =>
        {
            button.BackColor = button.Enabled ? primary ? AppTheme.Primary : Color.White : Color.FromArgb(244, 239, 239);
            button.ForeColor = button.Enabled ? primary ? Color.White : AppTheme.Ink : AppTheme.Muted;
        };
        return button;
    }

    internal static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
        EnableHeadersVisualStyles = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        ColumnHeadersHeight = S(42), ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersDefaultCellStyle = new() { BackColor = AppTheme.Lilac, ForeColor = AppTheme.Ink, WrapMode = DataGridViewTriState.True },
        DefaultCellStyle = new() { BackColor = Color.White, SelectionBackColor = AppTheme.AccentSoft,
            SelectionForeColor = AppTheme.Ink, Padding = new(S(5), S(3), S(5), S(3)) },
        AlternatingRowsDefaultCellStyle = new() { BackColor = Color.FromArgb(255, 250, 250) },
        RowTemplate = { Height = S(42) }
    };
}

internal enum AcademicFieldType { Text, Integer, Decimal, Check, Date, List, Choice }
internal sealed record AcademicOption(string Value, string Caption)
{
    public override string ToString() => Caption;
}
internal sealed record AcademicField(string Key, string Caption,
    AcademicFieldType Type = AcademicFieldType.Text, bool Required = false,
    string Default = "", string? Help = null, AcademicOption[]? Options = null,
    string? ReferenceTable = null, string? ReferenceId = null, string? ReferenceName = null);
internal sealed record AcademicColumn(string Key, string Caption, int Width = 110);
