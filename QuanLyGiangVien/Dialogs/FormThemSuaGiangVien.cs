using AntdUI;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;
using Input = AntdUI.Input;
using Select = AntdUI.Select;

namespace QuanLyGiangVien.Dialogs;

/// <summary>Form thêm/sửa dựng bằng C#; LecturerStore kiểm tra dữ liệu và thực hiện lưu hồ sơ.</summary>
public sealed class LecturerEditor : AntdUI.Window
{
    // Giữ kho dữ liệu, hồ sơ gốc và quy tắc phân công để phân biệt thêm mới với chỉnh sửa.
    private readonly LecturerStore store;
    private readonly Lecturer? original;
    private readonly Func<Lecturer, bool, string?>? assignmentValidation;
    private readonly IReadOnlyList<string> facultyChoices;
    private readonly System.Windows.Forms.Panel body;
    // Ánh xạ tên thuộc tính sang ô nhập/nhãn lỗi để trả lỗi kiểm tra về đúng trường trên form.
    private readonly Dictionary<string, (Input Input, AntdUI.Label Error)> fields = new();
    private readonly Input id = new(), name = new(), email = new(), phone = new();
    private readonly DatePicker birth = new();
    private readonly Select gender = new(), faculty = new(), degree = new(), status = new();
    private readonly Avatar avatar;
    private readonly AntdUI.Label summary;
    // Danh sách ảnh có sẵn, trạng thái lỗi phiên đăng nhập và tài nguyên font/hiệu ứng do form quản lý.
    private readonly string[] portraitKeys;
    private string avatarKey;
    private bool sessionError;
    private readonly List<Font> layoutFonts = [];
    private readonly Motion appearance = new();

    // Form cha lấy hồ sơ đã lưu qua Result khi hộp thoại đóng với DialogResult.OK.
    public Lecturer? Result { get; private set; }

