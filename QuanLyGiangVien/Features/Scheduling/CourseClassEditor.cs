using System.Collections.Immutable;
using AntdUI;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using Input = AntdUI.Input;
using MeetingList = System.Windows.Forms.ListBox;

namespace QuanLyGiangVien.Features.Scheduling;

public sealed class CourseClassEditor : SchedulingEditorBase
{
    private readonly ScheduleStore store;
    private readonly CourseClass? original;
    private readonly Input id = new(), capacity = new(), enrolled = new();
    private readonly Select course = new(), semester = new(), lecturer = new(), status = new();
    private readonly Dictionary<string, string> courseIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> semesterIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> lecturerIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly MeetingList meetingList;
    private readonly AntdUI.Label semesterInfo;
    private readonly AntdUI.Label emptyMeetings;
    private readonly AntdUI.Button editMeeting, removeMeeting;
    private ImmutableArray<ClassMeeting> meetings = [];
    public CourseClass? Result { get; private set; }
    public IReadOnlyList<ClassMeeting> Meetings => meetings;

    public CourseClassEditor(ScheduleStore store, CourseClass? original = null)
        : base(original is null ? "Mở lớp học phần" : "Chỉnh sửa lớp học phần",
            original is { HasRegistrationHistory: true } or { Enrolled: > 0 }
                ? "Có lịch sử đăng ký: giữ học phần, kỳ, giảng viên và lịch; tạo lớp mới nếu cần."
                : "Chọn học phần, phân công giảng viên và bố trí các buổi học.", "LỚP HỌC PHẦN", 680, 700,
            original is null ? "Mở lớp" : "Lưu thay đổi")
    {
        this.store = store;
        this.original = original;
        AddGridField(nameof(CourseClass.Id), "Mã lớp học phần *", id, 0);
        AddGridField(nameof(CourseClass.SemesterId), "Học kỳ *", semester, 1);
        AddGridField(nameof(CourseClass.CourseId), "Học phần *", course, 2);
        AddGridField(nameof(CourseClass.LecturerId), "Giảng viên *", lecturer, 3);
        AddGridField(nameof(CourseClass.Capacity), "Sĩ số tối đa *", capacity, 4);
        AddGridField(nameof(CourseClass.Enrolled), "Đã đăng ký", enrolled, 5);
        AddGridField(nameof(CourseClass.Status), "Trạng thái *", status, 6);
        id.ReadOnly = original is not null;
        id.BackColor = original is null ? Color.White : SchedulingTheme.Canvas;
        enrolled.ReadOnly = true;
        enrolled.TabStop = false;
        enrolled.BackColor = SchedulingTheme.Canvas;
        capacity.PlaceholderText = "Ví dụ: 40";
        ConfigureReferences();
        course.Enabled = semester.Enabled = original is not { HasRegistrationHistory: true } and not { Enrolled: > 0 };
        lecturer.Enabled = original is not { Enrolled: > 0 } and not { HasRegistrationHistory: true };
        ConfigureSelect(status, SchedulingLabels.ClassStatuses, "Chọn trạng thái");
        int top = GridBottom(7);
        semesterInfo = AddLabel("", MarginX, top, ContentWidth, AppTheme.S(24), 8, color: SchedulingTheme.Muted);
        AddLabel("Buổi học hằng tuần", MarginX, top + AppTheme.S(32), ContentWidth, AppTheme.S(22), 11, true);
        meetingList = new MeetingList {
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = AppTheme.S(50), IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = SchedulingTheme.Ink,
            Font = OwnFont(9), TabIndex = 7, HorizontalScrollbar = false,
            Location = new Point(MarginX, top + AppTheme.S(62)), Size = new Size(ContentWidth, AppTheme.S(154))
        };
        Body.Controls.Add(meetingList);
        emptyMeetings = AddLabel("Chưa xếp lịch. Bạn có thể lưu lớp và thêm buổi học sau.",
            MarginX + AppTheme.S(14), top + AppTheme.S(94), ContentWidth - AppTheme.S(28), AppTheme.S(42),
            9, color: SchedulingTheme.Muted);
        emptyMeetings.TextMultiLine = true;
        emptyMeetings.Click += (_, _) => AddMeeting();
        var detailFont = OwnFont(8);
        var titleFont = OwnFont(9, true);
        meetingList.DrawItem += (_, args) => DrawMeeting(args, titleFont, detailFont);
        var add = OwnButton("Thêm buổi", "PlusOutlined");
        editMeeting = OwnButton("Sửa", "EditOutlined");
        removeMeeting = OwnButton("Bỏ buổi", "DeleteOutlined");
        int buttonTop = top + AppTheme.S(226);
        add.SetBounds(MarginX, buttonTop, AppTheme.S(128), AppTheme.S(36));
        editMeeting.SetBounds(MarginX + AppTheme.S(140), buttonTop, AppTheme.S(94), AppTheme.S(36));
        removeMeeting.SetBounds(MarginX + AppTheme.S(246), buttonTop, AppTheme.S(112), AppTheme.S(36));
        add.TabIndex = 8;
        add.Enabled = original is not { Enrolled: > 0 } and not { HasRegistrationHistory: true };
        editMeeting.TabIndex = 9;
        removeMeeting.TabIndex = 10;
        add.Click += (_, _) => AddMeeting();
        editMeeting.Click += (_, _) => EditMeeting();
        removeMeeting.Click += (_, _) => RemoveMeeting();
        Body.Controls.Add(add);
        Body.Controls.Add(editMeeting);
        Body.Controls.Add(removeMeeting);
        var meetingError = AddLabel("", MarginX, buttonTop + AppTheme.S(39), ContentWidth, AppTheme.S(36),
            8, color: SchedulingTheme.Danger);
        meetingError.TextMultiLine = true;
        RegisterErrorField(nameof(CourseClass.Meetings), meetingList, meetingError);
        SetContentHeight(buttonTop + AppTheme.S(81));
        meetingList.SelectedIndexChanged += (_, _) => UpdateMeetingActions();
        meetingList.DoubleClick += (_, _) => EditMeeting();
        semester.SelectedIndexChanged += (_, _) => UpdateSemesterInfo();
        if (original is not null) FillForCheck(original);
        else {
            InitializeNewId(id, store.NextClassId, "Nhập mã lớp còn trống, gồm 1–20 chữ cái ASCII, chữ số, gạch ngang hoặc gạch dưới.");
            enrolled.Text = "0";
            capacity.Text = "40";
            status.SelectedIndex = 0;
            if (semester.Items.Count > 0) semester.SelectedIndex = 0;
            RefreshMeetings();
        }
        UpdateSemesterInfo();
        Shown += (_, _) => { if (original is null) id.Focus(); else capacity.Focus(); };
    }

