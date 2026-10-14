using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.StudentTimetable;

internal static class TimetableStyle
{
    internal static readonly Color Canvas = AppTheme.Canvas;
    internal static readonly Color Ink = AppTheme.Ink;
    internal static readonly Color Muted = AppTheme.Muted;
    // Existing color slots follow the common palette without changing the form's structure.
    internal static readonly Color Purple = AppTheme.Primary;
    internal static readonly Color PurpleDark = AppTheme.PrimaryPressed;
    internal static readonly Color SelectedFill = ColorTranslator.FromHtml("#7662C9");
    internal static readonly Color SessionInk = ColorTranslator.FromHtml("#6551BE");
    internal static readonly Color SessionLine = ColorTranslator.FromHtml("#E2D6FC");
    internal static readonly Color Soft = AppTheme.AccentSoft;
    internal static readonly Color Teal = AppTheme.Green;
    internal static readonly Color Line = AppTheme.Line;
    internal static readonly Color SessionFill = AppTheme.Cyan;
    internal static Color SessionHover(float progress) => Color.FromArgb(
        (int)Math.Round(SessionFill.R + (AppTheme.Lilac.R - SessionFill.R) * progress),
        (int)Math.Round(SessionFill.G + (AppTheme.Lilac.G - SessionFill.G) * progress),
        (int)Math.Round(SessionFill.B + (AppTheme.Lilac.B - SessionFill.B) * progress));
    internal static int S(int value) => AppTheme.S(value);
    internal static string Asset(string file) => Path.Combine(AppContext.BaseDirectory, "Assets", "student-timetable", file);
    internal static string Icon(string name) => File.ReadAllText(Asset(name + ".svg"));
    internal static void OwnFont(Control control, float size, bool bold = false)
    {
        var font = AppTheme.Font(size, bold);
        control.Font = font;
        control.Disposed += (_, _) => font.Dispose();
    }
    internal static Label Label(string text, float size = 10, bool bold = false, Color? color = null)
    {
        var label = new Label { Text = text, AutoSize = false, ForeColor = color ?? Ink,
            BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        OwnFont(label, size, bold);
        return label;
    }
    internal static AntdUI.Button Button(string text, string? icon = null, bool filled = false)
    {
        var button = new AntdUI.Button { Text = text, IconSvg = icon == null ? null : Icon(icon),
            Radius = 8, BorderWidth = 0, DefaultBack = filled ? Purple : Soft,
            ForeColor = filled ? Color.White : PurpleDark, BackHover = filled ? AppTheme.PrimaryHover : AppTheme.Peach,
            BackActive = filled ? AppTheme.PrimaryPressed : AppTheme.AccentSoft, Cursor = Cursors.Hand, AccessibleName = text };
        OwnFont(button, 9, true);
        return button;
    }
    internal static AntdUI.Panel Card() => new() { Back = Color.White, BackColor = Canvas, Radius = 12, BorderWidth = 0, Shadow = 0 };
    internal static void Put(Control parent, Control child, int x, int y, int w, int h)
    {
        child.SetBounds(S(x), S(y), S(w), S(h));
        parent.Controls.Add(child);
    }
}
