using AntdUI;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using Input = AntdUI.Input;

namespace QuanLyGiangVien.Features.Scheduling;

/// <summary>Edits one weekly meeting; saving the parent class performs conflict validation.</summary>
public sealed class MeetingEditor : SchedulingEditorBase
{
    private readonly ScheduleStore store;
    private readonly Semester semester;
    private readonly int capacity;
    private readonly Select room = new(), day = new();
    private readonly Input startPeriod = new(), endPeriod = new();
    private readonly DatePicker start = new(), end = new();
    private readonly Dictionary<string, string> roomIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly (DayOfWeek Day, string Text)[] Days = [
        (DayOfWeek.Monday, "Thứ 2"), (DayOfWeek.Tuesday, "Thứ 3"), (DayOfWeek.Wednesday, "Thứ 4"),
        (DayOfWeek.Thursday, "Thứ 5"), (DayOfWeek.Friday, "Thứ 6"), (DayOfWeek.Saturday, "Thứ 7"),
        (DayOfWeek.Sunday, "Chủ nhật")
    ];
    public ClassMeeting? Result { get; private set; }

    // Mở form thêm hoặc sửa buổi học
    public MeetingEditor(ScheduleStore store, Semester semester, int capacity, ClassMeeting? original = null)
        : base(original is null ? "Thêm buổi học" : "Chỉnh sửa buổi học",
            "Chọn phòng, tiết học và khoảng ngày của buổi học hằng tuần.", "LỊCH HỌC", 610, 570,
            original is null ? "Thêm buổi học" : "Lưu buổi học")
    {
        this.store = store;
        this.semester = semester;
        this.capacity = capacity;
        AddGridField(nameof(ClassMeeting.RoomId), "Phòng học *", room, 0);
        AddGridField(nameof(ClassMeeting.Day), "Ngày trong tuần *", day, 1);
        AddGridField(nameof(ClassMeeting.StartPeriod), "Tiết bắt đầu *", startPeriod, 2);
        AddGridField(nameof(ClassMeeting.EndPeriod), "Tiết kết thúc *", endPeriod, 3);
        AddGridField(nameof(ClassMeeting.StartDate), "Từ ngày *", start, 4);
        AddGridField(nameof(ClassMeeting.EndDate), "Đến ngày *", end, 5);
        ConfigureSelect(day, Days.Select(item => item.Text), "Chọn ngày học");
        PopulateRooms(original?.RoomId);
        start.Format = end.Format = "dd/MM/yyyy";
        start.PlaceholderText = end.PlaceholderText = "dd/mm/yyyy";
        startPeriod.PlaceholderText = endPeriod.PlaceholderText = "Từ 1 đến 12";
        int noteTop = GridBottom(6);
        var note = AddLabel($"{semester.Name}: {semester.StartDate:dd/MM/yyyy} – {semester.EndDate:dd/MM/yyyy}. " +
            $"Danh sách phòng gồm phòng sẵn sàng, đủ {capacity} chỗ. Khi lưu lớp sẽ kiểm tra lịch phòng và giảng viên.",
            MarginX, noteTop, ContentWidth, AppTheme.S(58), 8, color: SchedulingTheme.Muted);
        note.TextMultiLine = true;
        SetContentHeight(noteTop + AppTheme.S(62));
        if (original is not null) FillForCheck(original);
        else {
            day.SelectedIndex = 0;
            startPeriod.Text = "1";
            endPeriod.Text = "3";
            start.Value = semester.StartDate.ToDateTime(TimeOnly.MinValue);
            end.Value = semester.EndDate.ToDateTime(TimeOnly.MinValue);
        }
        Shown += (_, _) => room.Focus();
    }

    // Nạp danh sách phòng phù hợp
    private void PopulateRooms(string? originalId)
    {
        var choices = store.Rooms.Where(item => item.Status == SchedulingLabels.Ready && item.Capacity >= capacity)
            .OrderBy(item => item.Id).Select(item => RoomText(item)).ToList();
        foreach (var available in store.Rooms.Where(item => item.Status == SchedulingLabels.Ready && item.Capacity >= capacity))
            roomIds[RoomText(available)] = available.Id;
        if (originalId is not null) PreserveRoom(originalId, choices);
        ConfigureSelect(room, choices, choices.Count == 0 ? "Chưa có phòng đủ sức chứa" : "Chọn phòng học");
    }

