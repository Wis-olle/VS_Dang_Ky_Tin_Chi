using System.Drawing.Imaging;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Exercises owner painting without a prefilled background, as a native repaint can do.</summary>
internal sealed class WeeklySchedulePaintCheck : Control
{
    internal bool CornersMatchBackground(Control calendar, string folder, string name)
    {
        var buttons = Descendants(calendar).OfType<Button>()
            .Where(button => button is WeeklySessionButton or WeeklyGroupButton).ToArray();
        if (buttons.Length == 0) return false;
        foreach (var button in buttons)
        {
            if (!ControlCornersMatchBackground(button, folder, name)) return false;
        }
        return true;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    internal bool ControlCornersMatchBackground(Control button, string folder, string name)
    {
        using var bitmap = new Bitmap(button.Width, button.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            InvokePaint(button, new PaintEventArgs(graphics, button.ClientRectangle));
        }
        var corners = new[] {
            new Point(0, 0), new Point(button.Width - 1, 0),
            new Point(0, button.Height - 1), new Point(button.Width - 1, button.Height - 1)
        };
        if (corners.All(point => bitmap.GetPixel(point.X, point.Y).ToArgb() == button.BackColor.ToArgb())) return true;
        bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
        return false;
    }
}
