using AntdUI;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Shell;

internal sealed partial class CampusForm
{
    private Font MakeFont(float size, bool bold = false)
    {
        var font = AppTheme.Font(size, bold); ownedFonts.Add(font); return font;
    }
    private AntdUI.Label TextLabel(string text, float size = 10, bool bold = false, Color? color = null) => new() {
        Text = text, Font = MakeFont(size, bold), ForeColor = color ?? AppTheme.Ink, AutoEllipsis = true,
        BackColor = Color.Transparent
    };
    private AntdUI.Button ActionButton(string text, bool primary = false) => new() {
        Text = text, Font = MakeFont(9, true), Radius = 12, Type = primary ? TTypeMini.Primary : TTypeMini.Default,
        DefaultBack = Color.White, DefaultBorderColor = AppTheme.Line, BorderWidth = primary ? 0 : 1,
        ForeColor = primary ? Color.White : AppTheme.Ink
    };
    private static void ApplySelected(AntdUI.Button button, bool selected)
    {
        button.DefaultBack = selected ? AppTheme.AccentSoft : AppTheme.Canvas;
        button.DefaultBorderColor = selected ? AppTheme.AccentSoft : AppTheme.Canvas;
        button.ForeColor = selected ? AppTheme.PrimaryPressed : AppTheme.Muted;
        button.BorderWidth = 0;
    }
    private void BuildChrome()
    {
        caption.Text = " "; caption.ShowButton = true; caption.ShowIcon = false;
        caption.UseSystemStyleColor = true; caption.BackColor = AppTheme.Canvas; caption.Font = MakeFont(8);
        navigation.BackColor = host.BackColor = AppTheme.Canvas;
        navigation.Controls.AddRange([brand, identityText, roleText, disconnect]);
        disconnect.Visible = identityText.Visible = roleText.Visible = false;
        Controls.AddRange([caption, navigation, host]);
        host.Controls.Add(connectCard);
    }
    private void BuildConnection()
    {
        connectCard.Back = Color.White; connectCard.BackColor = Color.Transparent; connectCard.Radius = 20;
        connectCard.BorderWidth = 1; connectCard.BorderColor = AppTheme.Line;
        var title = TextLabel("Đăng ký học theo tín chỉ", 21, true);
        var description = TextLabel("Kết nối bằng tài khoản Windows hoặc SQL đã được cấp quyền.", 10, false, AppTheme.Muted);
        var configuration = new SqlConnectionStringBuilder(connectionString);
        connectionText.Text = $"Máy chủ: {configuration.DataSource}\nCơ sở dữ liệu: {configuration.InitialCatalog}";
        connectionText.TextMultiLine = errorText.TextMultiLine = true;
        title.SetBounds(AppTheme.S(28), AppTheme.S(26), AppTheme.S(584), AppTheme.S(42));
        description.SetBounds(AppTheme.S(28), AppTheme.S(82), AppTheme.S(584), AppTheme.S(50));
        connectionText.SetBounds(AppTheme.S(28), AppTheme.S(145), AppTheme.S(584), AppTheme.S(62));
        connect.SetBounds(AppTheme.S(28), AppTheme.S(225), AppTheme.S(240), AppTheme.S(44));
        errorText.SetBounds(AppTheme.S(28), AppTheme.S(286), AppTheme.S(584), AppTheme.S(72));
        connectCard.Controls.AddRange([title, description, connectionText, connect, errorText]);
    }
    private void LayoutShell()
    {
        if (brand == null) return;
        int s(int value) => AppTheme.S(value);
        int width = ClientSize.Width;
        caption.SetBounds(0, 0, width, s(30));
        navigation.SetBounds(0, s(30), width, s(62));
        host.SetBounds(0, s(92), width, Math.Max(1, ClientSize.Height - s(92)));
        FitCurrentPage();
        brand.SetBounds(s(28), s(9), s(width < s(1200) ? 125 : 150), s(42));
        int x = s(width < s(1200) ? 164 : 240);
        foreach (var pair in moduleButtons)
        {
            int buttonWidth = pair.Key switch {
                Domain.Shell.CampusModule.Lecturers => 110, Domain.Shell.CampusModule.Scheduling => 154,
                Domain.Shell.CampusModule.Catalogs => 170, Domain.Shell.CampusModule.Reports => 100,
                Domain.Shell.CampusModule.Registration => 178, _ => 150
            };
            pair.Value.SetBounds(x, s(10), s(buttonWidth), s(42)); x += s(buttonWidth + 6);
        }
        disconnect.SetBounds(width - s(138), s(11), s(112), s(40));
        int identityX = Math.Max(x + s(10), width - s(338));
        int identityWidth = Math.Max(s(70), width - s(154) - identityX);
        identityText.SetBounds(identityX, s(8), identityWidth, s(25));
        roleText.SetBounds(identityX, s(32), identityWidth, s(22));
        connectCard.SetBounds(Math.Max(s(16), (host.Width - s(640)) / 2),
            Math.Max(s(30), (host.Height - s(390)) / 2), s(640), s(390));
    }
    private void FitCurrentPage()
    {
        if (currentPage == null) return;
        // AntdUI.Window caches normal window size. Its typed setter clears that
        // cache; plain Control.Bounds/Dock would otherwise retain the old height.
        if (currentPage is AntdUI.Window window) window.Size = host.ClientSize;
        currentPage.Bounds = host.ClientRectangle;
    }
}