    private static string RoomText(Room value) => $"{value.Id} · {value.Capacity} chỗ · {value.Type}";
    private void PreserveRoom(string id, List<string>? choices = null)
    {
        if (roomIds.Values.Any(value => value.Equals(id, StringComparison.OrdinalIgnoreCase))) return;
        var existing = store.Rooms.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        string text = existing is null ? $"{id} · Không còn trong danh sách" :
            $"{RoomText(existing)} · {(existing.Status != SchedulingLabels.Ready ? existing.Status : "Không đủ chỗ")}";
        roomIds[text] = id;
        if (choices is null) room.Items.Add(text); else choices.Add(text);
    }

    // Điền thông tin buổi học
    public void FillForCheck(ClassMeeting value)
    {
        PreserveRoom(value.RoomId);
        room.SelectedValue = roomIds.First(item => item.Value.Equals(value.RoomId, StringComparison.OrdinalIgnoreCase)).Key;
        day.SelectedValue = Days.FirstOrDefault(item => item.Day == value.Day).Text;
        startPeriod.Text = value.StartPeriod.ToString();
        endPeriod.Text = value.EndPeriod.ToString();
        start.Value = value.StartDate == default ? null : value.StartDate.ToDateTime(TimeOnly.MinValue);
        end.Value = value.EndDate == default ? null : value.EndDate.ToDateTime(TimeOnly.MinValue);
    }

    // Kiểm tra và nhận thông tin buổi học
    public override bool TrySave()
    {
        ClearErrors();
        var validation = new Dictionary<string, string>();
        TryInteger(startPeriod, nameof(ClassMeeting.StartPeriod), validation, out int first);
        TryInteger(endPeriod, nameof(ClassMeeting.EndPeriod), validation, out int last);
        var selectedRoom = store.Rooms.FirstOrDefault(item => item.Id.Equals(roomIds.GetValueOrDefault(Selected(room)), StringComparison.OrdinalIgnoreCase));
        if (selectedRoom is null) validation[nameof(ClassMeeting.RoomId)] = "Chọn một phòng học trong danh sách.";
        else if (selectedRoom.Status != SchedulingLabels.Ready) validation[nameof(ClassMeeting.RoomId)] = "Phòng đang bảo trì, hãy chọn phòng khác.";
        else if (selectedRoom.Capacity < capacity) validation[nameof(ClassMeeting.RoomId)] = $"Phòng cần có ít nhất {capacity} chỗ.";
        if (!Days.Any(item => item.Text == Selected(day))) validation[nameof(ClassMeeting.Day)] = "Chọn ngày học trong tuần.";
        if (!validation.ContainsKey(nameof(ClassMeeting.StartPeriod)) && first is < 1 or > 12)
            validation[nameof(ClassMeeting.StartPeriod)] = "Tiết học nằm trong khoảng 1–12.";
        if (!validation.ContainsKey(nameof(ClassMeeting.EndPeriod)) && last is < 1 or > 12)
            validation[nameof(ClassMeeting.EndPeriod)] = "Tiết học nằm trong khoảng 1–12.";
        if (last < first) validation[nameof(ClassMeeting.EndPeriod)] = "Tiết kết thúc phải từ tiết bắt đầu trở đi.";
        ValidateDates(validation);
        if (validation.Count > 0) return ShowErrors(validation);
        Result = new ClassMeeting(selectedRoom!.Id, Days.First(item => item.Text == Selected(day)).Day,
            first, last, DateOnly.FromDateTime(start.Value!.Value), DateOnly.FromDateTime(end.Value!.Value));
        return true;
    }

    // Kiểm tra ngày học
    private void ValidateDates(Dictionary<string, string> validation)
    {
        if (start.Value is null) validation[nameof(ClassMeeting.StartDate)] = "Chọn ngày bắt đầu.";
        else {
            var date = DateOnly.FromDateTime(start.Value.Value);
            if (date < semester.StartDate || date > semester.EndDate)
                validation[nameof(ClassMeeting.StartDate)] = "Ngày phải nằm trong học kỳ đã chọn.";
        }
        if (end.Value is null) validation[nameof(ClassMeeting.EndDate)] = "Chọn ngày kết thúc.";
        else {
            var date = DateOnly.FromDateTime(end.Value.Value);
            if (date < semester.StartDate || date > semester.EndDate)
                validation[nameof(ClassMeeting.EndDate)] = "Ngày phải nằm trong học kỳ đã chọn.";
        }
        if (start.Value is not null && end.Value is not null && end.Value < start.Value)
            validation[nameof(ClassMeeting.EndDate)] = "Ngày kết thúc phải từ ngày bắt đầu trở đi.";
    }
}
