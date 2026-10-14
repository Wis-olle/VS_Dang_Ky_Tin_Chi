using AntdUI;

namespace QuanLyGiangVien.Ui;

public sealed class VietLocalization : ILocalization
{
    private static readonly IReadOnlyDictionary<string, string> strings = new Dictionary<string, string>
    {
        ["ID"] = "en-US", ["YearFormat"] = "yyyy", ["MonthFormat"] = "'Tháng' M",
        ["Mon"] = "T2", ["Tue"] = "T3", ["Wed"] = "T4", ["Thu"] = "T5",
        ["Fri"] = "T6", ["Sat"] = "T7", ["Sun"] = "CN", ["ToDay"] = "Hôm nay",
        ["Now"] = "Bây giờ", ["OK"] = "Đồng ý", ["Cancel"] = "Hủy",
        ["NoData"] = "Không có dữ liệu", ["ItemsPerPage"] = "/ trang",
        ["Filter"] = "Lọc", ["Filter.Search"] = "Tìm kiếm"
    };
    public string? GetLocalizedString(string key) => strings.GetValueOrDefault(key);
}
