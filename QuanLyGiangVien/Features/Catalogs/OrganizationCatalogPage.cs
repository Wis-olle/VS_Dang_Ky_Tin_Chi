using QuanLyGiangVien.Domain.Catalogs;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Catalogs;

internal sealed class OrganizationCatalogPage : UserControl
{
    internal IReadOnlyList<OrganizationControl> Sections { get; }
    internal bool IsBusy => Ui.IsBusy(this) || Sections.Any(s => s.IsBusy);
    internal bool HasUnsavedChanges => Sections.Any(s => s.HasUnsavedChanges);
    internal OrganizationCatalogPage(string connection, Action changed)
    {
        Dock = DockStyle.Fill; BackColor = AppTheme.Canvas;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        Sections = Enum.GetValues<OrganizationKind>().Select(kind => new OrganizationControl(kind, connection, changed)).ToArray();
        string[] titles = ["Khoa", "Ngành", "Lớp sinh viên"];
        for (int i = 0; i < Sections.Count; i++)
        {
            var tab = new TabPage(titles[i]) { BackColor = AppTheme.Canvas }; tab.Controls.Add(Sections[i]); tabs.TabPages.Add(tab);
        }
        tabs.Selecting += (_, e) =>
        {
            if (IsBusy) { e.Cancel = true; return; }
            if (!HasUnsavedChanges) return;
            if (!Ui.Confirm(this, "Có thay đổi danh mục chưa lưu. Bỏ thay đổi?")) { e.Cancel = true; return; }
            ResetEditors();
        };
        tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (tabs.SelectedTab?.Controls.OfType<OrganizationControl>().FirstOrDefault() is { } section)
                await Ui.RunAsync(section, () => section.ReloadAsync());
        };
        Controls.Add(tabs);
    }
    internal void ResetEditors() { foreach (var section in Sections) section.ResetEditor(); }
    internal async Task ReloadAsync() { foreach (var section in Sections) await section.ReloadAsync(); }
}
