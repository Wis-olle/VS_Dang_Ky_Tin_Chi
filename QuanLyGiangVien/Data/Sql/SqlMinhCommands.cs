using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data.Sql;

public static class SqlMinhCommands
{
    public static SqlCommand Load() => Procedure("DocDuLieu");

    public static SqlCommand SaveLecturer(Lecturer value, bool isNew)
    {
        var command = Procedure("LuuGiangVien");
        Add(command, "ma_gv", SqlDbType.VarChar, value.Id, 20);
        Add(command, "ho_ten", SqlDbType.NVarChar, value.Name, 100);
        Add(command, "ngay_sinh", SqlDbType.Date, value.BirthDate.Date);
        Add(command, "gioi_tinh", SqlDbType.NVarChar, value.Gender, 10);
        Add(command, "email", SqlDbType.VarChar, value.Email, 254);
        Add(command, "sdt", SqlDbType.VarChar, value.Phone, 20);
        Add(command, "ten_khoa", SqlDbType.NVarChar, value.Faculty, 100);
        Add(command, "hoc_vi", SqlDbType.NVarChar, value.Degree, 80);
        Add(command, "avatar_key", SqlDbType.VarChar, value.AvatarKey, 40);
        Add(command, "trang_thai", SqlDbType.Bit, value.Status == "Đang giảng dạy");
        Add(command, "la_moi", SqlDbType.Bit, isNew);
        Version(command, value.Version);
        return command;
    }

    public static SqlCommand SaveRoom(Room value, bool isNew)
    {
        var command = Procedure("LuuPhong");
        Add(command, "ma_phong", SqlDbType.VarChar, value.Id, 20);
        Add(command, "toa_nha", SqlDbType.NVarChar, value.Building, 80);
        Add(command, "suc_chua", SqlDbType.Int, value.Capacity);
        Add(command, "loai_phong", SqlDbType.NVarChar, value.Type, 50);
        Add(command, "trang_thai", SqlDbType.Bit, value.Status == SchedulingLabels.Ready);
        Add(command, "la_moi", SqlDbType.Bit, isNew);
        Version(command, value.Version);
        return command;
    }

    public static SqlCommand SaveClass(CourseClass value, bool isNew)
    {
        var command = Procedure("LuuLop");
        Add(command, "ma_lhp", SqlDbType.VarChar, value.Id, 20);
        Add(command, "ma_hp", SqlDbType.VarChar, value.CourseId, 20);
        Add(command, "ma_gv", SqlDbType.VarChar, value.LecturerId, 20);
        Add(command, "ma_hk", SqlDbType.VarChar, value.SemesterId, 20);
        Add(command, "si_so_max", SqlDbType.Int, value.Capacity);
        var status = value.Status switch { SchedulingLabels.Open => "DANG_MO", SchedulingLabels.Closed => "DONG",
            SchedulingLabels.Canceled => "HUY", _ => throw new ArgumentException("Trạng thái lớp không hợp lệ.", nameof(value)) };
        Add(command, "trang_thai", SqlDbType.VarChar, status, 12);
        var meetings = value.Meetings.Select(m => new { m.RoomId,
            Thu = m.Day == DayOfWeek.Sunday ? 8 : (int)m.Day + 1,
            m.StartPeriod, m.EndPeriod, m.StartDate, m.EndDate });
        Add(command, "lich_json", SqlDbType.NVarChar, JsonSerializer.Serialize(meetings), -1);
        Add(command, "la_moi", SqlDbType.Bit, isNew);
        Version(command, value.Version);
        return command;
    }

    public static SqlCommand DeleteLecturer(string id, string version = "") => Delete("XoaGiangVien", "ma_gv", id, version);
    public static SqlCommand DeleteRoom(string id, string version = "") => Delete("XoaPhong", "ma_phong", id, version);
    public static SqlCommand DeleteClass(string id, string version = "") => Delete("XoaLop", "ma_lhp", id, version);

    private static SqlCommand Delete(string procedure, string parameter, string id, string version)
    {
        var command = Procedure(procedure);
        Add(command, parameter, SqlDbType.VarChar, id, 20);
        Version(command, version);
        return command;
    }
    private static void Version(SqlCommand command, string version)
    {
        if (version.Length != 0 && (version.Length != 16 || !version.All(Uri.IsHexDigit)))
            throw new PersistenceException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại.");
        Add(command, "phien_ban", SqlDbType.Binary, version.Length == 0 ? DBNull.Value : Convert.FromHexString(version), 8);
    }
    private static SqlCommand Procedure(string name) => new("dbo.usp_Minh_" + name) {
        CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
    private static void Add(SqlCommand command, string name, SqlDbType type, object value, int size = 0)
    {
        if (value is string text && (size > 0 && text.Length > size
            || type == SqlDbType.VarChar && text.Any(character => character > 127)))
            throw new PersistenceException("Dữ liệu vượt giới hạn trường SQL hoặc chứa ký tự không phù hợp. Vui lòng kiểm tra và thử lại.");
        command.Parameters.Add(new SqlParameter("@" + name, type, size) { Value = value });
    }
}
