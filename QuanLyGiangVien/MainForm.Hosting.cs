namespace QuanLyGiangVien;

public sealed partial class MainForm
{
    private readonly bool embedded;
    internal bool IsEmbedded => embedded;
    internal bool HasOwnChromeForCheck => titlebar.Visible || nav.Visible;
}
