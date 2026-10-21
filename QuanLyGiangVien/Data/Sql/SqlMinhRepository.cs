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

    public Lecturer SaveLecturer(Lecturer value, bool isNew) => value with { Version = Execute(SqlMinhCommands.SaveLecturer(value, isNew), ReadVersion) };
    public Task<System.Collections.Immutable.ImmutableArray<TimetableStudent>> LoadTimetableStudentsAsync(CancellationToken token = default)
        => new TimetableDirectoryRepository(connectionString).LoadStudentsAsync(token);
    public void DeleteLecturer(string id, string version = "") => Execute(SqlMinhCommands.DeleteLecturer(id, version));
    public Room SaveRoom(Room value, bool isNew) => value with { Version = Execute(SqlMinhCommands.SaveRoom(value, isNew), ReadVersion) };
    public void DeleteRoom(string id, string version = "") => Execute(SqlMinhCommands.DeleteRoom(id, version));
    public CourseClass SaveClass(CourseClass value, bool isNew) => Execute(SqlMinhCommands.SaveClass(value, isNew), command =>
    {
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader["si_so_hien_tai"] is not int enrolled || enrolled < 0 || enrolled > value.Capacity
            || reader["phien_ban"] is not byte[] { Length: 8 } version)
            throw new PersistenceException("SQL chưa trả về sĩ số/phiên bản. Vui lòng cập nhật script tích hợp và tải lại dữ liệu.");
        return value with { Enrolled = enrolled, Version = Convert.ToHexString(version),
            HasRegistrationHistory = Convert.ToBoolean(reader["co_lich_su"]),
            Lifecycle = HasColumn(reader,"lifecycle") ? Convert.ToString(reader["lifecycle"])! : value.Lifecycle,
            ActualMeetings = HasColumn(reader,"actual_meetings_json")
                ? LifecycleRules.ReadActualCalendar(Convert.ToString(reader["actual_meetings_json"])) : value.ActualMeetings };
    });
    public void DeleteClass(string id, string version = "") => Execute(SqlMinhCommands.DeleteClass(id, version));

    private static bool HasColumn(System.Data.Common.DbDataReader reader,string name) =>
        Enumerable.Range(0,reader.FieldCount).Any(i=>reader.GetName(i)==name);

    private static string ReadVersion(SqlCommand command) => command.ExecuteScalar() is byte[] { Length: 8 } version
        ? Convert.ToHexString(version) : throw new PersistenceException("SQL chưa trả về phiên bản. Vui lòng cập nhật script tích hợp và tải lại dữ liệu.");

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
        52405 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        52410 => "Database chưa đủ migration hiện tại. Vui lòng nâng cấp database trước khi mở ứng dụng.",
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
        52041 => "Lớp có lịch sử đăng ký; không thể đổi học phần, học kỳ, giảng viên hoặc buổi học. Hãy tạo lớp mới nếu cần.",
        52042 => "Lớp còn đăng ký hợp lệ; cần xử lý đăng ký trước khi hủy lớp.",
        52043 or 52044 => "Trùng phòng hoặc lịch giảng viên vào ngày học thực tế. Vui lòng tải lại và điều chỉnh lịch.",
        52046 => "Dữ liệu đã được người khác cập nhật. Vui lòng đóng form, tải lại bằng F5 và mở lại trước khi sửa hoặc xóa.",
        52047 => "Lớp có lịch sử đăng ký; không thể đổi học phần hoặc học kỳ.",
        52045 => "Lớp có lịch sử đăng ký; không thể xóa.",
        52600 => "Thông tin thao tác vận hành chưa hợp lệ.",
        52601 => "Phiên bản lớp/học kỳ đã thay đổi hoặc đã kết thúc. Hãy tải lại.",
        52602 => "Dùng quy trình vận hành lớp/học kỳ để thay đổi vòng đời hoặc lịch ngoại lệ.",
        52603 => "Còn lớp, tổ hợp hoặc môn đồng hành bị ảnh hưởng. Cần xử lý các đăng ký liên quan trước.",
        52604 => "Có xung đột lịch thực tế. Thay đổi chưa được lưu.",
        52605 => "Ngày học hoặc tài nguyên mới chưa hợp lệ. Không được sửa lại lịch sử đã học.",
        52606 => "Hợp đồng lịch chưa tương thích. Cần kiểm tra migration và đối soát lịch sử.",
        52801 => "Tài khoản chưa được cấp quyền chi tiết cho thao tác này. Liên hệ quản trị.",
        >= 52000 and <= 52099 => "SQL từ chối thao tác vì dữ liệu đã thay đổi hoặc không thỏa điều kiện. Vui lòng tải lại và kiểm tra dữ liệu.",
        >= 51000 and <= 51099 => "Tài khoản hoặc điều kiện nghiệp vụ đăng ký không hợp lệ.",
        _ => "Không thể truy cập dữ liệu SQL. Kiểm tra kết nối, script tích hợp và quyền tài khoản."
    };
}
