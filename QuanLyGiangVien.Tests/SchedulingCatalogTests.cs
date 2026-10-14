using QuanLyGiangVien.Data;
using QuanLyGiangVien.Domain.Scheduling;
using Xunit;
using static QuanLyGiangVien.Tests.SchedulingStoreTests;

namespace QuanLyGiangVien.Tests;

public sealed class SchedulingCatalogTests
{
    [Fact]
    public void CatalogCopiesAndNormalizesCourseAndSemesterReferences()
    {
        var courses = new[] { new Course(" it101 ", " Lập trình ", 3, " Công nghệ thông tin ") };
        var semesters = new[] { new Semester(" hk1 ", " Học kỳ I ", Start, End) };
        var catalog = new TeachingCatalog(courses, semesters, DemoData.Create);
        courses[0] = courses[0] with { Name = "Changed" };
        semesters[0] = semesters[0] with { Name = "Changed" };
        Assert.Equal("IT101", catalog.Courses[0].Id);
        Assert.Equal("Lập trình", catalog.Courses[0].Name);
        Assert.Equal("HK1", catalog.Semesters[0].Id);
        Assert.Equal("Học kỳ I", catalog.Semesters[0].Name);
    }

    [Fact]
    public void CatalogRejectsInvalidOrDuplicateReferences()
    {
        var course = new Course("IT101", "Lập trình", 3, "Công nghệ thông tin");
        var semester = new Semester("HK1", "Học kỳ I", Start, End);
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course, course with { Id = " it101 " }], [semester], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course with { Credits = 0 }], [semester], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([null!], [semester], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course], [semester, semester], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course], [semester with { StartDate = default }], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course], [semester with { EndDate = Start.AddDays(-1) }], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course], [semester with { EndDate = Start.AddDays(367) }], DemoData.Create));
        Assert.Throws<ArgumentException>(() => new TeachingCatalog([course], [null!], DemoData.Create));
    }
}
