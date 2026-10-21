using AntdUI;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;
using Input = AntdUI.Input;
using Select = AntdUI.Select;

namespace QuanLyGiangVien.Dialogs;

/// <summary>Code-built editor; validation and state changes stay in LecturerStore.</summary>
public sealed class LecturerEditor : AntdUI.Window
{
    private readonly LecturerStore store;
    private readonly Lecturer? original;
    private readonly Func<Lecturer, bool, string?>? assignmentValidation;
    private readonly IReadOnlyList<string> facultyChoices;
    private readonly System.Windows.Forms.Panel body;
    private readonly Dictionary<string, (Input Input, AntdUI.Label Error)> fields = new();
    private readonly Input id = new(), name = new(), email = new(), phone = new();
    private readonly DatePicker birth = new();
    private readonly Select gender = new(), faculty = new(), degree = new(), status = new();
    private readonly Avatar avatar;
    private readonly AntdUI.Label summary;
    private readonly string[] portraitKeys;
    private string avatarKey;
    private bool sessionError;
    private readonly List<Font> layoutFonts = [];
    private readonly Motion appearance = new();

    public Lecturer? Result { get; private set; }

    public LecturerEditor(LecturerStore store, Lecturer? original = null, Func<Lecturer, bool, string?>? assignmentValidation = null,
        IReadOnlyList<string>? facultyChoices = null)
    {
        this.store = store;
        this.original = original;
        this.assignmentValidation = assignmentValidation;
        this.facultyChoices = facultyChoices ?? DemoData.Faculties;
        ConfigureWindow();
        portraitKeys = Enumerable.Range(1, 48).Select(index => $"portrait-{index:00}")
            .Where(key => File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "avatars", key + ".png")))
            .ToArray();
        avatarKey = original?.AvatarKey ?? portraitKeys.FirstOrDefault() ?? "portrait-01";
        var chrome = CreateChrome();
        body = chrome.Body;
        avatar = CreateIntroduction();
        AddInputGrid();
        ConfigureFields();
        summary = CreateFooter(chrome.Footer);
        BindBehavior();
        RefreshLayoutFonts();
        Shown += (_, _) => { RefreshLayoutFonts(); appearance.Run(200, progress => Opacity = progress); };
        VisibleChanged += (_, _) => { if (!Visible) appearance.Cancel(); };
    }
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Opacity = Config.Animation ? 0 : 1;
    }

    private void ConfigureWindow()
    {
        AutoHandDpi = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = AppTheme.Font();
        BackColor = Color.White;
        Text = original is null ? "Thêm giảng viên" : "Chỉnh sửa giảng viên";
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(AppTheme.S(640), workArea.Width - AppTheme.S(32)),
            Math.Min(AppTheme.S(680), workArea.Height - AppTheme.S(32)));
    }

    private (System.Windows.Forms.Panel Body, System.Windows.Forms.Panel Footer) CreateChrome()
    {
        var header = new PageHeader {
            Text = "HỒ SƠ GIẢNG VIÊN", Font = AppTheme.Font(8, true), ForeColor = AppTheme.Muted,
            Dock = DockStyle.Top, Height = AppTheme.S(42), ShowButton = true,
            MaximizeBox = false, MinimizeBox = false, DividerShow = false
        };
        var footer = new System.Windows.Forms.Panel {
            Dock = DockStyle.Bottom, Height = AppTheme.S(86), BackColor = AppTheme.Canvas
        };
        var content = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(header);
        return (content, footer);
    }

    private Avatar CreateIntroduction()
    {
        var portrait = new Avatar {
            Location = new Point(AppTheme.S(28), AppTheme.S(8)), Size = new Size(AppTheme.S(70), AppTheme.S(70)),
            Image = Portraits.Get(avatarKey), ImageFit = TFit.Cover, Round = true,
            BackColor = AppTheme.Lilac
        };
        body.Controls.Add(portrait);
        var cycle = AppTheme.Button("Đổi ảnh", "SwapOutlined");
        cycle.SetBounds(AppTheme.S(20), AppTheme.S(78), AppTheme.S(86), AppTheme.S(28));
        cycle.Font = AppTheme.Font(8);
        cycle.Enabled = portraitKeys.Length > 1;
        cycle.Click += (_, _) => CyclePortrait();
        body.Controls.Add(cycle);
        var title = AppTheme.Label(Text, 21, true);
        title.SetBounds(AppTheme.S(124), AppTheme.S(15), AppTheme.S(452), AppTheme.S(36));
        body.Controls.Add(title);
        var subtitle = AppTheme.Label(original is null ? "Tạo hồ sơ mới cho đội ngũ giảng dạy." : "Cập nhật thông tin và trạng thái giảng viên.", 9, color: AppTheme.Muted);
        subtitle.SetBounds(AppTheme.S(124), AppTheme.S(54), AppTheme.S(456), AppTheme.S(25));
        body.Controls.Add(subtitle);
        var required = AppTheme.Label("Các trường có dấu * là bắt buộc", 8, color: AppTheme.Muted);
        required.SetBounds(AppTheme.S(124), AppTheme.S(82), AppTheme.S(456), AppTheme.S(20));
        body.Controls.Add(required);
        return portrait;
    }

    private void AddInputGrid()
    {
        var width = Math.Max(AppTheme.S(200), (ClientSize.Width - AppTheme.S(80)) / 2);
        var right = AppTheme.S(28) + width + AppTheme.S(24);
        AddField(nameof(Lecturer.Id), "Mã giảng viên", id, AppTheme.S(28), AppTheme.S(120), width, 0);
        AddField(nameof(Lecturer.Name), "Họ và tên *", name, right, AppTheme.S(120), width, 1);
        AddField(nameof(Lecturer.BirthDate), "Ngày sinh *", birth, AppTheme.S(28), AppTheme.S(202), width, 2);
        AddField(nameof(Lecturer.Gender), "Giới tính *", gender, right, AppTheme.S(202), width, 3);
        AddField(nameof(Lecturer.Faculty), "Khoa *", faculty, AppTheme.S(28), AppTheme.S(284), width, 4);
        AddField(nameof(Lecturer.Degree), "Học vị *", degree, right, AppTheme.S(284), width, 5);
        AddField(nameof(Lecturer.Email), "Email *", email, AppTheme.S(28), AppTheme.S(366), width, 6);
        AddField(nameof(Lecturer.Phone), "Số điện thoại *", phone, right, AppTheme.S(366), width, 7);
        AddField(nameof(Lecturer.Status), "Trạng thái *", status, AppTheme.S(28), AppTheme.S(448), width, 8);
        var assignment = AppTheme.Label($"{original?.ActiveClasses ?? 0} lớp đang phụ trách", 10, true, AppTheme.Green);
        assignment.SetBounds(right, AppTheme.S(478), width, AppTheme.S(30));
        body.Controls.Add(assignment);
        body.AutoScrollMinSize = new Size(0, AppTheme.S(544));
    }

    private void ConfigureFields()
    {
        id.ReadOnly = true;
        id.TabStop = false;
        id.BackColor = AppTheme.Canvas;
        name.PlaceholderText = "Ví dụ: Nguyễn Hoàng Minh";
        email.PlaceholderText = "giangvien@campus.edu.vn";
        phone.PlaceholderText = "09xxxxxxxx";
        birth.Format = "dd/MM/yyyy";
        birth.PlaceholderText = "dd/mm/yyyy";
        birth.MinDate = DateTime.Today.AddYears(-100);
        birth.MaxDate = DateTime.Today.AddYears(-18);
        ConfigureSelect(gender, ["Nam", "Nữ", "Khác"], "Chọn giới tính");
        ConfigureSelect(faculty, facultyChoices, "Chọn khoa");
        ConfigureSelect(degree, DemoData.Degrees, "Chọn học vị");
        ConfigureSelect(status, ["Đang giảng dạy", "Tạm nghỉ"], "Chọn trạng thái");
    }

    private AntdUI.Label CreateFooter(System.Windows.Forms.Panel footer)
    {
        var validationSummary = AppTheme.Label("", 8, color: AppTheme.Danger);
        validationSummary.SetBounds(AppTheme.S(28), AppTheme.S(7), ClientSize.Width - AppTheme.S(56), AppTheme.S(20));
        footer.Controls.Add(validationSummary);
        var cancel = AppTheme.Button("Hủy");
        var save = AppTheme.Button(original is null ? "Tạo giảng viên" : "Lưu thay đổi", "CheckOutlined", true);
        cancel.SetBounds(ClientSize.Width - AppTheme.S(308), AppTheme.S(31), AppTheme.S(112), AppTheme.S(42));
        save.SetBounds(ClientSize.Width - AppTheme.S(184), AppTheme.S(31), AppTheme.S(156), AppTheme.S(42));
        cancel.TabIndex = 9;
        save.TabIndex = 10;
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        save.Click += (_, _) => SaveAndClose();
        footer.Controls.Add(cancel);
        footer.Controls.Add(save);
        return validationSummary;
    }

    private void BindBehavior()
    {
        KeyDown += (_, args) => {
            if (args.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); args.Handled = true; }
            else if (args.KeyCode == Keys.Enter) { SaveAndClose(); args.Handled = true; args.SuppressKeyPress = true; }
        };
        if (original is not null) FillForTest(original);
        else { id.Text = store.NextId(); status.SelectedIndex = 0; }
        Shown += (_, _) => name.Focus();
    }

    private void RefreshLayoutFonts()
    {
        var previous = layoutFonts.ToArray();
        layoutFonts.Clear();
        layoutFonts.AddRange(AppTheme.RefreshFonts(this));
        foreach (var font in previous) font.Dispose();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) appearance.Dispose();
        base.Dispose(disposing);
        if (disposing) { foreach (var font in layoutFonts) font.Dispose(); layoutFonts.Clear(); }
    }
    public void FillForTest(Lecturer value)
    {
        id.Text = value.Id;
        name.Text = value.Name;
        birth.Value = value.BirthDate == default ? null : value.BirthDate;
        gender.SelectedValue = value.Gender;
        faculty.SelectedValue = value.Faculty;
        degree.SelectedValue = value.Degree;
        email.Text = value.Email;
        phone.Text = value.Phone;
        status.SelectedValue = value.Status;
        avatarKey = value.AvatarKey;
        avatar.Image = Portraits.Get(avatarKey);
    }

    /// <summary>Returns success without closing, so the caller can inspect validation state.</summary>
    public bool TrySave()
    {
        sessionError = false;
        foreach (var field in fields.Values) { field.Input.Status = TType.None; field.Error.Text = ""; }
        summary.Text = "";
        summary.ForeColor = AppTheme.Danger;
        var value = new Lecturer(id.Text, name.Text, birth.Value ?? default, Selected(gender), email.Text,
            phone.Text, Selected(faculty), Selected(degree), Selected(status), avatarKey, original?.ActiveClasses ?? 0, original?.Version ?? "");
        var assignmentError = assignmentValidation?.Invoke(value, original is null);
        if (assignmentError != null)
        {
            status.Status = TType.Error; summary.Text = assignmentError;
            body.ScrollControlIntoView(status); status.Focus(); return false;
        }
        if (!store.TrySave(value, original is null, out var errors))
        {
            sessionError = errors.ContainsKey("_session");
            foreach (var error in errors)
                if (fields.TryGetValue(error.Key, out var field)) { field.Input.Status = TType.Error; field.Error.Text = error.Value; }
            summary.Text = errors.Keys.Any(fields.ContainsKey) ? "Vui lòng kiểm tra các trường được đánh dấu bên trên."
                : errors.Values.FirstOrDefault() ?? "Không thể lưu hồ sơ. Vui lòng thử lại.";
            var first = fields.Values.FirstOrDefault(field => field.Input.Status == TType.Error).Input;
            if (first is not null) { body.ScrollControlIntoView(first); first.Focus(); }
            return false;
        }
        Result = store.Items.First(item => item.Id.Equals(value.Id.Trim(), StringComparison.OrdinalIgnoreCase));
        return true;
    }

    internal void SessionRecovered()
    {
        if (!sessionError) return;
        sessionError = false;
        summary.Text = "Đã đăng nhập lại. Bản nháp chưa được lưu.";
        summary.ForeColor = AppTheme.Muted;
    }

    private void SaveAndClose()
    {
        if (!TrySave()) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void AddField(string key, string caption, Input input, int x, int y, int width, int tabIndex)
    {
        var label = AppTheme.Label(caption, 9, true);
        label.SetBounds(x, y, width, AppTheme.S(21));
        body.Controls.Add(label);
        input.SetBounds(x, y + AppTheme.S(24), width, AppTheme.S(38));
        input.Font = AppTheme.Font(9);
        input.Radius = 10;
        input.WaveSize = 0;
        input.BorderColor = AppTheme.Line;
        input.ForeColor = AppTheme.Ink;
        input.TabIndex = tabIndex;
        body.Controls.Add(input);
        var error = AppTheme.Label("", 7.5f, color: AppTheme.Danger);
        error.SetBounds(x, y + AppTheme.S(63), width, AppTheme.S(18));
        body.Controls.Add(error);
        fields.Add(key, (input, error));
    }

    private static void ConfigureSelect(Select select, IEnumerable<string> choices, string placeholder)
    {
        select.Items.AddRange(choices.Cast<object>().ToArray());
        select.PlaceholderText = placeholder;
        select.DropDownRadius = 12;
        select.ListAutoWidth = true;
    }

    private static string Selected(Select select) => select.SelectedValue?.ToString() ?? "";

    private void CyclePortrait()
    {
        if (portraitKeys.Length == 0) return;
        var current = Array.IndexOf(portraitKeys, avatarKey);
        avatarKey = portraitKeys[(current + 1) % portraitKeys.Length];
        avatar.Image = Portraits.Get(avatarKey);
    }
}
