using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntdUI;

namespace QuanLyGiangVien.Ui;

public static class AppTheme
{
    // Palette sampled from Nur Hasan's Student Dashboard; see docs/ASSET_SOURCES.md.
    public static readonly Color Canvas = ColorTranslator.FromHtml("#FFEFEF");
    public static readonly Color Ink = ColorTranslator.FromHtml("#211A1C");
    public static readonly Color Muted = ColorTranslator.FromHtml("#6E6267");
    public static readonly Color Line = ColorTranslator.FromHtml("#EEDADD");
    public static readonly Color Primary = ColorTranslator.FromHtml("#D44423");
    public static readonly Color PrimaryHover = ColorTranslator.FromHtml("#BA3B20");
    public static readonly Color PrimaryPressed = ColorTranslator.FromHtml("#A43019");
    public static readonly Color Accent = ColorTranslator.FromHtml("#FE6B4A");
    public static readonly Color AccentSoft = ColorTranslator.FromHtml("#F6D9D5");
    public static readonly Color HoverLine = ColorTranslator.FromHtml("#E6A89A");
    public static readonly Color Cyan = ColorTranslator.FromHtml("#E4F5F7");
    public static readonly Color Peach = ColorTranslator.FromHtml("#FCCAC1");
    public static readonly Color Lilac = ColorTranslator.FromHtml("#F6EDFE");
    public static readonly Color Mint = ColorTranslator.FromHtml("#E9F9EF");
    public static readonly Color FacultyHoverFill = ColorTranslator.FromHtml("#FCE7F3");
    public static readonly Color TableHeader = ColorTranslator.FromHtml("#FBF7FD");
    public static readonly Color Selection = ColorTranslator.FromHtml("#FBE9E5");
    public static readonly Color SubtleLilac = ColorTranslator.FromHtml("#FBF7FD");
    public static readonly Color SubtlePeach = ColorTranslator.FromHtml("#FCECE8");
    public const string HeroGradient = "90, #E2D6FC, #FCCAC1";
    public static readonly Color Green = ColorTranslator.FromHtml("#286644");
    public static readonly Color Danger = ColorTranslator.FromHtml("#B62D3C");
    public static float K { get; private set; } = 1;
    private static float fontScale = 1;
    private static readonly PrivateFontCollection fonts = new();
    private static readonly List<string> registeredFonts = [];
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool RemoveFontResourceEx(string fileName, uint flags, IntPtr reserved);
    private static FontFamily family = new("Segoe UI");
    private sealed record FontSpec(float Size, bool Bold);
    private static readonly ConditionalWeakTable<Font, FontSpec> fontSpecs = new();
    private static readonly ConditionalWeakTable<Control, FontSpec> controlSpecs = new();
    public static int S(int value) => (int)Math.Round(value * K);
    public static Font Font(float size = 10, bool bold = false)
    {
        var font = new Font(family, size * fontScale, bold ? FontStyle.Bold : FontStyle.Regular);
        fontSpecs.Add(font, new(size, bold));
        return font;
    }
    public static IReadOnlyList<Font> RefreshFonts(Control root)
    {
        var snapshot = new List<(Control Control, FontSpec Spec)>();
        void Collect(Control control)
        {
            if (controlSpecs.TryGetValue(control, out var stored)) snapshot.Add((control, stored));
            else if (fontSpecs.TryGetValue(control.Font, out var spec))
            {
                controlSpecs.Add(control, spec);
                snapshot.Add((control, spec));
            }
            foreach (Control child in control.Controls) Collect(child);
        }
        Collect(root);
        var replacements = new List<Font>();
        foreach (var (control, spec) in snapshot)
        {
            var replacement = Font(spec.Size, spec.Bold);
            control.Font = replacement;
            replacements.Add(replacement);
        }
        return replacements;
    }
    public static void Initialize()
    {
        foreach (var name in new[] { "PlusJakartaSans-Regular.ttf", "PlusJakartaSans-Bold.ttf" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "fonts", name);
            if (!File.Exists(path)) continue;
            // WinForms uses GDI as well as GDI+. Register privately for both renderers;
            // PrivateFontCollection alone does not make TextRenderer see the font.
            if (AddFontResourceEx(path, 0x10, IntPtr.Zero) > 0) registeredFonts.Add(path);
            fonts.AddFontFile(path);
        }
        if (fonts.Families.Length > 0) family = fonts.Families[0];
        Config.Animation = true;
        Config.TextRenderingHighQuality = true;
        Style.SetPrimary(Primary);
        Style.SetError(Danger);
        Localization.Provider = new VietLocalization();
    }
    public static void Release()
    {
        fonts.Dispose();
        foreach (var path in registeredFonts) RemoveFontResourceEx(path, 0x10, IntPtr.Zero);
        registeredFonts.Clear();
    }
    public static void SetScale(int dpi)
    {
        K = dpi / 96f;
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        fontScale = dpi / graphics.DpiX;
        Config.SetDpi(K);
        Config.NoticeWindowOffsetXY = S(88);
    }
    public static Color Tone(string faculty) => faculty switch
    {
        "Công nghệ thông tin" => Cyan,
        "Kinh tế" => Peach,
        "Ngoại ngữ" => Lilac,
        _ => Mint
    };
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static AntdUI.Label Label(string text, float size = 10, bool bold = false, Color? color = null) => new()
    {
        Text = text, Font = Font(size, bold), ForeColor = color ?? Ink,
        BackColor = Color.Transparent, AutoEllipsis = true
    };
    public static AntdUI.Button Button(string text, string? icon = null, bool primary = false) => new MotionButton
    {
        Text = text, IconSvg = icon, Font = Font(10, true), Radius = 12,
        Type = primary ? TTypeMini.Primary : TTypeMini.Default,
        BackColor = primary ? Primary : null, DefaultBack = Color.White, DefaultBorderColor = Line,
        BackHover = primary ? PrimaryHover : AccentSoft, BackActive = primary ? PrimaryPressed : Peach,
        BorderWidth = primary ? 0 : 1, ForeColor = primary ? Color.White : Ink,
        Cursor = Cursors.Hand
    };
    public static AntdUI.Panel Surface(int radius = 22) => new()
    {
        Back = Color.White, BackColor = Color.Transparent, Radius = radius, Shadow = 0,
        BorderWidth = 0
    };
}
