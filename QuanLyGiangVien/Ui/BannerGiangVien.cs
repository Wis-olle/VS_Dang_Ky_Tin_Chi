using AntdUI;
using Label = AntdUI.Label;

namespace QuanLyGiangVien.Ui;

public sealed class DashboardHero : AntdUI.Panel
{
    private static readonly Color GradientStart = ColorTranslator.FromHtml(AppTheme.HeroGradient.Split(',')[1].Trim());
    private static readonly Color GradientEnd = ColorTranslator.FromHtml(AppTheme.HeroGradient.Split(',')[2].Trim());
    private readonly Label eyebrow = AppTheme.Label("KHÔNG GIAN ĐÀO TẠO", 8, true);
    private readonly Label heading = AppTheme.Label("Đội ngũ giảng viên", 23, true);
    private readonly Label subtitle = AppTheme.Label("Hồ sơ rõ ràng, kết nối việc giảng dạy\nvà phân công trong một không gian.", 9, false, AppTheme.Muted);
    private readonly AntdUI.Button add = AppTheme.Button("Thêm giảng viên", "PlusOutlined", true);
    private readonly FloatingArtwork illustration;
    private readonly Bitmap? artwork;
    private Rectangle illustrationBounds;
    internal int ArtworkOffsetPixels => (int)Math.Round(illustration.Offset);
    internal float ArtworkOffset => illustration.Offset;
    internal int ArtworkCacheBuilds => illustration.CacheBuilds;
    public event Action? AddRequested;

    public DashboardHero()
    {
        Radius = 20;
        BackColor = Color.Transparent;
        Back = AppTheme.Lilac;
        BackExtend = AppTheme.HeroGradient;
        BorderWidth = 0;
        heading.AutoEllipsis = false;
        subtitle.TextMultiLine = true;
        add.BackColor = AppTheme.Primary;
        add.BackHover = AppTheme.PrimaryHover;
        add.BackActive = AppTheme.PrimaryPressed;
        add.Radius = 9;
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "illustrations", "on-the-laptop.png");
        if (File.Exists(path))
        {
            // The local PNG preserves the original ink lines and transparency.
            artwork = new Bitmap(path);
        }
        illustration = new(artwork);
        Controls.AddRange([illustration, eyebrow, heading, subtitle, add]);
        add.Click += (_, _) => AddRequested?.Invoke();
        Resize += (_, _) => Arrange();
    }

    private void Arrange()
    {
        bool compact = Height < AppTheme.S(185);
        int pad = AppTheme.S(compact ? 20 : 28);
        int imageWidth = Math.Clamp(Width / 3, AppTheme.S(170), AppTheme.S(310));
        int textWidth = Math.Max(AppTheme.S(270), Width - imageWidth - pad * 2);
        eyebrow.SetBounds(pad, AppTheme.S(compact ? 9 : 21), textWidth, AppTheme.S(18));
        heading.SetBounds(pad, AppTheme.S(compact ? 31 : 50), textWidth, AppTheme.S(44));
        subtitle.Visible = !compact;
        subtitle.SetBounds(pad, AppTheme.S(96), textWidth, AppTheme.S(36));
        add.SetBounds(pad, Height - AppTheme.S(compact ? 47 : 49), AppTheme.S(176), AppTheme.S(compact ? 32 : 36));
        illustrationBounds = new(Width - imageWidth - AppTheme.S(10), AppTheme.S(8), imageWidth, Height - AppTheme.S(16));
        illustration.Bounds = illustrationBounds;
        illustration.SetOffset(0);
        illustration.SetBackdrop(GradientStart, GradientEnd, Width, illustrationBounds.X);
    }
    internal void ApplyAmbient(double seconds, bool enabled)
    {
        if (IsDisposed || Disposing) return;
        if (!enabled || !AntdUI.Config.Animation)
        {
            illustration.SetOffset(0);
            return;
        }
        if (!IsInViewport()) return;
        // Keep the gradient stable: changing BackExtend repaints the entire AntdUI
        // panel and all its text. Only the fixed opaque artwork surface animates.
        illustration.SetOffset(MotionMath.Wave(seconds, 5, AppTheme.S(3)));
    }
    private bool IsInViewport()
    {
        if (!IsHandleCreated || !Visible || Width <= 0 || Height <= 0) return false;
        Rectangle visible = ClientRectangle;
        for (Control? current = this; current?.Parent is { } parent; current = parent)
        {
            visible.Offset(current.Left, current.Top);
            visible = Rectangle.Intersect(visible, parent.ClientRectangle);
            if (visible.Width <= 0 || visible.Height <= 0 || !parent.Visible) return false;
        }
        return true;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) artwork?.Dispose();
    }
}
