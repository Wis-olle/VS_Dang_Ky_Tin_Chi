using AntdUI;
using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Ui;

public sealed class LecturerRow
{
    public Lecturer Model { get; }
    public CellImage Avatar { get; }
    public CellText Name { get; }
    public string Id => Model.Id;
    public string Faculty => Model.Faculty;
    public string Email => Model.Email;
    public string Phone => Model.Phone;
    public CellTag Status { get; }
    public CellButton[] Actions { get; }
    public LecturerRow(Lecturer item)
    {
        Model = item;
        Avatar = new(Portraits.Get(item.AvatarKey)) { Size = new Size(36, 36), Round = true };
        Name = new(item.Name, AppTheme.Ink) { Font = AppTheme.Font(10, true) };
        Status = new(item.Status) { Back = item.Status == "Tạm nghỉ" ? AppTheme.Peach : AppTheme.Mint, Fore = item.Status == "Tạm nghỉ" ? AppTheme.Ink : AppTheme.Green, BorderWidth = 0 };
        Actions = [Action("view", "Xem hồ sơ", "EyeOutlined"), Action("edit", "Sửa", "EditOutlined"), Action("delete", "Xóa", "DeleteOutlined")];
    }
    private static CellButton Action(string id, string label, string icon) => new(id, TTypeMini.Default)
    {
        Tooltip = label, IconSvg = icon, Fore = id == "delete" ? AppTheme.Danger : AppTheme.Ink,
        Radius = 8, BorderWidth = 1, DefaultBack = Color.White, DefaultBorderColor = AppTheme.Line,
        Gap = 14, IconRatio = .85f
    };
}
