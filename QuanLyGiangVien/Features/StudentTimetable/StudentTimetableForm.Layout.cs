using QuanLyGiangVien.Data;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.StudentTimetable.TimetableStyle;

namespace QuanLyGiangVien.Features.StudentTimetable;

internal sealed partial class StudentTimetableForm
{
    private PictureBox Picture(string file)
    {
        Bitmap bitmap;
        if (file == "graduation-cap.png")
        {
            var color = AppTheme.Primary;
            var svg = Icon("graduation-cap").Replace("currentColor", $"#{color.R:X2}{color.G:X2}{color.B:X2}", StringComparison.Ordinal);
            bitmap = AntdUI.SvgExtend.SvgToBmp(svg, 64, 64, null)
                ?? throw new InvalidOperationException("Không thể hiển thị biểu tượng sinh viên.");
        }
        else
        {
            var path = file == "programming.png"
                ? Path.Combine(AppContext.BaseDirectory, "Assets", "scheduling", "resource-visuals", "coding.png") : Asset(file);
            using var original = Image.FromFile(path);
            bitmap = new Bitmap(original);
        }
        images.Add(bitmap);
        return new PictureBox { Image = bitmap, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, TabStop = false };
    }
    private void BuildSidebar()
    {
        profile = Card(); summary = Card(); detail = Card();
        summary.Back = AppTheme.Lilac;
        var portrait = new AntdUI.Avatar { Image = Portraits.Get("portrait-04"), Round = true, ImageFit = AntdUI.TFit.Cover,
            BackColor = Color.White };
        Put(profile, portrait, 96, 18, 68, 68);
        var name = Label(studentName, 12, true); name.TextAlign = ContentAlignment.MiddleCenter;
        Put(profile, name, 16, 96, 228, 29);
        var id = Label(studentId, 9, false, Muted); id.TextAlign = ContentAlignment.MiddleCenter;
        Put(profile, id, 16, 128, 228, 24);
        Put(profile, Label("NGÀNH HỌC", 8, true, Muted), 22, 166, 216, 23);
        Put(profile, Label(studentMajor, 10, true), 22, 190, 216, 25);
        Put(profile, Label(studentClass, 9, false, Muted), 22, 219, 216, 23);
        Put(profile, Label("Đang theo học", 9, true, Teal), 22, 251, 216, 25);
        Put(summary, Label("Học kỳ của bạn", 12, true), 22, 18, 216, 30);
        summaryText = Label("", 10, false); summaryText.TextAlign = ContentAlignment.TopLeft;
        Put(summary, summaryText, 22, 59, 216, 120);
        Put(detail, Label("CHI TIẾT BUỔI HỌC", 8, true, PurpleDark), 22, 18, 216, 25);
        detailTitle = Label("", 12, true); detailTitle.TextAlign = ContentAlignment.TopLeft;
        Put(detail, detailTitle, 22, 57, 216, 58);
        detailBody = Label("", 9, false, Muted); detailBody.TextAlign = ContentAlignment.TopLeft;
        Put(detail, detailBody, 22, 110, 216, 205);
        scroll.Controls.AddRange([profile, summary, detail]);
    }
    private void LayoutPage()
    {
        if (building || scroll == null) return;
        SurfaceTransition.Cancel(calendar); SurfaceTransition.Cancel(calendarShell); SurfaceTransition.Cancel(intro);
        top.SetBounds(0, 0, ClientSize.Width, S(84));
        top.Controls["headerUser"]?.SetBounds(ClientSize.Width - S(260), S(39), S(230), S(34));
        int headerHeight = embedded ? 0 : S(84);
        scroll.SetBounds(0, headerHeight, ClientSize.Width, ClientSize.Height - headerHeight);
        scroll.SuspendLayout();
        // A fixed logical content height keeps periods legible; small windows scroll vertically.
        int width = Math.Max(950, (int)(scroll.ClientSize.Width / AppTheme.K) - 18);
        int main = width - 56 - 260 - 20;
        var offset = scroll.AutoScrollPosition;
        void Place(Control c, int x, int y, int w, int h) => c.SetBounds(S(x) + offset.X, S(y) + offset.Y, S(w), S(h));
        Place(greeting, 28, 19, main, 38); Place(introCaption, 28, 61, main, 24);
        int introWidth = (int)(main * .56);
        Place(intro, 28, 104, introWidth, 136); Place(nextCard, 28 + introWidth + 18, 104, main - introWidth - 18, 136);
        intro.Controls["introArt"]?.SetBounds(S(introWidth - 150), S(20), S(132), S(96));
        // Keep the illustration separate from text even in the compact layout.
        intro.Controls[1].Width = S(Math.Max(180, introWidth - 175));
        intro.Controls[1].Text = introWidth < 460 ? $"Chào {FirstName}!" : $"Chào {FirstName}, học tốt nhé!";
        intro.Controls[2].Width = S(Math.Max(180, introWidth - 175));
        nextTitle.Width = nextDescription.Width = nextCard.Width - S(40);
        Place(calendarShell, 28, 260, main, 648);
        calendar.SetBounds(S(14), S(100), calendarShell.Width - S(28), S(530));
        (semester.Parent is SoftInputSurface surface ? surface : (Control)semester)
            .SetBounds(calendarShell.Width - S(278), S(10), S(256), S(40));
        previous.SetBounds(calendarShell.Width - S(218), S(55), S(36), S(32));
        today.SetBounds(calendarShell.Width - S(174), S(55), S(108), S(32));
        next.SetBounds(calendarShell.Width - S(58), S(55), S(36), S(32));
        Place(profile, 28 + main + 20, 104, 260, 294);
        Place(summary, 28 + main + 20, 416, 260, 184);
        Place(detail, 28 + main + 20, 618, 260, 326);
        scroll.AutoScrollMinSize = new(0, S(970));
        scroll.ResumeLayout();
    }
}
