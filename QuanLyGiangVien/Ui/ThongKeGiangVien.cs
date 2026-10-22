using QuanLyGiangVien.Domain;
using AntdUI;
using Label = AntdUI.Label;

namespace QuanLyGiangVien.Ui;

public sealed class DashboardStats : System.Windows.Forms.Panel
{
    public event Action<int>? ActionRequested;
    private readonly DashboardStatCard[] cards =
    [
        new("Tổng giảng viên", "users", "TeamOutlined", AppTheme.Peach),
        new("Khoa có giảng viên", "graduation-cap", "BankOutlined", AppTheme.Lilac),
        new("Lượt phân công lớp", "book-text", "ReadOutlined", AppTheme.Cyan)
    ];
    public DashboardStats()
    {
        BackColor = AppTheme.Canvas;
        Controls.AddRange(cards);
        for (int i = 0; i < cards.Length; i++) { int index = i; cards[i].Click += (_, _) => ActionRequested?.Invoke(index); }
        Resize += (_, _) => Arrange();
    }
    public void Refresh(IReadOnlyList<Lecturer> lecturers)
    {
        cards[0].ShowValue(lecturers.Count);
        cards[1].ShowValue(lecturers.Select(item => item.Faculty).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        cards[2].ShowValue(lecturers.Sum(item => item.ActiveClasses));
        Arrange();
    }
    public void AnimateFromZero(int delayMilliseconds = 0)
    {
        for (int index = 0; index < cards.Length; index++) cards[index].AnimateFromZero(delayMilliseconds + index * 70);
    }
    internal bool HasRunningMotion => cards.Any(card => card.IsAnimating);
    internal IReadOnlyList<int> DisplayedCounts => Array.AsReadOnly(cards.Select(card => card.DisplayedCount).ToArray());
    internal IReadOnlyList<int> ActualCounts => Array.AsReadOnly(cards.Select(card => card.ActualCount).ToArray());
    private void Arrange()
    {
        int gap = AppTheme.S(16), width = Math.Max(1, (Width - gap * 2) / 3);
        for (int i = 0; i < cards.Length; i++) cards[i].SetBounds(i * (width + gap), 0, i == 2 ? Width - i * (width + gap) : width, Height);
    }
}

internal sealed class DashboardStatCard : DashboardActionPanel
{
    private readonly AnimatedDashboardIcon icon;
    private readonly Label title;
    private readonly Label value = AppTheme.Label("0", 19, true);
    private readonly Motion countMotion = new();
    private int target;
    private int displayed;
    internal bool IsAnimating => countMotion.IsRunning;
    internal int DisplayedCount => displayed;
    internal int ActualCount => target;
    public DashboardStatCard(string caption, string iconId, string fallbackSvg, Color tone)
    {
        Radius = 16;
        Back = Color.White;
        BackColor = Color.Transparent;
        icon = new(iconId, caption, tone, fallbackSvg);
        title = AppTheme.Label(caption, 8.5f, false, AppTheme.Muted);
        Controls.AddRange([icon, title, value]);
        AccessibleName = caption;
        AccessibleDescription = caption == "Khoa có giảng viên" ? "Mở bộ lọc khoa." : "Mở danh sách giảng viên tương ứng.";
        BindChildClicks();
        icon.BindHover(this, title, value);
        AuthCardMotion.Bind(this, Color.FromArgb(255, (255 + tone.R) / 2, (255 + tone.G) / 2, (255 + tone.B) / 2));
        Resize += (_, _) => Arrange();
    }
    public void ShowValue(int count)
    {
        value.AccessibleName = $"{title.Text}: {count:N0}";
        if (target == count) return;
        target = count;
        AnimateCount(displayed);
    }
    public void AnimateFromZero(int delayMilliseconds)
    {
        if (target == 0) return;
        AnimateCount(0, delayMilliseconds);
    }
    private void AnimateCount(int from, int delayMilliseconds = 0)
    {
        countMotion.Cancel(false);
        int to = target;
        if (!IsHandleCreated || !Config.Animation) { ShowCount(to); return; }
        countMotion.Run(650, progress => ShowCount(MotionMath.Counter(from, to, progress)), delayMilliseconds);
    }
    private void ShowCount(int count)
    {
        displayed = count;
        value.Text = count.ToString("N0");
    }
    private void Arrange()
    {
        int x = AppTheme.S(18), size = AppTheme.S(36);
        icon.SetBounds(x, (Height - size) / 2, size, size);
        int textX = x + size + AppTheme.S(14), width = Math.Max(1, Width - textX - AppTheme.S(12));
        int top = (Height - AppTheme.S(57)) / 2;
        value.SetBounds(textX, top, width, AppTheme.S(32));
        title.SetBounds(textX, top + AppTheme.S(34), width, AppTheme.S(23));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) countMotion.Dispose();
        base.Dispose(disposing);
    }
}
