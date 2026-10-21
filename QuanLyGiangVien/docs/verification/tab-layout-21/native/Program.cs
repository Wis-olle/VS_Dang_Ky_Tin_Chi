using System.Reflection;
using System.Text.Json;
using QuanLyGiangVien.Ui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); AppTheme.Initialize(); AppTheme.SetScale(96);
        Directory.CreateDirectory(args[0]); var checks = new List<string>(); var errors = new List<string>();
        void Expect(bool ok, string text) { if (ok) checks.Add(text); else errors.Add(text); }
        var style = typeof(AppTheme).Assembly.GetType("QuanLyGiangVien.Features.Registration.ShowcaseTabs")!;
        Panel Wrap(TabControl tabs, bool show) => show
            ? Enhance(tabs) : (Panel)style.GetMethod("HideHeader", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [tabs])!;
        Panel Enhance(TabControl tabs) { style.GetMethod("Enhance", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [tabs]); return (Panel)tabs.Parent!.Parent!; }
        void Covered(TabControl tabs, string scenario)
        {
            Application.DoEvents(); var view = tabs.Parent!; var display = tabs.DisplayRectangle;
            Expect(tabs.Top + display.Top <= 0, scenario + ": the entire native header is outside its viewport.");
            Expect(tabs.Top + display.Bottom >= view.ClientSize.Height && tabs.Left + display.Right >= view.ClientSize.Width,
                scenario + ": the actual page fills the viewport.");
            Expect(Enumerable.Range(0, tabs.TabCount).All(i =>
            {
                var header = tabs.GetTabRect(i); header.Offset(tabs.Location);
                return !header.IntersectsWith(view.ClientRectangle);
            }), scenario + ": no native tab header intersects the visible viewport.");
        }
        try
        {
            using var form = new Form { ClientSize = new(1100, 720), BackColor = AppTheme.Canvas, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(5000, 0) };
            using var font = AppTheme.Font(10); using var larger = AppTheme.Font(16);
            var tabs = new TabControl { Name = "outer", Dock = DockStyle.Fill, Padding = new(14, 8) };
            var host = Wrap(tabs, false); form.Controls.Add(host); form.Show(); Application.DoEvents();
            string[] titles = ["Đợt, quota & hàng chờ", "Kết quả đăng ký", "Cố vấn & kế hoạch", "Thống kê theo lớp", "Duyệt ngoại lệ", "Vận hành lớp & lịch", "Nhập dữ liệu nguồn", "Nhu cầu & sức khỏe", "Thông báo"];
            foreach (var title in titles) tabs.TabPages.Add(new TabPage(title));
            tabs.Font = font; Covered(tabs, "Pages added after wrapping with a late inherited font");
            tabs.Font = larger; Covered(tabs, "Runtime font change");
            tabs.Font = font; tabs.Padding = new(14, 18); Covered(tabs, "Header padding change");
            tabs.Multiline = true; form.ClientSize = new(600, 500); Covered(tabs, "Native multiline header after compact resize");
            tabs.SelectedIndex = 6; Covered(tabs, "Selecting another native row");
            foreach (TabPage old in tabs.TabPages.Cast<TabPage>().ToArray()) { tabs.TabPages.Remove(old); old.Dispose(); }
            foreach (var title in titles) tabs.TabPages.Add(new TabPage(title));
            Covered(tabs, "Clearing and repopulating all pages");
            tabs.Alignment = TabAlignment.Bottom; tabs.Alignment = TabAlignment.Top;
            Covered(tabs, "Native handle recreation");
            tabs.Multiline = false; tabs.Padding = new(14, 8); form.ClientSize = new(1100, 720);
            tabs.SelectedIndex = 6;
            var child = new TabControl { Name = "nested", Dock = DockStyle.Fill };
            child.TabPages.AddRange([new TabPage("Hàng chờ & lời mời"), new TabPage("Đợt đăng ký"), new TabPage("Nhóm ưu tiên"), new TabPage("Quota lớp")]);
            tabs.TabPages[6].Controls.Add(child); var nested = Wrap(child, true); Covered(child, "Nested navigation");
            var navigation = nested.Controls.Cast<Control>().Single(c => c != child.Parent);
            var button = navigation.Controls.OfType<Button>().Single(b => b.AccessibleName == "Hàng chờ & lời mời");
            Expect(button.Visible && button.Width >= TextRenderer.MeasureText(button.Text, button.Font).Width + 18,
                "The full waiting-list label fits its visible navigation button.");
            var other = navigation.Controls.OfType<Button>().Single(b => b.AccessibleName == "Quota lớp"); other.PerformClick();
            Expect(child.SelectedIndex == 3, "Rounded navigation selects the original native page.");
            using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, form.ClientRectangle); image.Save(Path.Combine(args[0], "tabs.png"));
            File.WriteAllText(Path.Combine(args[0], "results.json"), JsonSerializer.Serialize(new { passed = errors.Count == 0, checks, errors }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{checks.Count} PASS / {errors.Count} FAIL"); foreach (var error in errors) Console.WriteLine(error);
            Environment.ExitCode = errors.Count == 0 ? 0 : 1;
        }
        finally { AppTheme.Release(); }
    }
}
