using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Catalogs;
using Xunit;

namespace QuanLyGiangVien.Tests;

[Collection("Shared SQL fixture")]
public sealed class CatalogIntegrationTests
{
    private static string Connection
    {
        get {
            var value=Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
            Assert.StartsWith("DangKyTinChi_Minh_Test_",new SqlConnectionStringBuilder(value).InitialCatalog,StringComparison.Ordinal);
            return value;
        }
    }
    private static HocPhan Course(string code)=>new() {Ma=code,Ten="Học phần kiểm thử",TinChi=3,Min=3,Max=3,HoatDong=true};
    private static HocPhan FindCourse(DataTable table,string code)=>HocPhan.FromRow(table.AsEnumerable().Single(r=>(string)r["ma_hp"]==code));
    private static HocKy FindSemester(DataTable table,string code)=>HocKy.FromRow(table.AsEnumerable().Single(r=>(string)r["ma_hk"]==code));

    [SqlIntegrationFact]
    public async Task CoursesCrudAndOptimisticConcurrencyPreserveNullableFaculty()
    {
        var repo=new CatalogRepository(Connection);var code="CT"+Guid.NewGuid().ToString("N")[..10];
        await repo.SaveHocPhanAsync(Course(code),true);
        try {
            var original=FindCourse(await repo.HocPhanAsync(code,null,null),code);
            Assert.Null(original.MaKhoa);Assert.Equal(64,original.PhienBan!.Length);
            await repo.SaveHocPhanAsync(original with {Ten="Tên đã sửa"},false);
            await Assert.ThrowsAsync<PersistenceException>(()=>repo.SaveHocPhanAsync(original with {Ten="Ghi đè cũ"},false));
            var fresh=FindCourse(await repo.HocPhanAsync(code,null,null),code);
            Assert.Equal("Tên đã sửa",fresh.Ten);Assert.NotEqual(original.PhienBan,fresh.PhienBan);
            await repo.SaveHocPhanAsync(fresh with {HoatDong=false},false);
            Assert.Empty((await repo.HocPhanAsync(code,null,true)).Rows.Cast<DataRow>());
        }
        finally {await repo.DeleteHocPhanAsync(FindCourse(await repo.HocPhanAsync(code,null,null),code));}
        Assert.Empty((await repo.HocPhanAsync(code,null,null)).Rows.Cast<DataRow>());
    }

    [SqlIntegrationFact]
    public async Task SemesterStartsClosedAndSupportsStatusConcurrencyAndTeachingDates()
    {
        var repo=new CatalogRepository(Connection);var code="CK"+Guid.NewGuid().ToString("N")[..10];
        var semester=new HocKy {Ma=code,NamHoc="2898-2899",Ky=3,BatDau=new(2898,6,1),KetThuc=new(2898,6,15),HanHuy=new(2898,6,20),NgayHocBatDau=new(2898,7,1),NgayHocKetThuc=new(2898,9,1)};
        await repo.SaveHocKyAsync(semester,true);
        try {
            var original=FindSemester(await repo.HocKyAsync(code),code);Assert.Equal("DONG",original.TrangThai);
            await repo.SetHocKyStatusAsync(original,"DANG_MO");
            await Assert.ThrowsAsync<PersistenceException>(()=>repo.SetHocKyStatusAsync(original,"DONG"));
            var opened=FindSemester(await repo.HocKyAsync(code),code);Assert.Equal("DANG_MO",opened.TrangThai);
            await repo.SaveHocKyAsync(opened with {NgayHocKetThuc=opened.NgayHocKetThuc.AddDays(1)},false);
            var saved=FindSemester(await repo.HocKyAsync(code),code);Assert.Equal("DANG_MO",saved.TrangThai);
            Assert.Equal(semester.NgayHocKetThuc.AddDays(1),saved.NgayHocKetThuc);
            await repo.SetHocKyStatusAsync(saved,"DONG");
        }
        finally {await repo.DeleteHocKyAsync(FindSemester(await repo.HocKyAsync(code),code));}
    }

    [SqlIntegrationFact]
    public async Task ExistingRegisteredCourseMetadataAndUsedSemesterCannotBeDeleted()
    {
        var repo=new CatalogRepository(Connection);var snapshot=await new SqlMinhRepository(Connection).LoadAsync();
        var registration=snapshot.Registrations.First(r=>r.Status=="DANG_KY");
        var item=snapshot.Classes.Single(c=>c.Id==registration.ClassId);
        var course=FindCourse(await repo.HocPhanAsync(item.CourseId,null,null),item.CourseId);
        await Assert.ThrowsAsync<PersistenceException>(()=>repo.SaveHocPhanAsync(course with {Ten=course.Ten+" đổi"},false));
        await Assert.ThrowsAsync<PersistenceException>(()=>repo.SaveHocPhanAsync(course with {HoatDong=false},false));
        await Assert.ThrowsAsync<PersistenceException>(()=>repo.DeleteHocPhanAsync(course));
        var semester=FindSemester(await repo.HocKyAsync(item.SemesterId),item.SemesterId);
        await Assert.ThrowsAsync<PersistenceException>(()=>repo.DeleteHocKyAsync(semester));
        var withMeeting=snapshot.Classes.First(c=>c.Meetings.Length>0);
        var teaching=FindSemester(await repo.HocKyAsync(withMeeting.SemesterId),withMeeting.SemesterId);
        await Assert.ThrowsAsync<PersistenceException>(()=>repo.SaveHocKyAsync(teaching with {NgayHocKetThuc=teaching.NgayHocBatDau},false));
        Assert.Equal(course,FindCourse(await repo.HocPhanAsync(item.CourseId,null,null),item.CourseId));
    }

    [SqlIntegrationFact]
    public async Task ServerRejectsInvalidCreditNameAndCodeEvenIfClientValidationIsBypassed()
    {
        using var connection=new SqlConnection(Connection);await connection.OpenAsync();
        using var command=new SqlCommand("dbo.usp_Phong_HocPhan_Luu",connection) {CommandType=CommandType.StoredProcedure};
        command.Parameters.AddRange([
            new("@la_moi",SqlDbType.Bit){Value=true},new("@ma_hp",SqlDbType.VarChar,128){Value="CT_INVALID"},
            new("@ten_hp",SqlDbType.NVarChar,4000){Value="Không hợp lệ"},new("@so_tin_chi",SqlDbType.TinyInt){Value=13},
            new("@loai_hp",SqlDbType.VarChar,20){Value="BAT_BUOC"},new("@tin_chi_min",SqlDbType.TinyInt){Value=3},
            new("@tin_chi_max",SqlDbType.TinyInt){Value=15},new("@trang_thai",SqlDbType.Bit){Value=true}]);
        var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(52100,error.Number);
        command.Parameters["@so_tin_chi"].Value=3;command.Parameters["@ten_hp"].Value=new string('x',151);
        var nameError=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(52100,nameError.Number);
        command.Parameters["@ten_hp"].Value="Tên hợp lệ";command.Parameters["@ma_hp"].Value=new string('x',21);
        var codeError=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(52100,codeError.Number);
    }
}
