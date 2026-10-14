using System.Drawing.Imaging;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Exercises owner painting without a prefilled background, as a native repaint can do.</summary>
internal sealed class WeeklySchedulePaintCheck : Control
{
    internal bool CornersMatchBackground(Control calendar, string folder, string name)
    {
        foreach (var button in calendar.Controls.Cast<Control>()
            .SelectMany(day => day.Controls.OfType<System.Windows.Forms.Button>()))
        {
            if (!ControlCornersMatchBackground(button, folder, name)) return false;
        }
        return true;
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