    private void ConfigureReferences()
    {
        foreach (var item in store.Catalog.Courses) courseIds[$"{item.Id} · {item.Name}"] = item.Id;
        foreach (var item in store.Catalog.Semesters) semesterIds[$"{item.Name} · {item.Id}"] = item.Id;
        foreach (var item in store.Catalog.Lecturers)
            lecturerIds[$"{item.Id} · {item.Name}{(item.Status != "Đang giảng dạy" ? " · " + item.Status : "")}"] = item.Id;
        ConfigureSelect(course, courseIds.Keys, "Chọn học phần");
        ConfigureSelect(semester, semesterIds.Keys, "Chọn học kỳ");
        ConfigureSelect(lecturer, lecturerIds.Keys, "Chọn giảng viên");
    }

    public void FillForCheck(CourseClass value)
    {
        id.Text = value.Id;
        ChooseReference(course, courseIds, value.CourseId);
        ChooseReference(semester, semesterIds, value.SemesterId);
        ChooseReference(lecturer, lecturerIds, value.LecturerId);
        capacity.Text = value.Capacity.ToString();
        enrolled.Text = (original?.Enrolled ?? 0).ToString();
        if (!SchedulingLabels.ClassStatuses.Contains(value.Status)) status.Items.Add(value.Status);
        status.SelectedValue = value.Status;
        meetings = value.Meetings.IsDefault ? [] : value.Meetings;
        RefreshMeetings();
        UpdateSemesterInfo();
    }

    private static void ChooseReference(Select select, Dictionary<string, string> choices, string id)
    {
        string? text = choices.FirstOrDefault(item => item.Value.Equals(id, StringComparison.OrdinalIgnoreCase)).Key;
        if (text is null) {
            text = $"{id} · Không còn trong danh sách";
            choices[text] = id;
            select.Items.Add(text);
        }
        select.SelectedValue = text;
    }

    private Semester? CurrentSemester() => store.Catalog.Semesters.FirstOrDefault(item =>
        item.Id.Equals(semesterIds.GetValueOrDefault(Selected(semester)), StringComparison.OrdinalIgnoreCase));

    private void UpdateSemesterInfo()
    {
        if (semesterInfo is null) return;
        var selected = CurrentSemester();
        semesterInfo.Text = selected is null ? "Chọn học kỳ để thêm lịch học." :
            $"Thời gian học kỳ: {selected.StartDate:dd/MM/yyyy} – {selected.EndDate:dd/MM/yyyy}";
    }

