using System.Drawing.Imaging;
using System.Reflection;
using QuanLyGiangVien.Data;

namespace QuanLyGiangVien.Ui;

internal static class DashboardClickUiCheck
{
    internal static async Task Run(MainForm form, string folder, Action<bool, string> expect)
    {
        var stats = Descendants(form).OfType<DashboardStats>().Single();
        var cards = stats.Controls.OfType<AntdUI.Panel>().ToArray();
        var faculty = Descendants(form).OfType<DashboardFaculty>().Single();
        var select = Descendants(form).OfType<AnimatedSelect>().Single(c => c.AccessibleName == "Lọc theo khoa");
        form.SetFiltersForCheck("not-found", DemoData.Faculties[0], "Tạm nghỉ");
        Click(cards[0]);
        expect(form.ResultCount == form.Store.Items.Count, "Total card restores the complete lecturer list and clears stale filters");
        Click(cards[1]);
        expect(select.IsDropDownOpen, "Faculty count card opens the existing faculty selector");
        select.CloseDropDown(true);
        foreach (var target in Descendants(cards[0]))
        {
            form.SetFiltersForCheck("not-found"); Click(target);
            expect(form.ResultCount == form.Store.Items.Count, "Total card child opens the full list: " + target.GetType().Name);
        }
        foreach (var target in Descendants(cards[1]))
        {
            Click(target);
            expect(select.IsDropDownOpen, "Faculty count child opens the selector: " + target.GetType().Name);
            select.CloseDropDown(true);
        }
        var tiles = Descendants(faculty).OfType<AntdUI.Panel>().Where(c => c.Controls.OfType<AnimatedDashboardIcon>().Any()).ToArray();
        for (int i = 0; i < tiles.Length; i++)
        {
            string name = DemoData.Faculties[i];
            foreach (var target in Descendants(tiles[i]).Prepend(tiles[i]).ToArray())
            {
                form.SetFiltersForCheck("not-found", "Tất cả khoa", "Tạm nghỉ");
                Click(target);
                expect(select.SelectedValue == name && form.ResultCount == form.Store.Items.Count(c => c.Faculty == name),
                    "Faculty body, icon, count and label select the correct faculty: " + name + "/" + target.GetType().Name);
            }
        }
        foreach (var target in Descendants(cards[2]).Prepend(cards[2]).ToArray())
        {
            Click(cards[0]); Click(target);
            expect(form.ResultCount == form.Store.Items.Count(c => c.ActiveClasses > 0),
                "Assignment card surface shows only lecturers with assigned classes: " + target.GetType().Name);
        }
        form.SetFiltersForCheck("", DemoData.Faculties[0]);
        expect(form.ResultCount == form.Store.Items.Count(c => c.ActiveClasses > 0 && c.Faculty == DemoData.Faculties[0]),
            "Assignment filter combines with the existing faculty filter");
        foreach (var key in new[] { Keys.Enter, Keys.Space })
        {
            Click(cards[0]); Key(cards[2], key);
            expect(form.ResultCount == form.Store.Items.Count(c => c.ActiveClasses > 0), "Assignment card supports keyboard activation: " + key);
            Key(cards[0], key);
            expect(form.ResultCount == form.Store.Items.Count, "Total card clears assignment mode from the keyboard: " + key);
        }
        cards[2].Enabled = false; Click(cards[2]); Click(cards[2].Controls[0]);
        expect(form.ResultCount == form.Store.Items.Count, "Disabled statistic and its child cannot activate");
        cards[2].Enabled = true;
        expect(cards.All(c => c.TabStop && c.Cursor == Cursors.Hand && c.AccessibleRole == AccessibleRole.PushButton),
            "Statistic cards provide named keyboard accessible click surfaces");
        expect(tiles.All(c => c.TabStop && c.Cursor == Cursors.Hand), "Faculty tiles provide keyboard accessible click surfaces");
        expect(cards.Concat(tiles).All(c => new[] { Keys.Enter, Keys.Space }.All(key =>
            (bool)typeof(Control).GetMethod("IsInputKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(c, [key])!)),
            "Card and faculty surfaces receive Enter and Space as input keys");
        Key(tiles[0], Keys.Space);
        expect(form.ResultCount == form.Store.Items.Count(c => c.Faculty == DemoData.Faculties[0]),
            "Faculty tile supports Space activation");
        using (var rebuilt = new DashboardFaculty { Size = new(AppTheme.S(450), AppTheme.S(108)) })
        {
            form.Controls.Add(rebuilt);
            int calls = 0; string? chosen = null;
            rebuilt.FacultyRequested += name => { calls++; chosen = name; };
            rebuilt.Refresh(form.Store.Items);
            var oldTile = Descendants(rebuilt).OfType<AntdUI.Panel>().First(c => c.Controls.OfType<AnimatedDashboardIcon>().Any());
            Click(oldTile.Controls[0]);
            rebuilt.Refresh(form.Store.Items.Skip(1).ToArray());
            var newTile = Descendants(rebuilt).OfType<AntdUI.Panel>().First(c => c.Controls.OfType<AnimatedDashboardIcon>().Any());
            Click(newTile.Controls[0]);
            expect(oldTile.IsDisposed && calls == 2 && chosen == DemoData.Faculties[0],
                "Rebuilt faculty tiles preserve one click action and dispose the old surface");
        }
        Click(cards[0]);
        if (stats.Parent is ScrollableControl body) body.AutoScrollPosition = Point.Empty;
        await Task.Delay(800);
        using (var image = UiCapture.Client(form)) image.Save(Path.Combine(folder, "01-clickable-dashboard.png"), ImageFormat.Png);
        Click(cards[2]);
        await Task.Delay(500);
        using (var image = UiCapture.Client(form)) image.Save(Path.Combine(folder, "02-assigned-lecturers.png"), ImageFormat.Png);
        Click(cards[0]);
        if (stats.Parent is ScrollableControl finalBody) finalBody.AutoScrollPosition = Point.Empty;
        File.WriteAllText(Path.Combine(folder, "environment.json"), System.Text.Json.JsonSerializer.Serialize(new { dpi = form.DeviceDpi, scale = AppTheme.K }));
    }
    private static void Click(Control control) => typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);
    private static void Key(Control control, Keys key)
    {
        foreach (var method in new[] { "OnKeyDown", "OnKeyUp" })
            typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [new KeyEventArgs(key)]);
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var item in Descendants(child)) yield return item; }
    }
}
