using QuanLyGiangVien.Domain.Shell;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class CampusNavigationTests
{
    [Theory]
    [InlineData("DAO_TAO")]
    [InlineData("QUAN_TRI")]
    public void TrainingAndAdminReceiveOnlyAvailableManagementModules(string role)
    {
        Assert.Equal(new[] { CampusModule.Lecturers, CampusModule.Scheduling, CampusModule.Catalogs, CampusModule.Students, CampusModule.Academics, CampusModule.Reports },
            CampusNavigation.ForRole(role));
        Assert.DoesNotContain(CampusModule.Registration, CampusNavigation.ForRole(role));
    }

    [Fact]
    public void StudentReceivesRegistrationAndOwnTimetable()
    {
        Assert.Equal(new[] { CampusModule.Registration, CampusModule.Timetable }, CampusNavigation.ForRole("SINH_VIEN"));
        Assert.False(CampusNavigation.CanOpen("SINH_VIEN", CampusModule.Catalogs));
        Assert.False(CampusNavigation.CanOpen("SINH_VIEN", CampusModule.Reports));
        Assert.False(CampusNavigation.CanOpen("SINH_VIEN", CampusModule.Students));
        Assert.False(CampusNavigation.CanOpen("SINH_VIEN", CampusModule.Academics));
    }

    [Theory]
    [InlineData("")]
    [InlineData("GIANG_VIEN")]
    [InlineData("Admin")]
    public void UnknownOrUnimplementedRoleCannotOpenModules(string role)
    {
        Assert.Empty(CampusNavigation.ForRole(role));
    }

    [Fact]
    public void ModulesHaveConcreteVietnameseTitles()
    {
        Assert.Equal("Giảng viên", CampusNavigation.Title(CampusModule.Lecturers));
        Assert.Equal("Học phần & học kỳ", CampusNavigation.Title(CampusModule.Catalogs));
        Assert.Equal("Tra cứu & đăng ký", CampusNavigation.Title(CampusModule.Registration));
    }
}
