using AntdUI;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;
using Label = AntdUI.Label;
using Panel = AntdUI.Panel;

namespace QuanLyGiangVien.Panels;

public sealed class LecturerProfile : Panel
{
    private readonly System.Windows.Forms.Panel scroll = new() { AutoScroll = true, BackColor = Color.White };
    private readonly AntdUI.Button edit = AppTheme.Button("Chỉnh sửa hồ sơ", "EditOutlined", true);
    private readonly Label heading = AppTheme.Label("HỒ SƠ GIẢNG VIÊN", 7.5f, true, AppTheme.Muted);
    private readonly Label name = AppTheme.Label("", 12.5f, true);
    private readonly Label subtitle = AppTheme.Label("", 8, false, AppTheme.Muted);
    private readonly Avatar avatar = new() { ImageFit = TFit.Cover, Round = true, BackColor = Color.White };
    private readonly Tag status = new() { Radius = 8, BorderWidth = 0, Font = AppTheme.Font(7.5f) };
    private readonly Label[] labels = new Label[5];
    private readonly Label[] values = new Label[5];
    private readonly System.Windows.Forms.FlowLayoutPanel classes = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
    private readonly Label classTitle = AppTheme.Label("LỚP ĐƯỢC PHÂN CÔNG", 8.5f, true, AppTheme.Muted);
    private Lecturer? current;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.Browsable(false)]
    public Func<Lecturer, IReadOnlyList<TeachingClass>>? ClassesProvider { get; set; }
    public event Action<Lecturer>? EditRequested;
    public LecturerProfile()
    {
        Radius = 18;
        BorderWidth = 1;
        BorderColor = AppTheme.Line;
        Back = Color.White;
        BackColor = Color.Transparent;
        Shadow = 0;
        Controls.Add(scroll);
        Controls.Add(edit);
        scroll.Controls.AddRange([heading, avatar, name, subtitle, status, classTitle, classes]);
        string[] titles = ["Ngày sinh", "Giới tính", "Email", "Điện thoại", "Học vị"];
        for (int i = 0; i < titles.Length; i++)
        {
            labels[i] = AppTheme.Label(titles[i], 8, false, AppTheme.Muted);
            values[i] = AppTheme.Label("", 8.5f, true);
            scroll.Controls.AddRange([labels[i], values[i]]);
        }
        edit.Click += (_, _) => { if (current != null) EditRequested?.Invoke(current); };
        Resize += (_, _) => Relayout();
        scroll.Scroll += (_, _) => SurfaceTransition.Cancel(this);
    }
    public void ShowLecturer(Lecturer? lecturer, bool animate = true)
    {
        SurfaceTransition.Cancel(this);
        if (current == lecturer && ClassesProvider == null) return;
        using var previous = animate ? SurfaceTransition.Capture(this) : null;
        current = lecturer;
        edit.Enabled = lecturer != null;
        avatar.Image = lecturer == null ? null : Portraits.Get(lecturer.AvatarKey);
        name.Text = lecturer?.Name ?? "Chọn một giảng viên";
        subtitle.Text = lecturer == null ? "Hồ sơ sẽ hiển thị tại đây" : $"{lecturer.Id}  ·  {lecturer.Faculty}";
        status.Visible = lecturer != null;
        status.Text = lecturer?.Status ?? "";
        status.BackColor = lecturer?.Status == "Tạm nghỉ" ? AppTheme.Peach : AppTheme.Mint;
        status.ForeColor = lecturer?.Status == "Tạm nghỉ" ? AppTheme.Ink : AppTheme.Green;
        string[] details = lecturer == null ? ["—", "—", "—", "—", "—"] :
            [lecturer.BirthDate.ToString("dd/MM/yyyy"), lecturer.Gender, lecturer.Email, lecturer.Phone, lecturer.Degree];
        for (int i = 0; i < details.Length; i++) values[i].Text = details[i];
        foreach (Control control in classes.Controls.Cast<Control>().ToArray()) control.Dispose();
        var teaching = lecturer == null ? [] : ClassesProvider?.Invoke(lecturer) ?? DemoData.Classes(lecturer);
        classTitle.Text = lecturer == null ? "LỚP ĐƯỢC PHÂN CÔNG" : $"LỚP ĐƯỢC PHÂN CÔNG · {teaching.Count}";
        if (teaching.Count == 0)
        {
            var empty = AppTheme.Label("Chưa có lớp được phân công\ntrong học kỳ này.", 9, false, AppTheme.Muted);
            empty.Size = new(AppTheme.S(245), AppTheme.S(44));
            classes.Controls.Add(empty);
        }
        foreach (var item in teaching)
        {
            var card = new Panel { Back = AppTheme.Tone(lecturer!.Faculty), BackColor = Color.White, Radius = 9, Size = new(AppTheme.S(250), AppTheme.S(44)), Margin = new(0, 0, 0, AppTheme.S(6)) };
            var title = AppTheme.Label(item.Name, 8.5f, true);
            title.SetBounds(AppTheme.S(10), AppTheme.S(3), AppTheme.S(225), AppTheme.S(22));
            var schedule = AppTheme.Label($"{item.Schedule}  ·  {item.Room}", 8, false, AppTheme.Muted);
            schedule.SetBounds(AppTheme.S(10), AppTheme.S(25), AppTheme.S(225), AppTheme.S(18));
            card.Controls.AddRange([title, schedule]);
            classes.Controls.Add(card);
        }
        scroll.AutoScrollPosition = Point.Empty;
        Relayout();
        if (previous != null) SurfaceTransition.Crossfade(this, new Bitmap(previous));
    }
    private void Relayout()
    {
        int pad = AppTheme.S(18), width = Math.Max(AppTheme.S(180), Width - pad * 2);
        edit.SetBounds(pad, Height - AppTheme.S(50), width, AppTheme.S(36));
        scroll.SetBounds(AppTheme.S(8), AppTheme.S(12), Width - AppTheme.S(16), Math.Max(AppTheme.S(100), Height - AppTheme.S(78)));
        int bodyWidth = Math.Max(AppTheme.S(180), scroll.ClientSize.Width - pad * 2 - SystemInformation.VerticalScrollBarWidth);
        heading.SetBounds(pad, AppTheme.S(10), bodyWidth - AppTheme.S(124), AppTheme.S(20));
        status.SetBounds(pad + bodyWidth - AppTheme.S(112), AppTheme.S(8), AppTheme.S(112), AppTheme.S(22));
        avatar.SetBounds(pad, AppTheme.S(38), AppTheme.S(56), AppTheme.S(56));
        name.SetBounds(pad + AppTheme.S(68), AppTheme.S(39), bodyWidth - AppTheme.S(68), AppTheme.S(27));
        subtitle.SetBounds(pad + AppTheme.S(68), AppTheme.S(70), bodyWidth - AppTheme.S(68), AppTheme.S(18));
        LayoutDetails(pad, bodyWidth);
        classTitle.SetBounds(pad, AppTheme.S(214), bodyWidth, AppTheme.S(22));
        classes.SetBounds(pad, AppTheme.S(240), bodyWidth, Math.Max(AppTheme.S(44), classes.Controls.Count * AppTheme.S(50)));
        foreach (Control card in classes.Controls)
        {
            card.Width = bodyWidth;
            foreach (Control child in card.Controls) child.Width = Math.Max(AppTheme.S(80), bodyWidth - AppTheme.S(20));
        }
        scroll.AutoScrollMinSize = new(0, classes.Bottom + AppTheme.S(16));
    }

    private void LayoutDetails(int pad, int bodyWidth)
    {
        int columnWidth = (bodyWidth - AppTheme.S(18)) / 2;
        int right = pad + columnWidth + AppTheme.S(18);
        SetDetail(0, pad, 99, columnWidth);
        SetDetail(1, right, 99, columnWidth);
        SetDetail(2, pad, 136, bodyWidth);
        SetDetail(3, pad, 173, columnWidth);
        SetDetail(4, right, 173, columnWidth);
    }

    private void SetDetail(int index, int x, int y, int width)
    {
        labels[index].SetBounds(x, AppTheme.S(y), width, AppTheme.S(14));
        values[index].SetBounds(x, AppTheme.S(y + 13), width, AppTheme.S(21));
    }
}
