using AntdUI;
using QuanLyGiangVien.Ui;
using Input = AntdUI.Input;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Khung form dùng chung cho nhập lớp/phòng/buổi học: bố cục, bàn phím, báo lỗi và giải phóng tài nguyên.</summary>
public abstract class SchedulingEditorBase : AntdUI.Window
{
    private readonly List<Font> ownedFonts = [];
    private readonly Dictionary<string, (Control Control, AntdUI.Label Error)> fields = new();
    private readonly Motion appearance = new();
    private readonly System.Windows.Forms.Label summary;
    private readonly ToolTip errors = new();
    private readonly AntdUI.Button saveButton;
    // Các form con dùng cùng lưới nhập; cửa sổ hẹp tự chuyển từ hai cột sang một cột.
    protected System.Windows.Forms.Panel Body { get; }
    protected int GridTop => AppTheme.S(102);
    protected int RowHeight => AppTheme.S(80);
    protected int MarginX => AppTheme.S(26);
    protected bool TwoColumns => ClientSize.Width >= AppTheme.S(540);
    protected int ContentWidth => ClientSize.Width - AppTheme.S(72);
    protected int FieldWidth => TwoColumns ? (ContentWidth - AppTheme.S(20)) / 2 : ContentWidth;
    public IReadOnlyDictionary<string, string> FieldErrors { get; private set; } = new Dictionary<string, string>();
    public string ValidationSummary => summary.Text;

