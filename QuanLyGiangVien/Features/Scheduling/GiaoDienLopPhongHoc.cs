using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Scheduling layout uses the lecturer page's existing pastel palette.</summary>
internal static class SchedulingTheme
{
    internal static readonly Color Canvas = AppTheme.Canvas;
    internal static readonly Color Ink = AppTheme.Ink;
    internal static readonly Color Muted = AppTheme.Muted;
    internal static readonly Color Line = AppTheme.Line;
    internal static readonly Color Primary = AppTheme.Primary;
    internal static readonly Color Lavender = AppTheme.Lilac;
    internal static readonly Color Cyan = AppTheme.Cyan;
    internal static readonly Color Amber = AppTheme.Peach;
    internal static readonly Color Rose = AppTheme.AccentSoft;
    internal static readonly Color Mint = AppTheme.Mint;
    internal static readonly Color Danger = AppTheme.Danger;
    internal static readonly Color Green = AppTheme.Green;

    internal static AntdUI.Label Label(string text, float size = 10, bool bold = false, Color? color = null)
        => AppTheme.Label(text, size, bold, color ?? Ink);

    internal static AntdUI.Panel Surface(int radius = 18) => AppTheme.Surface(radius);

    internal static AntdUI.Button Button(string text, string? icon = null, bool primary = false)
    {
        var button = AppTheme.Button(text, icon, primary);
        var originalFont = button.Font;
        button.Font = AppTheme.Font(9, primary);
        originalFont.Dispose();
        button.BackColor = primary ? Primary : null;
        button.BackHover = primary ? AppTheme.PrimaryHover : AppTheme.AccentSoft;
        button.BackActive = primary ? AppTheme.PrimaryPressed : AppTheme.Peach;
        button.DefaultBorderColor = Line;
        button.ForeColor = primary ? Color.White : Ink;
        button.Radius = 12;
        return button;
    }

    internal static string Icon(string key, Color? color = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling", key + ".svg");
        try
        {
            var svg = System.Xml.Linq.XElement.Parse(File.ReadAllText(path));
            // AntdUI recolors the root fill. Keep the source's outline in a local group.
            var strokes = new System.Xml.Linq.XElement(svg.Name.Namespace + "g",
                new System.Xml.Linq.XAttribute("fill", "none"), svg.Elements().ToArray());
            svg.ReplaceNodes(strokes);
            var ink = color ?? Primary;
            return svg.ToString().Replace("currentColor", $"#{ink.R:X2}{ink.G:X2}{ink.B:X2}", StringComparison.Ordinal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("Scheduling icon unavailable ({0}): {1}", key, e.Message);
            return key == "door-open" ? "HomeOutlined" : key == "calendar-days" ? "CalendarOutlined" : "BookOutlined";
        }
    }

    internal static AntdUI.Input ConfigureInput(AntdUI.Input input)
    {
        input.Font = AppTheme.Font(9);
        input.Radius = 10;
        input.BorderColor = Line;
        input.BorderActive = Primary; input.BorderHover = Primary;
        input.ForeColor = Ink;
        input.BackColor = Color.White;
        return input;
    }
}
