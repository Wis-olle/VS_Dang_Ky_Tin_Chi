using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Native regression checks for the lecturer list after removing hover previews.</summary>
internal static class ListHoverUiCheck
{
    internal static async Task Run(MainForm form, string folder, Action<bool, string> expect)
    {
        File.WriteAllText(Path.Combine(folder, "list-hover-environment.json"),
            JsonSerializer.Serialize(new { form.DeviceDpi, LayoutScale = AppTheme.K, form.ClientSize }));
        var table = Descendants(form).OfType<AntdUI.Table>().Single();
        var grid = Descendants(form).OfType<LecturerGrid>().Single();
        form.SetTableForCheck(true);
        await Task.Delay(400);
        var selected = form.SelectedIdForCheck;
        var focus = form.ActiveControl;
        var bounds = table.Bounds;
        for (int row = 0; row < 3; row++)
        {
            MoveRow(table, row);
            expect(NoPreview(form), "Hovering table row " + row + " creates no lecturer preview window");
            await Task.Delay(400);
        }
        expect(NoPreview(form), "Settled and repeated table hover leaves no lecturer preview window");
        expect(form.ActiveControl == focus && form.SelectedIdForCheck == selected && !form.HasOpenProfileDrawer,
            "Table hover preserves keyboard focus, selected lecturer and closed profile drawer");
        expect(table.Bounds == bounds, "Table hover keeps interactive bounds fixed");
        form.SetFiltersForCheck("", "Ngoại ngữ");
        await Task.Delay(350);
        MoveRow(table, 0);
        expect(form.ResultCount == 3 && NoPreview(form), "Filtered lecturer rows remain usable without hover previews");
        form.SetFiltersForCheck("");
        await Task.Delay(350);
        table.SetSortIndex(Enumerable.Range(0, form.ResultCount).Reverse().ToArray());
        MoveRow(table, 0);
        expect((table.GetRow(0)?.record as LecturerRow)?.Id == form.Store.Items[^1].Id && NoPreview(form),
            "Sorted table rows show the current lecturer without creating a preview");
        table.SetSortIndex(Enumerable.Range(0, form.ResultCount).ToArray());
        table.ScrollBar.ValueY = 57;
        MoveRow(table, 2);
        expect(NoPreview(form), "Scrolling and hovering table rows creates no lecturer preview");
        table.ScrollBar.ValueY = 0;
        Move(table, new(80, 12));
        expect(NoPreview(form), "Table header hover creates no lecturer preview");
        await CheckTableSelection(form, table, expect);
        await CheckCardSelection(form, grid, expect);
        form.SetTableForCheck(true);
        await Task.Delay(400);
        MoveRow(table, 1);
        expect(form.ResultCount == 12 && NoPreview(form), "Switching back to table view preserves records without restoring hover previews");
        using var image = UiCapture.Client(form);
        image.Save(Path.Combine(folder, "33-table-without-preview.png"), ImageFormat.Png);
        Leave(table);
    }

    private static async Task CheckTableSelection(MainForm form, AntdUI.Table table, Action<bool, string> expect)
    {
        var column = table.Columns.Single(column => column.Key == "Name");
        var cell = table.CellRectangle(2, column.INDEX);
        var point = new Point(cell.Left + 20, cell.Top + cell.Height / 2);
        Click(table, point);
        await Task.Delay(350);
        expect(form.SelectedIdForCheck == (table.GetRow(1)?.record as LecturerRow)?.Id
            && form.HasOpenProfileDrawer && NoPreview(form),
            "Clicking a table lecturer still opens the selected profile drawer");
        form.CloseProfileForCheck();
        await Task.Delay(350);
        expect(!form.HasOpenProfileDrawer, "The existing lecturer profile drawer closes normally");
    }

    private static async Task CheckCardSelection(MainForm form, LecturerGrid grid, Action<bool, string> expect)
    {
        form.SetTableForCheck(false);
        await Task.Delay(400);
        var cards = grid.Controls.OfType<LecturerCard>().ToArray();
        var bounds = cards.Select(card => card.Bounds).ToArray();
        var selected = form.SelectedIdForCheck;
        var focus = form.ActiveControl;
        Move(cards[0], new(35, 90));
        await Task.Delay(250);
        expect(NoPreview(form) && form.SelectedIdForCheck == selected && form.ActiveControl == focus,
            "Card hover preserves focus and selection without an information preview");
        expect(cards.Select(card => card.Bounds).SequenceEqual(bounds), "Card hover preserves interactive card bounds");
        Invoke(cards[0], "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, 35, 90, 0));
        expect(form.SelectedIdForCheck == cards[0].Lecturer.Id && NoPreview(form),
            "Existing card click still selects its lecturer");
        Invoke(cards[1], "OnKeyDown", new KeyEventArgs(Keys.Enter));
        expect(form.SelectedIdForCheck == cards[1].Lecturer.Id && NoPreview(form),
            "Existing card Enter key still selects its lecturer");
        Leave(cards[0]);
    }

    private static bool NoPreview(MainForm form) =>
        !form.OwnedForms.Any(window => window.GetType().Name == "LecturerInfoPreview");
    private static void MoveRow(AntdUI.Table table, int row)
    {
        var column = table.Columns.Single(column => column.Key == "Name");
        var cell = table.CellRectangle(row + 1, column.INDEX);
        Move(table, new(cell.Left + 18 - table.ScrollBar.ValueX,
            cell.Top + cell.Height / 2 - table.ScrollBar.ValueY));
    }
    private static void Click(Control control, Point point)
    {
        var args = new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0);
        Invoke(control, "OnMouseDown", args);
        Invoke(control, "OnMouseUp", args);
    }
    private static void Move(Control control, Point point) =>
        Invoke(control, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
    private static void Leave(Control control) => Invoke(control, "OnMouseLeave", EventArgs.Empty);
    private static void Invoke(Control control, string method, object args) =>
        control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [args]);
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in Descendants(control)) yield return child;
        }
    }
}
