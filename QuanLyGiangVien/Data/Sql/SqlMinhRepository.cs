using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data.Sql;

public sealed class SqlMinhRepository : ILecturerPersistence, ISchedulePersistence
{
    private readonly string connectionString;

    public SqlMinhRepository(string connectionString)
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
        catch (ArgumentException) { throw new PersistenceException("Cấu hình kết nối SQL không hợp lệ. Kiểm tra MINH_DB_CONNECTION."); }
    }

    public async Task<DatabaseSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(connection, cancellationToken).ConfigureAwait(false);
            using var command = SqlMinhCommands.Load();
            command.Connection = connection;
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return SqlSnapshotMapper.Read(reader) with { Repository = this };
        }
        catch (SqlException exception) { throw new PersistenceException(SqlErrorMessages.ForNumber(exception.Number)); }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể mở kết nối SQL. Kiểm tra cấu hình và dịch vụ SQL Server."); }
    }

    public void SaveLecturer(Lecturer value, bool isNew) => Execute(SqlMinhCommands.SaveLecturer(value, isNew));
    public void DeleteLecturer(string id) => Execute(SqlMinhCommands.DeleteLecturer(id));
    public void SaveRoom(Room value, bool isNew) => Execute(SqlMinhCommands.SaveRoom(value, isNew));
    public void DeleteRoom(string id) => Execute(SqlMinhCommands.DeleteRoom(id));
    public int SaveClass(CourseClass value, bool isNew) => Execute(SqlMinhCommands.SaveClass(value, isNew), command =>
        command.ExecuteScalar() is int enrolled && enrolled >= 0 && enrolled <= value.Capacity ? enrolled
            : throw new PersistenceException("SQL chưa trả về sĩ số thực tế. Vui lòng cập nhật script tích hợp và tải lại dữ liệu."));
    public void DeleteClass(string id) => Execute(SqlMinhCommands.DeleteClass(id));

    private void Execute(SqlCommand command) => Execute(command, value => value.ExecuteNonQuery());

    private T Execute<T>(SqlCommand command, Func<SqlCommand, T> execute)
    {
        using (command)
        try
        {
            using var connection = new SqlConnection(connectionString);
            AppSqlConnection.Open(connection);
            command.Connection = connection;
            return execute(command);
        }
        catch (SqlException exception) { throw new PersistenceException(SqlErrorMessages.ForNumber(exception.Number)); }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể lưu vào SQL. Kiểm tra cấu hình kết nối và thử lại."); }
    }
}

public static class SqlErrorMessages
{
    public static string ForNumber(int number) => number switch {
        52105 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng xuất và đăng nhập lại.",
        -2 => "Hết thời gian chờ SQL; chưa xác nhận được kết quả. Vui lòng tải lại dữ liệu trước khi thử lại.",
        1222 or 52002 => "SQL đang bận. Vui lòng tải lại dữ liệu và thử lại.",
        1205 => "Có thao tác đồng thời trên SQL. Dữ liệu chưa thay đổi; vui lòng thử lại.",
        18456 or 229 or 52001 => "Tài khoản SQL chưa có quyền thực hiện thao tác này.",
        2601 or 2627 => "Mã hoặc email đã tồn tại trong SQL. Vui lòng tải lại dữ liệu trước khi thử lại.",
        547 => "Dữ liệu đang được tham chiếu hoặc chưa có trong danh mục chung. Vui lòng tải lại và kiểm tra.",
        52011 => "Khoa không hoạt động hoặc tên khoa không duy nhất. Vui lòng kiểm tra danh mục khoa.",
        52012 or 52021 or 52034 => "Mã đã tồn tại trong SQL. Vui lòng tải lại dữ liệu trước khi thử lại.",
        52013 or 52022 or 52035 => "Dữ liệu không còn tồn tại trong SQL. Vui lòng tải lại.",
        52014 => "Giảng viên còn được phân công lớp; vui lòng chuyển phân công trước khi tạm nghỉ.",
        52015 => "Email hoặc số điện thoại đã được sử dụng.",
        52016 => "Giảng viên có dữ liệu liên kết; không thể xóa.",
        52023 => "Phòng còn lớp sử dụng hoặc sức chứa thấp hơn sĩ số tối đa lớp.",
        52024 => "Phòng có lịch hoặc lớp liên kết; không thể xóa.",
        52033 => "Học kỳ chưa có ngày bắt đầu và kết thúc học. Vui lòng bổ sung dữ liệu học kỳ.",
        52036 => "Học phần cần đang hoạt động và có từ 1 đến 12 tín chỉ.",
        52037 => "Giảng viên không còn tồn tại hoặc đang tạm nghỉ. Vui lòng tải lại dữ liệu.",
        52038 => "Phòng không sẵn sàng hoặc không đủ sức chứa cho sĩ số tối đa.",
        52039 => "Ngày buổi học phải nằm trong thời gian học của học kỳ.",
        52040 => "Sĩ số tối đa không được thấp hơn số đăng ký thực tế. Vui lòng tải lại dữ liệu.",
        52041 => "Lớp đã có sinh viên đăng ký; không thể đổi học phần, học kỳ, giảng viên hoặc buổi học.",
        52042 => "Lớp còn đăng ký hợp lệ; cần xử lý đăng ký trước khi hủy lớp.",
        52043 or 52044 => "Trùng phòng hoặc lịch giảng viên vào ngày học thực tế. Vui lòng tải lại và điều chỉnh lịch.",
        52045 => "Lớp có lịch sử đăng ký; không thể xóa.",
        >= 52000 and <= 52099 => "SQL từ chối thao tác vì dữ liệu đã thay đổi hoặc không thỏa điều kiện. Vui lòng tải lại và kiểm tra dữ liệu.",
        >= 51000 and <= 51099 => "Tài khoản hoặc điều kiện nghiệp vụ đăng ký không hợp lệ.",
        _ => "Không thể truy cập dữ liệu SQL. Kiểm tra kết nối, script tích hợp và quyền tài khoản."
    };
}
