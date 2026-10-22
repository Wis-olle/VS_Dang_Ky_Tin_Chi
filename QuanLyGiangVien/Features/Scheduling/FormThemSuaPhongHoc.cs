using AntdUI;
using QuanLyGiangVien.Domain.Scheduling;
using QuanLyGiangVien.Ui;
using Input = AntdUI.Input;

namespace QuanLyGiangVien.Features.Scheduling;

public sealed class RoomEditor : SchedulingEditorBase
{
    private readonly ScheduleStore store;
    private readonly Room? original;
    private readonly Input id = new(), building = new(), capacity = new();
    private readonly Select type = new(), status = new();
    public Room? Result { get; private set; }

    // Mở form thêm hoặc sửa phòng học
    public RoomEditor(ScheduleStore store, Room? original = null)
        : base(original is null ? "Thêm phòng học" : "Chỉnh sửa phòng học",
            "Thông tin phòng dùng chung cho các lớp học phần.", "PHÒNG HỌC", 600, 520,
            original is null ? "Thêm phòng" : "Lưu thay đổi")
    {
        this.store = store;
        this.original = original;
        AddGridField(nameof(Room.Id), "Mã phòng *", id, 0);
        AddGridField(nameof(Room.Building), "Tòa nhà *", building, 1);
        AddGridField(nameof(Room.Capacity), "Sức chứa *", capacity, 2);
        AddGridField(nameof(Room.Type), "Loại phòng *", type, 3);
        AddGridField(nameof(Room.Status), "Trạng thái *", status, 4);
        ConfigureSelect(type, SchedulingLabels.RoomTypes, "Chọn loại phòng");
        ConfigureSelect(status, SchedulingLabels.RoomStatuses, "Chọn trạng thái");
        id.ReadOnly = original is not null;
        id.BackColor = original is null ? Color.White : SchedulingTheme.Canvas;
        building.PlaceholderText = "Ví dụ: Tòa A";
        capacity.PlaceholderText = "Ví dụ: 60";
        var noteTop = GridBottom(5);
        var note = AddLabel("Phòng đang có lịch học sẽ được kiểm tra trước khi đổi sức chứa hoặc trạng thái.",
            MarginX, noteTop, ContentWidth, AppTheme.S(38), 8, color: SchedulingTheme.Muted);
        note.TextMultiLine = true;
        SetContentHeight(noteTop + AppTheme.S(42));
        if (original is not null) FillForCheck(original);
        else {
            InitializeNewId(id, store.NextRoomId, "Nhập mã phòng chưa dùng, gồm 1–20 chữ cái ASCII, chữ số, gạch ngang hoặc gạch dưới.");
            type.SelectedIndex = 0;
            status.SelectedIndex = 0;
        }
        Shown += (_, _) => { if (original is null) id.Focus(); else building.Focus(); };
    }

    // Điền thông tin phòng học
    public void FillForCheck(Room value)
    {
        id.Text = value.Id;
        building.Text = value.Building;
        capacity.Text = value.Capacity.ToString();
        SelectKnown(type, value.Type);
        SelectKnown(status, value.Status);
    }

    // Lưu phòng học
    public override bool TrySave()
    {
        ClearErrors();
        var parseErrors = new Dictionary<string, string>();
        if (!TryInteger(capacity, nameof(Room.Capacity), parseErrors, out int seats)) return ShowErrors(parseErrors);
        var value = new Room(id.Text, building.Text, seats, Selected(type), Selected(status), original?.Version ?? "");
        if (!store.TrySaveRoom(value, original is null, out var validation)) return ShowErrors(validation);
        Result = store.Rooms.First(room => room.Id.Equals(value.Id.Trim(), StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static void SelectKnown(Select select, string value)
    {
        if (!select.Items.Cast<object>().Any(item => item.ToString() == value)) select.Items.Add(value);
        select.SelectedValue = value;
    }
}