    public override bool TrySave()
    {
        ClearErrors();
        var parseErrors = new Dictionary<string, string>();
        if (!TryInteger(capacity, nameof(CourseClass.Capacity), parseErrors, out int seats)) return ShowErrors(parseErrors);
        var value = new CourseClass(id.Text, courseIds.GetValueOrDefault(Selected(course)) ?? "",
            semesterIds.GetValueOrDefault(Selected(semester)) ?? "", lecturerIds.GetValueOrDefault(Selected(lecturer)) ?? "",
            seats, original?.Enrolled ?? 0, Selected(status), meetings, original?.Version ?? "", original?.HasRegistrationHistory ?? false,
            original?.Lifecycle ?? "REGISTRATION", original?.ActualMeetings ?? default);
        if (!store.TrySaveClass(value, original is null, out var validation)) return ShowErrors(validation);
        Result = store.Classes.First(item => item.Id.Equals(value.Id.Trim(), StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private bool CanEditMeetings(out Semester? selected, out int seats)
    {
        ClearErrors();
        var validation = new Dictionary<string, string>();
        selected = CurrentSemester();
        if (original is { Enrolled: > 0 } or { HasRegistrationHistory: true }) validation[nameof(CourseClass.Meetings)] = "Lớp có lịch sử đăng ký; không thể đổi buổi học.";
        if (selected is null) validation[nameof(CourseClass.SemesterId)] = "Chọn học kỳ trước khi thêm buổi học.";
        TryInteger(capacity, nameof(CourseClass.Capacity), validation, out seats);
        if (seats is < 1 or > 1000) validation[nameof(CourseClass.Capacity)] = "Sĩ số tối đa nằm trong khoảng 1–1000.";
        return validation.Count == 0 || ShowErrors(validation);
    }

    private void AddMeeting()
    {
        if (!CanEditMeetings(out var selected, out int seats)) return;
        using var editor = new MeetingEditor(store, selected!, seats);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is null) return;
        meetings = meetings.Add(editor.Result);
        RefreshMeetings(meetings.Length - 1);
    }

    private void EditMeeting()
    {
        int index = meetingList.SelectedIndex;
        if (index < 0 || !CanEditMeetings(out var selected, out int seats)) return;
        using var editor = new MeetingEditor(store, selected!, seats, meetings[index]);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Result is null) return;
        meetings = meetings.SetItem(index, editor.Result);
        RefreshMeetings(index);
    }

    private void RemoveMeeting()
    {
        int index = meetingList.SelectedIndex;
        if (index < 0 || original is { Enrolled: > 0 } or { HasRegistrationHistory: true }) return;
        meetings = meetings.RemoveAt(index);
        ClearErrors();
        RefreshMeetings(Math.Min(index, meetings.Length - 1));
    }

    private void RefreshMeetings(int selectedIndex = -1)
    {
        meetingList.BeginUpdate();
        meetingList.Items.Clear();
        meetingList.Items.AddRange(meetings.Cast<object>().ToArray());
        meetingList.SelectedIndex = selectedIndex;
        meetingList.EndUpdate();
        emptyMeetings.Visible = meetings.Length == 0;
        UpdateMeetingActions();
    }

    private void UpdateMeetingActions()
    {
        editMeeting.Enabled = removeMeeting.Enabled = meetingList.SelectedIndex >= 0
            && original is not { Enrolled: > 0 } and not { HasRegistrationHistory: true };
    }

    private void DrawMeeting(System.Windows.Forms.DrawItemEventArgs args, Font titleFont, Font detailFont)
    {
        args.DrawBackground();
        if (args.Index < 0 || args.Index >= meetings.Length) return;
        var value = meetings[args.Index];
        bool selected = args.State.HasFlag(DrawItemState.Selected);
        using var fill = new SolidBrush(selected ? SchedulingTheme.Canvas : Color.White);
        args.Graphics.FillRectangle(fill, args.Bounds);
        string dayText = value.Day == DayOfWeek.Sunday ? "Chủ nhật" : $"Thứ {(int)value.Day + 1}";
        var titleBounds = new Rectangle(args.Bounds.X + AppTheme.S(12), args.Bounds.Y + AppTheme.S(6),
            args.Bounds.Width - AppTheme.S(24), AppTheme.S(20));
        var detailBounds = new Rectangle(titleBounds.X, args.Bounds.Y + AppTheme.S(28), titleBounds.Width, AppTheme.S(18));
        TextRenderer.DrawText(args.Graphics, $"{dayText}  ·  Phòng {value.RoomId}  ·  Tiết {value.StartPeriod}–{value.EndPeriod}",
            titleFont, titleBounds, SchedulingTheme.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(args.Graphics, $"{value.StartDate:dd/MM/yyyy} – {value.EndDate:dd/MM/yyyy}",
            detailFont, detailBounds, SchedulingTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        args.DrawFocusRectangle();
    }

    protected override bool ProcessCmdKey(ref System.Windows.Forms.Message message, Keys keyData)
    {
        if (meetingList is not null && meetingList.ContainsFocus) {
            if (keyData == Keys.Enter) { EditMeeting(); return true; }
            if (keyData == Keys.Delete) { RemoveMeeting(); return true; }
        }
        return base.ProcessCmdKey(ref message, keyData);
    }
}