    // Dựng tiêu đề, vùng nhập có cuộn và chân form chứa tóm tắt lỗi cùng hai nút Hủy/Lưu.
    protected SchedulingEditorBase(string title, string subtitle, string section, int width, int height, string saveText)
    {
        AutoHandDpi = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = OwnFont();
        BackColor = Color.White;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        // Giới hạn cửa sổ trong vùng làm việc của màn hình đang đặt con trỏ.
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(AppTheme.S(width), workArea.Width - AppTheme.S(32)),
            Math.Min(AppTheme.S(height), workArea.Height - AppTheme.S(32)));
        var header = new PageHeader {
            Text = section, Font = OwnFont(8, true), ForeColor = SchedulingTheme.Muted,
            Dock = DockStyle.Top, Height = AppTheme.S(40), ShowButton = true,
            MaximizeBox = false, MinimizeBox = false, DividerShow = false
        };
        var footer = new System.Windows.Forms.Panel {
            Dock = DockStyle.Bottom, Height = AppTheme.S(104), BackColor = SchedulingTheme.Canvas
        };
        Body = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
        Controls.Add(Body);
        Controls.Add(footer);
        Controls.Add(header);
        AddLabel(title, MarginX, AppTheme.S(8), ContentWidth, AppTheme.S(36), 21, true);
        AddLabel(subtitle, MarginX, AppTheme.S(48), ContentWidth, AppTheme.S(24), 9, color: SchedulingTheme.Muted);
        AddLabel("Các trường có dấu * là bắt buộc", MarginX, AppTheme.S(74), ContentWidth, AppTheme.S(18), 8,
            color: SchedulingTheme.Muted);
        summary = new System.Windows.Forms.Label {
            Font = OwnFont(8), ForeColor = SchedulingTheme.Danger, AutoEllipsis = true,
            Location = new Point(MarginX, AppTheme.S(7)),
            Size = new Size(ClientSize.Width - AppTheme.S(52), AppTheme.S(36)), BackColor = Color.Transparent
        };
        footer.Controls.Add(summary);
        // Nút lưu gọi TrySave của form con; chỉ đóng khi phần kiểm tra và lưu đã thành công.
        var cancel = OwnButton("Hủy");
        saveButton = OwnButton(saveText, "CheckOutlined", true);
        cancel.SetBounds(ClientSize.Width - AppTheme.S(294), AppTheme.S(49), AppTheme.S(102), AppTheme.S(42));
        saveButton.SetBounds(ClientSize.Width - AppTheme.S(180), AppTheme.S(49), AppTheme.S(154), AppTheme.S(42));
        cancel.Click += (_, _) => CancelAndClose();
        saveButton.Click += (_, _) => SaveAndClose();
        footer.Controls.Add(cancel);
        footer.Controls.Add(saveButton);
        Shown += (_, _) => appearance.Run(180, progress => Opacity = progress);
        VisibleChanged += (_, _) => { if (!Visible) appearance.Cancel(); };
    }

    // Đặt độ trong suốt ban đầu theo cấu hình hiệu ứng trước khi form xuất hiện.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Opacity = Config.Animation ? 0 : 1;
    }

    // Tạo font và ghi nhận quyền sở hữu để giải phóng cùng form, tránh rò tài nguyên GDI.
    protected Font OwnFont(float size = 10, bool bold = false)
    {
        var font = AppTheme.Font(size, bold);
        ownedFonts.Add(font);
        return font;
    }

    // Tạo nút theo giao diện chung và đưa font của nút vào danh sách tài nguyên cần hủy.
    protected AntdUI.Button OwnButton(string text, string? icon = null, bool primary = false)
    {
        var button = SchedulingTheme.Button(text, icon, primary);
        ownedFonts.Add(button.Font);
        return button;
    }

    // Tạo nhãn trong vùng nhập với tọa độ và font do form quản lý.
    protected AntdUI.Label AddLabel(string text, int x, int y, int width, int height, float size = 9,
        bool bold = false, Color? color = null)
    {
        var label = SchedulingTheme.Label(text, size, bold, color);
        ownedFonts.Add(label.Font);
        label.SetBounds(x, y, width, height);
        Body.Controls.Add(label);
        return label;
    }

    // Tính đáy lưới sau số trường đã có để form con đặt các phần mở rộng bên dưới.
    protected int GridBottom(int count) => GridTop + ((count + (TwoColumns ? 1 : 0)) / (TwoColumns ? 2 : 1)) * RowHeight;

    // Chuyển thứ tự trường thành hàng/cột rồi giao cho AddField dựng control và nhãn lỗi.
    protected void AddGridField(string key, string caption, Control control, int index)
    {
        int column = TwoColumns ? index % 2 : 0;
        int row = TwoColumns ? index / 2 : index;
        AddField(key, caption, control, MarginX + column * (FieldWidth + AppTheme.S(20)),
            GridTop + row * RowHeight, FieldWidth, index);
    }

    // Gắn tên trường với ô nhập và nhãn lỗi để kết quả kiểm tra tìm đúng vị trí trên form.
    protected void AddField(string key, string caption, Control control, int x, int y, int width, int tabIndex)
    {
        AddLabel(caption, x, y, width, AppTheme.S(20), bold: true);
        control.Font = OwnFont(9);
        control.ForeColor = SchedulingTheme.Ink;
        control.SetBounds(x, y + AppTheme.S(24), width, AppTheme.S(36));
        control.TabIndex = tabIndex;
        if (control is Input input) {
            input.Radius = 10;
            input.WaveSize = 0;
            input.BorderColor = SchedulingTheme.Line;
            input.BorderActive = input.BorderHover = SchedulingTheme.Primary;
        }
        Body.Controls.Add(control);
        var error = AddLabel("", x, y + AppTheme.S(62), width, AppTheme.S(18), 7.5f, color: SchedulingTheme.Danger);
        fields.Add(key, (control, error));
    }

    // Đăng ký trường đặc biệt do form con tự dựng, chẳng hạn danh sách buổi học.
    protected void RegisterErrorField(string key, Control control, AntdUI.Label label) => fields.Add(key, (control, label));
    // Đặt chiều cao cuộn tối thiểu để vẫn đi tới các trường cuối trên màn hình thấp.
    protected void SetContentHeight(int height) => Body.AutoScrollMinSize = new Size(0, height + AppTheme.S(16));

    // Lấy mã tự sinh; nếu hết dải mã, giữ form dùng được và hướng dẫn nhập mã còn trống.
    protected void InitializeNewId(Input input, Func<string> nextId, string instruction)
    {
        try { input.Text = nextId(); }
        catch (InvalidOperationException error) {
            System.Diagnostics.Trace.TraceWarning("Automatic schedule ID unavailable: {0}", error.Message);
            input.Text = "";
            input.PlaceholderText = "Nhập mã chưa sử dụng";
            summary.ForeColor = SchedulingTheme.Muted;
            summary.Text = "Đã hết mã tự động. " + instruction;
        }
    }

    // Xóa lỗi của lần lưu trước trên nhãn, tooltip và trạng thái ô nhập trước khi kiểm tra lại.
    protected void ClearErrors()
    {
        FieldErrors = new Dictionary<string, string>();
        summary.Text = "";
        summary.ForeColor = SchedulingTheme.Danger;
        foreach (var field in fields.Values) {
            field.Error.Text = "";
            errors.SetToolTip(field.Control, "");
            if (field.Control is Input input) input.Status = TType.None;
        }
    }

    // Phân phối lỗi về từng trường, cuộn tới lỗi đầu tiên và trả false cho luồng TrySave.
    protected bool ShowErrors(IReadOnlyDictionary<string, string> validation)
    {
        FieldErrors = new Dictionary<string, string>(validation);
        foreach (var error in validation) {
            if (!fields.TryGetValue(error.Key, out var field)) continue;
            field.Error.Text = error.Value;
            errors.SetToolTip(field.Control, error.Value);
            if (field.Control is Input input) input.Status = TType.Error;
        }
        summary.Text = validation.Values.FirstOrDefault() ?? "Vui lòng kiểm tra các trường được đánh dấu.";
        var first = validation.Keys.Select(key => fields.GetValueOrDefault(key).Control).FirstOrDefault(control => control is not null);
        if (first is not null) { Body.ScrollControlIntoView(first); first.Focus(); }
        return false;
    }

    // Sau khi đăng nhập lại, bỏ lỗi phiên nhưng giữ nguyên dữ liệu nháp để người dùng chủ động lưu.
    internal void SessionRecovered()
    {
        if (!FieldErrors.ContainsKey("_session")) return;
        ClearErrors();
        summary.Text = "Đã đăng nhập lại. Bản nháp chưa được lưu.";
        summary.ForeColor = SchedulingTheme.Muted;
    }

    // Nạp danh mục và cấu hình hộp xổ xuống cho các bộ chọn của form con.
    protected static void ConfigureSelect(AntdUI.Select select, IEnumerable<string> choices, string placeholder)
    {
        select.Items.AddRange(choices.Cast<object>().ToArray());
        select.PlaceholderText = placeholder;
        select.DropDownRadius = 12;
        select.ListAutoWidth = true;
    }

    // Đọc lựa chọn thành chuỗi rỗng nếu chưa chọn để các hàm kiểm tra xử lý thống nhất.
    protected static string Selected(AntdUI.Select select) => select.SelectedValue?.ToString() ?? "";
    // Đổi ô nhập thành số nguyên; nếu sai định dạng thì gắn lỗi với đúng tên trường.
    protected static bool TryInteger(Input input, string key, Dictionary<string, string> validation, out int value)
    {
        if (int.TryParse(input.Text, out value)) return true;
        validation[key] = "Nhập số nguyên hợp lệ.";
        return false;
    }

    // Escape hủy form, Enter lưu; các control đang xử lý xổ xuống hoặc chọn ngày giữ hành vi riêng.
    protected override bool ProcessCmdKey(ref System.Windows.Forms.Message message, Keys keyData)
    {
        if (keyData == Keys.Escape) {
            if (FocusedInput() is AntdUI.Select { ExpandDrop: true } or DatePicker { ExpandDrop: true })
                return base.ProcessCmdKey(ref message, keyData);
            CancelAndClose();
            return true;
        }
        if (keyData == Keys.Enter && FocusedInput() is not (AntdUI.Select or DatePicker or ComboBox or DateTimePicker or AntdUI.Button)) {
            SaveAndClose();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    // Tìm sâu trong cây control để biết ô nhập thực sự đang giữ focus, kể cả control có lớp bọc.
    private Control? FocusedInput()
    {
        Control? Find(Control parent) {
            foreach (Control child in parent.Controls) {
                if (!child.ContainsFocus) continue;
                if (child is AntdUI.Select or DatePicker or ComboBox or DateTimePicker or AntdUI.Button) return child;
                return Find(child) ?? child;
            }
            return null;
        }
        return Find(this);
    }

    // Trả OK về trang cha chỉ sau khi form con xác nhận lưu thành công.
    private void SaveAndClose()
    {
        if (!TrySave()) return;
        DialogResult = DialogResult.OK;
        Close();
    }
    // Trả Cancel để trang cha biết không cần cập nhật dữ liệu từ bản nháp.
    private void CancelAndClose() { DialogResult = DialogResult.Cancel; Close(); }
    // Mỗi form con tự kiểm tra và lưu loại dữ liệu của mình qua phương thức này.
    public abstract bool TrySave();
    // Danh sách chỉ đọc dành cho kiểm chứng quyền sở hữu font của form.
    internal IReadOnlyList<Font> OwnedFontResourcesForCheck => ownedFonts.ToArray();

    // Dừng hiệu ứng và tooltip trước khi hủy control; sau đó hủy từng font duy nhất đã sở hữu.
    protected override void Dispose(bool disposing)
    {
        if (disposing) { appearance.Dispose(); errors.Dispose(); }
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var font in ownedFonts.Distinct<Font>(ReferenceEqualityComparer.Instance)) font.Dispose();
        ownedFonts.Clear();
    }
}
