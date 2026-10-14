using QuanLyGiangVien.Domain.Catalogs;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class CatalogValidationTests
{
    private static HocPhan Course => new() { Ma="HP_TEST", Ten="Học phần", TinChi=3, Min=3, Max=3, HoatDong=true };
    private static HocKy Semester => new() { Ma="HK_TEST", NamHoc="2026-2027", Ky=1,
        BatDau=new(2026,10,1), KetThuc=new(2026,10,15), HanHuy=new(2026,10,20),
        NgayHocBatDau=new(2026,11,1), NgayHocKetThuc=new(2027,2,1) };

    [Fact] public void ValidCourseAndSemesterAreAccepted() { Validation.HocPhan(Course); Validation.HocKy(Semester); }
    [Fact] public void HueCourseNameLimitIs150() => Assert.Throws<ArgumentException>(()=>Validation.HocPhan(Course with {Ten=new string('a',151)}));
    [Fact] public void MinhCourseCreditLimitIs12() => Assert.Throws<ArgumentException>(()=>Validation.HocPhan(Course with {TinChi=13,Max=13}));
    [Theory] [InlineData("a b")] [InlineData("á")] [InlineData("")] [InlineData("123456789012345678901")]
    public void InvalidCodesAreRejected(string code) => Assert.Throws<ArgumentException>(()=>Validation.HocPhan(Course with {Ma=code}));
    [Theory] [InlineData(0,3,3)] [InlineData(4,3,5)] [InlineData(1,4,3)] [InlineData(1,3,31)]
    public void InvalidCreditBoundsAreRejected(byte minimum,byte credits,byte maximum) => Assert.Throws<ArgumentException>(()=>Validation.HocPhan(Course with {Min=minimum,TinChi=credits,Max=maximum}));
    [Fact] public void UnknownCourseTypeIsRejected()=>Assert.Throws<ArgumentException>(()=>Validation.HocPhan(Course with {Loai="OTHER"}));
    [Fact] public void MissingTeachingDatesAreRejected()=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {NgayHocBatDau=default}));
    [Fact] public void ReversedTeachingDatesAreRejected()=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {NgayHocKetThuc=new(2026,10,1)}));
    [Fact] public void TeachingPeriodCannotExceed366Days()=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {NgayHocKetThuc=new(2028,1,1)}));
    [Theory] [InlineData("2026-2026")] [InlineData("2026-2028")] [InlineData("abc")]
    public void AcademicYearsMustBeConsecutive(string year)=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {NamHoc=year}));
    [Fact] public void InvalidRegistrationPeriodIsRejected()=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {KetThuc=Semester.BatDau}));
    [Fact] public void CancellationCannotPrecedeRegistration()=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {HanHuy=Semester.BatDau.AddSeconds(-1)}));
    [Fact] public void SemesterMustBe1To3()=>Assert.Throws<ArgumentException>(()=>Validation.HocKy(Semester with {Ky=4}));
}
