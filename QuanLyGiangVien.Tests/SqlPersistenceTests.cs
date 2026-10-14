using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SqlPersistenceTests
{
    [Fact]
    public void FullSnapshotMapsBooleanStatusesSundayAndRegistration()
    {
        using var reader = Tables().CreateDataReader();
        var snapshot = SqlSnapshotMapper.Read(reader);
        Assert.Equal("SV0001", snapshot.Identity.StudentId);
        Assert.Single(snapshot.Faculties);
        Assert.Equal("Đang giảng dạy", snapshot.Lecturers[0].Status);
        Assert.Equal(SchedulingLabels.Ready, snapshot.Rooms[0].Status);
        Assert.Equal(SchedulingLabels.Open, snapshot.Classes[0].Status);
        Assert.Equal(DayOfWeek.Sunday, snapshot.Classes[0].Meetings[0].Day);
        Assert.Equal("DANG_KY", snapshot.Registrations[0].Status);
        var stores = snapshot.CreateStores();
        Assert.Single(stores.Lecturers.Items);
        Assert.Single(stores.Scheduling.Classes);
    }

    [Fact]
    public void EmptySemesterCatalogFailsClearlyBeforeUiSelectsFirstSemester()
    {
        var data = Tables();
        data.Tables[4].Clear();
        data.Tables[6].Clear();
        data.Tables[7].Clear();
        data.Tables[8].Clear();
        using var reader = data.CreateDataReader();
        var error = Assert.Throws<PersistenceException>(() => SqlSnapshotMapper.Read(reader));
        Assert.Contains("học kỳ", error.Message);
    }

    [Theory]
    [InlineData("DANG_MO", SchedulingLabels.Open)]
    [InlineData("FULL", SchedulingLabels.Open)]
    [InlineData("DONG", SchedulingLabels.Closed)]
    [InlineData("HUY", SchedulingLabels.Canceled)]
    public void EveryHueClassStatusMaps(string sql, string expected)
    {
        var data = Tables();
        data.Tables[6].Rows[0]["trang_thai"] = sql;
        if (sql == "HUY") { data.Tables[6].Rows[0]["si_so_hien_tai"] = 0; data.Tables[2].Rows[0]["active_classes"] = 0; data.Tables[8].Clear(); }
        using var reader = data.CreateDataReader();
        Assert.Equal(expected, SqlSnapshotMapper.Read(reader).Classes[0].Status);
    }

    [Fact]
    public void UnknownStatusOrMissingResultSetFailsWithoutDemoFallback()
    {
        var data = Tables();
        data.Tables[6].Rows[0]["trang_thai"] = "MO";
        using (var reader = data.CreateDataReader())
            Assert.Throws<PersistenceException>(() => SqlSnapshotMapper.Read(reader));
        data.Tables.RemoveAt(8);
        using (var reader = data.CreateDataReader())
            Assert.Throws<PersistenceException>(() => SqlSnapshotMapper.Read(reader));
    }

    [Fact]
    public void ClassCommandUsesTypedParametersAndHueWeekdayJsonWithoutEnrollmentWrites()
    {
        var demo = SchedulingDemo.Create();
        var item = demo.Classes[0] with { Meetings = [demo.Classes[0].Meetings[0] with { Day = DayOfWeek.Sunday }] };
        using var command = SqlMinhCommands.SaveClass(item, false);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Equal("dbo.usp_Minh_LuuLop", command.CommandText);
        Assert.Equal(SqlDbType.VarChar, command.Parameters["@ma_lhp"].SqlDbType);
        Assert.Equal(20, command.Parameters["@ma_lhp"].Size);
        Assert.Equal("DANG_MO", command.Parameters["@trang_thai"].Value);
        Assert.False(command.Parameters.Contains("@si_so_hien_tai"));
        using var json = JsonDocument.Parse((string)command.Parameters["@lich_json"].Value);
        Assert.Equal(8, json.RootElement[0].GetProperty("Thu").GetInt32());
        Assert.Equal(item.Meetings[0].StartDate.ToString("yyyy-MM-dd"), json.RootElement[0].GetProperty("StartDate").GetString());
        Assert.InRange(command.CommandTimeout, 1, 30);
    }

    [Fact]
    public void LecturerParametersPreserveUnicodeDateAndBitAndDoNotInterpolateInput()
    {
        var item = DemoData.Create()[0] with { Name = "Trần O’Minh", Status = "Tạm nghỉ" };
        using var command = SqlMinhCommands.SaveLecturer(item, true);
        Assert.Equal(SqlDbType.NVarChar, command.Parameters["@ho_ten"].SqlDbType);
        Assert.Equal(item.Name, command.Parameters["@ho_ten"].Value);
        Assert.Equal(SqlDbType.Date, command.Parameters["@ngay_sinh"].SqlDbType);
        Assert.Equal(false, command.Parameters["@trang_thai"].Value);
        Assert.Equal(true, command.Parameters["@la_moi"].Value);
        Assert.DoesNotContain(item.Name, command.CommandText);
    }

    [Fact]
    public void DirectCommandFactoriesRejectTruncationAndLossyVarcharConversion()
    {
        Assert.Throws<PersistenceException>(() => SqlMinhCommands.SaveLecturer(DemoData.Create()[0] with { Name = new string('A', 101) }, true));
        Assert.Throws<PersistenceException>(() => SqlMinhCommands.SaveRoom(new("P1234", new string('A', 61), 40, SchedulingLabels.Theory, SchedulingLabels.Ready), true));
        Assert.Throws<PersistenceException>(() => SqlMinhCommands.SaveClass(SchedulingDemo.Create().Classes[0] with { CourseId = new string('A', 21) }, false));
        Assert.Throws<PersistenceException>(() => SqlMinhCommands.DeleteRoom("Phòng thử"));
    }

    [Fact]
    public void InvalidConnectionConfigurationNeverEchoesCredentials()
    {
        var error = Assert.Throws<PersistenceException>(() => new SqlMinhRepository("Password=secret-never-echo;Unknown=bad"));
        Assert.DoesNotContain("secret-never-echo", error.Message);
    }

    [Fact]
    public void MapperAcceptsInactiveResourcesAndCanceledRegistration()
    {
        var data = Tables();
        data.Tables[2].Rows[0]["trang_thai"] = false;
        data.Tables[2].Rows[0]["active_classes"] = 0;
        data.Tables[5].Rows[0]["trang_thai"] = false;
        data.Tables[6].Rows[0]["trang_thai"] = "HUY";
        data.Tables[6].Rows[0]["si_so_hien_tai"] = 0;
        data.Tables[8].Rows[0]["trang_thai"] = "DA_HUY";
        using var reader = data.CreateDataReader();
        var snapshot = SqlSnapshotMapper.Read(reader);
        Assert.Equal("Tạm nghỉ", snapshot.Lecturers[0].Status);
        Assert.Equal(SchedulingLabels.Maintenance, snapshot.Rooms[0].Status);
        Assert.Equal("DA_HUY", snapshot.Registrations[0].Status);
    }

    [Theory]
    [InlineData(2, "avatar_key", "../bad")]
    [InlineData(7, "thu", 1)]
    [InlineData(8, "trang_thai", "HUY")]
    [InlineData(0, "vai_tro", "ADMIN")]
    public void InvalidDatabaseBoundaryDataIsRejected(int table, string column, object value)
    {
        var data = Tables();
        data.Tables[table].Rows[0][column] = value;
        using var reader = data.CreateDataReader();
        Assert.Throws<PersistenceException>(() => SqlSnapshotMapper.Read(reader));
    }

    [Fact]
    public void ManagementChecksCountsButStudentSnapshotCanContainOnlyOwnRegistration()
    {
        var data = Tables();
        data.Tables[6].Rows[0]["si_so_hien_tai"] = 20;
        using (var reader = data.CreateDataReader())
            Assert.Equal(20, SqlSnapshotMapper.Read(reader).Classes[0].Enrolled);
        data.Tables[0].Rows[0]["vai_tro"] = "DAO_TAO";
        using (var reader = data.CreateDataReader())
            Assert.Throws<PersistenceException>(() => SqlSnapshotMapper.Read(reader));
    }

    [Fact]
    public void OptionalMajorNameMapsWhenPresent()
    {
        var data = Tables();
        data.Tables[0].Columns.Add("ten_nganh", typeof(string));
        data.Tables[0].Rows[0]["ten_nganh"] = "Kỹ thuật phần mềm";
        using var reader = data.CreateDataReader();
        Assert.Equal("Kỹ thuật phần mềm", SqlSnapshotMapper.Read(reader).Identity.MajorName);
    }

    [Fact]
    public void ReregistrationHistoryAllowsCanceledRowsButRejectsTwoActiveRegistrations()
    {
        var data = Tables();
        data.Tables[0].Rows[0]["vai_tro"] = "DAO_TAO";
        data.Tables[8].Rows.Add("SV0001", "LHP0001", "DA_HUY");
        data.Tables[8].Rows.Add("SV0001", "LHP0001", "DA_HUY");
        using (var reader = data.CreateDataReader()) Assert.Equal(3, SqlSnapshotMapper.Read(reader).Registrations.Length);
        data.Tables[8].Rows.Add("SV0001", "LHP0001", "DANG_KY");
        using (var reader = data.CreateDataReader()) Assert.Throws<PersistenceException>(() => SqlSnapshotMapper.Read(reader));
    }

    [Fact]
    public void RoomAndDeleteCommandsUseExplicitTypesAndParameters()
    {
        using var room = SqlMinhCommands.SaveRoom(new("P1234", "Tòa thử", 60, SchedulingLabels.Lab, SchedulingLabels.Maintenance), false);
        Assert.Equal("dbo.usp_Minh_LuuPhong", room.CommandText);
        Assert.Equal(SqlDbType.NVarChar, room.Parameters["@toa_nha"].SqlDbType);
        Assert.Equal(60, room.Parameters["@toa_nha"].Size);
        Assert.Equal(false, room.Parameters["@trang_thai"].Value);
        Assert.Equal(60, room.Parameters["@suc_chua"].Value);
        using var lecturerDelete = SqlMinhCommands.DeleteLecturer("GV9999");
        using var roomDelete = SqlMinhCommands.DeleteRoom("P1234");
        using var classDelete = SqlMinhCommands.DeleteClass("LHP9999");
        Assert.Equal("GV9999", lecturerDelete.Parameters["@ma_gv"].Value);
        Assert.Equal("P1234", roomDelete.Parameters["@ma_phong"].Value);
        Assert.Equal("LHP9999", classDelete.Parameters["@ma_lhp"].Value);
        Assert.All(new[] { lecturerDelete, roomDelete, classDelete }, command => {
            Assert.Single(command.Parameters.Cast<SqlParameter>());
            Assert.Equal(SqlDbType.VarChar, command.Parameters[0].SqlDbType);
            Assert.Equal(20, command.Parameters[0].Size);
        });
    }

    [Theory]
    [InlineData(SchedulingLabels.Closed, "DONG")]
    [InlineData(SchedulingLabels.Canceled, "HUY")]
    public void ClassSaveTranslatesRemainingStatuses(string label, string expected)
    {
        using var command = SqlMinhCommands.SaveClass(SchedulingDemo.Create().Classes[0] with { Status = label }, false);
        Assert.Equal(expected, command.Parameters["@trang_thai"].Value);
    }

    [Theory]
    [InlineData(-2)] [InlineData(1205)] [InlineData(1222)] [InlineData(18456)] [InlineData(229)]
    [InlineData(2601)] [InlineData(2627)] [InlineData(547)] [InlineData(52001)] [InlineData(52002)]
    [InlineData(52011)] [InlineData(52012)] [InlineData(52013)] [InlineData(52014)] [InlineData(52015)]
    [InlineData(52016)] [InlineData(52023)] [InlineData(52024)] [InlineData(52033)] [InlineData(52036)]
    [InlineData(52037)] [InlineData(52038)] [InlineData(52039)] [InlineData(52040)] [InlineData(52041)]
    [InlineData(52042)] [InlineData(52043)] [InlineData(52045)] [InlineData(52099)] [InlineData(51001)]
    [InlineData(12345)]
    public void SqlErrorNumbersHaveSafeUserFacingMessages(int number)
    {
        var message = SqlErrorMessages.ForNumber(number);
        Assert.NotEmpty(message);
        Assert.DoesNotContain("Server=", message);
        Assert.DoesNotContain("Password=", message);
    }

    internal static DataSet Tables()
    {
        var result = new DataSet();
        Add("ma_tk,ma_sv,vai_tro,ho_ten,ma_lop", "1", "SV0001", "SINH_VIEN", "Nguyễn Minh", "LHC01");
        Add("ma_khoa,ten_khoa", "CNTT", "Công nghệ thông tin");
        Add("ma_gv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,ten_khoa,hoc_vi,avatar_key,trang_thai,active_classes",
            "GV0001", "Nguyễn Văn An", new DateTime(1988, 1, 1), "Nam", "an@example.edu.vn", "0912345678", "Công nghệ thông tin", "Thạc sĩ", "portrait-01", true, 1);
        Add("ma_hp,ten_hp,so_tin_chi,ten_khoa", "IT101", "Cơ sở dữ liệu", 3, "Công nghệ thông tin");
        Add("ma_hk,ten_hk,ngay_hoc_bd,ngay_hoc_kt", "HK126", "Học kỳ I", new DateTime(2026, 9, 7), new DateTime(2026, 12, 27));
        Add("ma_phong,toa_nha,suc_chua,loai_phong,trang_thai", "A201", "Tòa A", 60, SchedulingLabels.Theory, true);
        Add("ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,si_so_hien_tai,trang_thai", "LHP0001", "IT101", "GV0001", "HK126", 40, 1, "DANG_MO");
        Add("ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt", "LHP0001", "A201", 8, 1, 3, new DateTime(2026, 9, 7), new DateTime(2026, 12, 27));
        Add("ma_sv,ma_lhp,trang_thai", "SV0001", "LHP0001", "DANG_KY");
        return result;

        void Add(string columns, params object[] row)
        {
            var table = new DataTable();
            foreach (var (name, index) in columns.Split(',').Select((name, index) => (name, index)))
                table.Columns.Add(name, row[index].GetType());
            table.Rows.Add(row);
            result.Tables.Add(table);
        }
    }
}