    // Nhận hồ sơ gốc (null khi thêm), dựng vùng nhập liệu và chuẩn bị dữ liệu/lựa chọn ban đầu.
    public LecturerEditor(LecturerStore store, Lecturer? original = null, Func<Lecturer, bool, string?>? assignmentValidation = null,
        IReadOnlyList<string>? facultyChoices = null)
    {
        this.store = store;
        this.original = original;
        this.assignmentValidation = assignmentValidation;
        this.facultyChoices = facultyChoices ?? DemoData.Faculties;
        ConfigureWindow();
        // Chỉ cho chuyển giữa những ảnh thực sự có trong thư mục Assets của ứng dụng đang chạy.
        portraitKeys = Enumerable.Range(1, 48).Select(index => $"portrait-{index:00}")
            .Where(key => File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "avatars", key + ".png")))
            .ToArray();
        avatarKey = original?.AvatarKey ?? portraitKeys.FirstOrDefault() ?? "portrait-01";
        // Tạo khung trước rồi thêm phần giới thiệu, lưới ô nhập và thanh nút cố định phía dưới.
        var chrome = CreateChrome();
        body = chrome.Body;
        avatar = CreateIntroduction();
        AddInputGrid();
        ConfigureFields();
        summary = CreateFooter(chrome.Footer);
        BindBehavior();
        RefreshLayoutFonts();
        // Hiệu ứng mở tăng độ rõ của cửa sổ; hủy hiệu ứng nếu form bị ẩn giữa chừng.
        Shown += (_, _) => { RefreshLayoutFonts(); appearance.Run(200, progress => Opacity = progress); };
        VisibleChanged += (_, _) => { if (!Visible) appearance.Cancel(); };
    }
    // Chuẩn bị độ trong suốt ban đầu tùy cài đặt chuyển động trước khi form xuất hiện.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Opacity = Config.Animation ? 0 : 1;
    }

    // Đặt tiêu đề theo chế độ thêm/sửa, mở giữa form cha và giới hạn kích thước theo màn hình.
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

    // Tách phần nội dung có thể cuộn khỏi thanh tiêu đề và thanh nút để luôn thao tác được nút Lưu/Hủy.
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

    // Dựng ảnh đại diện, nút đổi ảnh, tiêu đề và hướng dẫn nhập trường bắt buộc ở đầu form.
    private Avatar CreateIntroduction()
    {
        var portrait = new Avatar {
            Location = new Point(AppTheme.S(28), AppTheme.S(8)), Size = new Size(AppTheme.S(70), AppTheme.S(70)),
            Image = Portraits.Get(avatarKey), ImageFit = TFit.Cover, Round = true,
            BackColor = AppTheme.Lilac
        };
        body.Controls.Add(portrait);
        // Chỉ bật đổi ảnh khi có ít nhất hai ảnh; bấm nút sẽ chọn ảnh kế tiếp trong bộ có sẵn.
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

    // Sắp xếp các trường thành hai cột; AddField tạo nhãn, ô nhập và vị trí hiển thị lỗi cho từng trường.
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
        // Số lớp là thông tin tham khảo lấy từ hồ sơ, không cho sửa trực tiếp trong form giảng viên.
        var assignment = AppTheme.Label($"{original?.ActiveClasses ?? 0} lớp đang phụ trách", 10, true, AppTheme.Green);
        assignment.SetBounds(right, AppTheme.S(478), width, AppTheme.S(30));
        body.Controls.Add(assignment);
        body.AutoScrollMinSize = new Size(0, AppTheme.S(544));
    }

    // Cấu hình ô nhập: mã chỉ đọc, ngày sinh theo độ tuổi và các danh mục giới tính/khoa/học vị/trạng thái.
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

    // Tạo dòng báo lỗi chung và hai nút Hủy/Lưu; nút Lưu chỉ đóng form sau khi kiểm tra thành công.
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

    // Gắn phím Esc để hủy, Enter để lưu và nạp hồ sơ cũ hoặc sinh mã cho hồ sơ mới.
    private void BindBehavior()
    {
        KeyDown += (_, args) => {
            if (args.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); args.Handled = true; }
            else if (args.KeyCode == Keys.Enter) { SaveAndClose(); args.Handled = true; args.SuppressKeyPress = true; }
        };
        // Hàm FillForTest cũng được dùng để điền hồ sơ thật khi mở chế độ chỉnh sửa.
        if (original is not null) FillForTest(original);
        else { id.Text = store.NextId(); status.SelectedIndex = 0; }
        Shown += (_, _) => name.Focus();
    }

    // Thay font theo tỷ lệ hiển thị hiện hành rồi giải phóng bộ font do lần cập nhật trước tạo ra.
    private void RefreshLayoutFonts()
    {
        var previous = layoutFonts.ToArray();
        layoutFonts.Clear();
        layoutFonts.AddRange(AppTheme.RefreshFonts(this));
        foreach (var font in previous) font.Dispose();
    }
    // Dừng hiệu ứng và giải phóng font tự tạo khi form kết thúc vòng đời.
    protected override void Dispose(bool disposing)
    {
        if (disposing) appearance.Dispose();
        base.Dispose(disposing);
        if (disposing) { foreach (var font in layoutFonts) font.Dispose(); layoutFonts.Clear(); }
    }
    // Điền toàn bộ thông tin và ảnh từ một hồ sơ vào các control; dùng cho sửa hồ sơ và kiểm chứng.
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

    /// <summary>Kiểm tra và lưu dữ liệu, trả kết quả nhưng chưa đóng form để bên gọi xem trạng thái lỗi.</summary>
    public bool TrySave()
    {
        // Xóa dấu lỗi của lần lưu trước để kết quả lần kiểm tra mới được hiển thị độc lập.
        sessionError = false;
        foreach (var field in fields.Values) { field.Input.Status = TType.None; field.Error.Text = ""; }
        summary.Text = "";
        summary.ForeColor = AppTheme.Danger;
        // Gom dữ liệu trên form thành hồ sơ mới; giữ số lớp và phiên bản gốc để kiểm tra khi cập nhật.
        var value = new Lecturer(id.Text, name.Text, birth.Value ?? default, Selected(gender), email.Text,
            phone.Text, Selected(faculty), Selected(degree), Selected(status), avatarKey, original?.ActiveClasses ?? 0, original?.Version ?? "");
        // Kiểm tra ràng buộc phân công trước, chẳng hạn trạng thái giảng viên không còn phù hợp với lớp đang dạy.
        var assignmentError = assignmentValidation?.Invoke(value, original is null);
        if (assignmentError != null)
        {
            status.Status = TType.Error; summary.Text = assignmentError;
            body.ScrollControlIntoView(status); status.Focus(); return false;
        }
        // Kho kiểm tra các trường và lưu qua nguồn dữ liệu đã cấu hình; thất bại thì giữ form để sửa lỗi.
        if (!store.TrySave(value, original is null, out var errors))
        {
            sessionError = errors.ContainsKey("_session");
            // Lỗi có tên trường được đánh dấu tại ô tương ứng; lỗi chung/phiên đăng nhập hiện ở dòng tổng hợp.
            foreach (var error in errors)
                if (fields.TryGetValue(error.Key, out var field)) { field.Input.Status = TType.Error; field.Error.Text = error.Value; }
            summary.Text = errors.Keys.Any(fields.ContainsKey) ? "Vui lòng kiểm tra các trường được đánh dấu bên trên."
                : errors.Values.FirstOrDefault() ?? "Không thể lưu hồ sơ. Vui lòng thử lại.";
            // Cuộn tới và đặt con trỏ ở trường lỗi đầu tiên để người dùng biết nơi cần sửa.
            var first = fields.Values.FirstOrDefault(field => field.Input.Status == TType.Error).Input;
            if (first is not null) { body.ScrollControlIntoView(first); first.Focus(); }
            return false;
        }
        // Lấy lại hồ sơ sau khi lưu để trả đúng dữ liệu đã chuẩn hóa và phiên bản cập nhật trong kho.
        Result = store.Items.First(item => item.Id.Equals(value.Id.Trim(), StringComparison.OrdinalIgnoreCase));
        return true;
    }

    // Sau khi đăng nhập lại, đổi thông báo lỗi phiên thành lời nhắc rằng bản nháp vẫn chưa được lưu.
    internal void SessionRecovered()
    {
        if (!sessionError) return;
        sessionError = false;
        summary.Text = "Đã đăng nhập lại. Bản nháp chưa được lưu.";
        summary.ForeColor = AppTheme.Muted;
    }

    // Chỉ trả OK và đóng cửa sổ khi TrySave thành công; lỗi thì giữ nguyên dữ liệu nhập để chỉnh sửa.
    private void SaveAndClose()
    {
        if (!TrySave()) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    // Tạo một trường nhập có nhãn/lỗi, thứ tự Tab và đăng ký vào bảng ánh xạ dùng khi hiển thị lỗi lưu.
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

    // Nạp lựa chọn và gợi ý cho hộp chọn, đồng thời thống nhất kiểu hiển thị danh sách xổ xuống.
    private static void ConfigureSelect(Select select, IEnumerable<string> choices, string placeholder)
    {
        select.Items.AddRange(choices.Cast<object>().ToArray());
        select.PlaceholderText = placeholder;
        select.DropDownRadius = 12;
        select.ListAutoWidth = true;
    }

    // Đọc giá trị hộp chọn; trả chuỗi rỗng khi chưa chọn để bộ kiểm tra nhận biết trường còn thiếu.
    private static string Selected(Select select) => select.SelectedValue?.ToString() ?? "";

    // Chọn ảnh tiếp theo trong danh sách và quay về ảnh đầu khi đã tới ảnh cuối.
    private void CyclePortrait()
    {
        if (portraitKeys.Length == 0) return;
        var current = Array.IndexOf(portraitKeys, avatarKey);
        avatarKey = portraitKeys[(current + 1) % portraitKeys.Length];
        avatar.Image = Portraits.Get(avatarKey);
    }
}
