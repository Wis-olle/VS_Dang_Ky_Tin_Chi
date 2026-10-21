using QuanLyGiangVien.Ui;
namespace QuanLyGiangVien.Features.Registration;

internal sealed record WorkflowChoice(string Value, string Text) { public override string ToString() => Text; }
internal static class PhaseThreeUi
{
    internal static ComboBox Choices(params WorkflowChoice[] values)
    {
        var input = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = AppTheme.S(220), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = AppTheme.Ink };
        input.Items.AddRange(values); if (values.Length > 0) input.SelectedIndex = 0; return input;
    }
    internal static string Choice(ComboBox input) => ((WorkflowChoice)input.SelectedItem!).Value;
    internal static TextBox Text(string name, int max = 20) => new() { Name = name, AccessibleName = name, Width = AppTheme.S(max > 100 ? 360 : 130), MaxLength = max, BorderStyle = BorderStyle.FixedSingle, ForeColor = AppTheme.Ink, BackColor = Color.White };
    internal static void Field(FlowLayoutPanel bar, string label, Control input)
    {
        input.AccessibleName = label; input.Margin = new Padding(AppTheme.S(4), AppTheme.S(7), AppTheme.S(8), AppTheme.S(4));
        bar.Controls.Add(Ui.Label(label)); bar.Controls.Add(input);
    }
    internal static string? Optional(TextBox input) => string.IsNullOrWhiteSpace(input.Text) ? null : input.Text.Trim().ToUpperInvariant();
    internal static string Version(object value) => value is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(value)!.Replace("0x", "");
}
