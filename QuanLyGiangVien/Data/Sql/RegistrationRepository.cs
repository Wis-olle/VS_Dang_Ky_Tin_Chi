using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;

namespace QuanLyGiangVien.Data.Sql;

/// <summary>Missing data adapter for Huệ's Function_4 UI. Business rules remain in original SQL procedures.</summary>
public sealed class RegistrationRepository
{
    private readonly string connectionString;

    public RegistrationRepository(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
                throw new ArgumentException();
            builder.ConnectTimeout = Math.Clamp(builder.ConnectTimeout, 1, 15);
            builder.PersistSecurityInfo = false;
            this.connectionString = builder.ConnectionString;
        }
        catch (ArgumentException) { throw new PersistenceException("Cấu hình kết nối SQL không hợp lệ."); }
    }

    public Task<DataTable> NguoiDungAsync() => ExecuteAsync(RegistrationCommands.User());
    public Task<DataTable> HocKyAsync() => ExecuteAsync(RegistrationCommands.Terms());
    public Task<DataTable> ThoiHanAsync(string hk) => ExecuteAsync(RegistrationCommands.Deadline(hk));
    public Task<DataTable> TraCuuAsync(string hk, string? keyword, bool conCho) =>
        ExecuteAsync(RegistrationCommands.Lookup(hk, keyword, conCho));
    public Task<DataTable> DangKyAsync(string lhp) => ExecuteAsync(RegistrationCommands.Register(lhp));
    public Task<DataTable> HuyAsync(long id) => ExecuteAsync(RegistrationCommands.Cancel(id));
    public Task<DataTable> KetQuaAsync(string hk, string? student, bool history) =>
        ExecuteAsync(RegistrationCommands.Results(hk, student, history));
    public Task<DataTable> ThongKeAsync(string hk, int? threshold) =>
        ExecuteAsync(RegistrationCommands.Statistics(hk, threshold));
    public Task<DataTable> DanhSachAsync(string lhp) => ExecuteAsync(RegistrationCommands.Roster(lhp));

    private async Task<DataTable> ExecuteAsync(SqlCommand command)
    {
        using (command)
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(connection).ConfigureAwait(false);
            command.Connection = connection;
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };
            for (var i = 0; i < reader.FieldCount; i++) table.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                table.Rows.Add(values);
            }
            return table;
        }
        catch (SqlException error) { throw new PersistenceException(RegistrationErrorMessages.ForNumber(error.Number)); }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể truy cập SQL. Vui lòng tải lại dữ liệu và kiểm tra kết nối."); }
    }
}

internal static class RegistrationCommands
{
    private static SqlCommand Command(string procedure, params SqlParameter[] parameters)
    {
        var command = new SqlCommand("dbo." + procedure) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        command.Parameters.AddRange(parameters);
        return command;
    }
    private static SqlParameter Code(string name, string code) =>
        new(name, SqlDbType.VarChar, 20) { Value = RegistrationValidation.RequiredCode(code) };
    public static SqlCommand User() => Command("usp_LayNguoiDung");
    public static SqlCommand Terms() => Command("usp_LayHocKy");
    public static SqlCommand Deadline(string hk) => Command("usp_KiemTraThoiHan", Code("@ma_hk", hk));
    public static SqlCommand Lookup(string hk, string? keyword, bool available) => Command("usp_TraCuuLopDangMo",
        Code("@ma_hk", hk), new("@tu_khoa", SqlDbType.NVarChar, 100) { Value = RegistrationValidation.Keyword(keyword) },
        new("@chi_con_cho", SqlDbType.Bit) { Value = available });
    public static SqlCommand Register(string code) => Command("usp_DangKyHocPhan", Code("@ma_lhp", code));
    public static SqlCommand Cancel(long id) => Command("usp_HuyDangKy",
        new SqlParameter("@ma_dk", SqlDbType.BigInt) { Value = RegistrationValidation.RegistrationId(id) });
    public static SqlCommand Results(string hk, string? student, bool history) => Command("usp_XemKetQuaDangKy",
        Code("@ma_hk", hk), new("@ma_sv", SqlDbType.VarChar, 20)
        { Value = student is null ? DBNull.Value : RegistrationValidation.RequiredCode(student) },
        new("@gom_da_huy", SqlDbType.Bit) { Value = history });
    public static SqlCommand Statistics(string hk, int? threshold) => Command("usp_ThongKeLopHocPhan",
        Code("@ma_hk", hk), new("@nguong_it_sv", SqlDbType.Int)
        { Value = (object?)RegistrationValidation.Threshold(threshold) ?? DBNull.Value });
    public static SqlCommand Roster(string code) => Command("usp_DanhSachSinhVienTheoLop", Code("@ma_lhp", code));
}

public static class RegistrationErrorMessages
{
    public static string ForNumber(int number) => number switch {
        51001 => "Tài khoản chưa được ánh xạ hoặc đã bị khóa.",
        51002 => "Vai trò không có quyền sử dụng phân hệ đăng ký.",
        51003 => "Hồ sơ sinh viên không hoạt động.",
        51004 => "Học kỳ không tồn tại. Vui lòng tải lại.",
        51005 => "Chức năng này chỉ dành cho sinh viên.",
        51006 => "Hệ thống đang bận. Vui lòng thử lại.",
        51007 => "Lớp học phần không tồn tại. Vui lòng tải lại.",
        51008 => "Lớp đã thay đổi học kỳ. Vui lòng tải lại.",
        51009 => "Chưa đến hạn, đã hết hạn hoặc đợt đăng ký đã đóng.",
        51010 => "Lớp không nhận đăng ký.",
        51011 => "Học phần không hoạt động.",
        51012 => "Lớp chưa có lịch học; chưa thể đăng ký.",
        51013 => "Bạn đã đăng ký học phần này trong học kỳ.",
        51014 => "Lớp đã đủ sĩ số. Vui lòng tải lại danh sách.",
        51016 => "Lớp này trùng lịch với lớp bạn đã đăng ký.",
        51017 => "Đăng ký không tồn tại, đã hủy hoặc không thuộc về bạn. Vui lòng tải lại.",
        51018 => "Không nằm trong thời hạn hủy hoặc đợt đăng ký đã đóng.",
        51019 => "Hãy nhập mã sinh viên hợp lệ.",
        51020 => "Chỉ phòng đào tạo hoặc quản trị được xem báo cáo này.",
        51021 => "Ngưỡng sinh viên không hợp lệ.",
        _ => SqlErrorMessages.ForNumber(number)
    };
}
