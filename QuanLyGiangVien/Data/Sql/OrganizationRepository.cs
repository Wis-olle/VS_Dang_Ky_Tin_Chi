using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Catalogs;

namespace QuanLyGiangVien.Data.Sql;

public sealed class OrganizationRepository(string connectionString)
{
    private static bool ValidCode(string? code) => code is not null && Regex.IsMatch(code, "^[A-Za-z0-9_-]{1,20}$");
    private static void ValidateKind(OrganizationKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Loại danh mục không hợp lệ.");
    }
    public static void Validate(OrganizationRecord item)
    {
        ValidateKind(item.Kind);
        if (!ValidCode(item.Code) || string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 100 || item.Description?.Length > 500)
            throw new ArgumentException("Mã gồm 1–20 chữ, số, gạch dưới hoặc gạch ngang; tên tối đa 100 và mô tả tối đa 500 ký tự.");
        if (item.Kind == OrganizationKind.Faculty ? item.ParentCode is not null : !ValidCode(item.ParentCode))
            throw new ArgumentException("Chọn khoa cho ngành hoặc ngành cho lớp; khoa không có cấp trên.");
        if (item.Kind != OrganizationKind.Class && (item.Cohort is not null || item.AcademicYears is not null)
            || item.Kind == OrganizationKind.Class && item.Description is not null)
            throw new ArgumentException("Khóa và niên khóa chỉ dành cho lớp; mô tả chỉ dành cho khoa hoặc ngành.");
        if (item.Cohort is < 1900 or > 2200) throw new ArgumentException("Khóa tuyển sinh phải từ 1900 đến 2200.");
        if (item.AcademicYears is { } years && (!Regex.IsMatch(years, "^[12][0-9]{3}-[12][0-9]{3}$")
            || int.Parse(years[..4]) >= int.Parse(years[5..])))
            throw new ArgumentException("Niên khóa theo dạng YYYY-YYYY, năm kết thúc lớn hơn năm bắt đầu.");
    }
    private static SqlParameter P(string name, SqlDbType type, object? value, int size = 0) => new(name, type, size) { Value = value ?? DBNull.Value };
    private static SqlParameter Version(string? value, bool isNew)
    {
        if (value is null && !isNew || value is not null && (value.Length != 64 || !value.All(Uri.IsHexDigit)))
            throw new ArgumentException("Thiếu phiên bản hợp lệ. Hãy tải lại danh mục trước khi sửa.");
        return P("@phien_ban", SqlDbType.Binary, value is null ? null : Convert.FromHexString(value), 32);
    }
    private static SqlCommand Command(string operation) => new("dbo.usp_ToChuc_" + operation) { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
    public static SqlCommand SaveCommand(OrganizationRecord item, bool isNew)
    {
        Validate(item);
        var cmd = Command("Luu");
        cmd.Parameters.AddRange([P("@loai", SqlDbType.Int, (int)item.Kind), P("@la_moi", SqlDbType.Bit, isNew),
            P("@ma", SqlDbType.VarChar, item.Code, 20), P("@ten", SqlDbType.NVarChar, item.Name.Trim(), 100),
            P("@ma_cha", SqlDbType.VarChar, item.ParentCode, 20), P("@trang_thai", SqlDbType.Bit, item.Active),
            P("@khoa_hoc", SqlDbType.SmallInt, item.Cohort), P("@nien_khoa", SqlDbType.VarChar, item.AcademicYears, 9),
            P("@mo_ta", SqlDbType.NVarChar, item.Description, 500), Version(item.Version, isNew)]);
        return cmd;
    }
    public static SqlCommand StatusCommand(OrganizationRecord item, bool active)
    {
        ValidateKind(item.Kind);
        if (!ValidCode(item.Code)) throw new ArgumentException("Mã danh mục không hợp lệ.");
        var cmd = Command("TrangThai");
        cmd.Parameters.AddRange([P("@loai", SqlDbType.Int, (int)item.Kind), P("@ma", SqlDbType.VarChar, item.Code, 20),
            P("@trang_thai", SqlDbType.Bit, active), Version(item.Version, false)]);
        return cmd;
    }
    public async Task<DataTable> ListAsync(OrganizationKind kind, string keyword = "", bool? active = null, string? parentCode = null)
    {
        ValidateKind(kind);
        if (keyword.Length > 200 || parentCode is not null && !ValidCode(parentCode)) throw new ArgumentException("Bộ lọc danh mục không hợp lệ.");
        using var cmd = Command("DanhSach");
        cmd.Parameters.AddRange([P("@loai", SqlDbType.Int, (int)kind), P("@tu_khoa", SqlDbType.NVarChar, keyword.Trim(), 200),
            P("@trang_thai", SqlDbType.Bit, active), P("@ma_cha", SqlDbType.VarChar, parentCode, 20)]);
        return await RunAsync(cmd, true);
    }
    public async Task SaveAsync(OrganizationRecord item, bool isNew)
    {
        using var cmd = SaveCommand(item, isNew); using var result = await RunAsync(cmd, false);
    }
    public async Task SetStatusAsync(OrganizationRecord item, bool active)
    {
        using var cmd = StatusCommand(item, active); using var result = await RunAsync(cmd, false);
    }
    private async Task<DataTable> RunAsync(SqlCommand command, bool read)
    {
        try
        {
            using var cn = new SqlConnection(connectionString); command.Connection = cn;
            await AppSqlConnection.OpenAsync(cn).ConfigureAwait(false);
            var table = new DataTable();
            if (read) { using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false); table.Load(reader); }
            else await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            return table;
        }
        catch (SqlException ex)
        {
            throw new PersistenceException(ex.Number switch {
                53400 => "Thông tin khoa, ngành hoặc lớp không hợp lệ. Kiểm tra mã, tên, khóa và niên khóa.",
                53401 => "Mã danh mục đã tồn tại. Hãy dùng mã khác.",
                53402 => "Danh mục đã thay đổi ở phiên khác hoặc không còn tồn tại. Hãy tải lại trước khi sửa.",
                53403 => "Khoa hoặc ngành cấp trên không tồn tại hoặc đã ngừng hoạt động.",
                53404 => "Danh mục đã có sinh viên hoặc chương trình đào tạo; không thể chuyển sang cấp trên khác.",
                2812 => "Cần chạy migration 28_OrganizationCatalogs.sql để quản lý khoa, ngành và lớp.",
                _ => SqlErrorMessages.ForNumber(ex.Number)
            }, ex.Number == 53402 ? PersistenceFailure.DataConflict : PersistenceFailure.General);
        }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể truy cập danh mục khoa, ngành và lớp. Hãy tải lại."); }
    }
    public static OrganizationRecord FromRow(OrganizationKind kind, DataRow row)
    {
        string? Optional(string key) => row.IsNull(key) ? null : Convert.ToString(row[key]);
        return new(kind, Convert.ToString(row["code"])!, Convert.ToString(row["name"])!, Optional("parent_code"),
            Convert.ToBoolean(row["active"]), row.IsNull("cohort") ? null : Convert.ToInt16(row["cohort"]), Optional("academic_years"),
            Optional("description"), row.IsNull("version") ? null : Convert.ToHexString((byte[])row["version"]));
    }
}
