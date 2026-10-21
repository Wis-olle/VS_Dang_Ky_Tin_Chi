using QuanLyGiangVien.Domain;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class LecturerTests
{
    private static Lecturer Sample(string id = "GV0001", string email = "minh@example.edu.vn", int classes = 0) =>
        new(id, "Nguyễn Đăng Minh", new DateTime(1988, 3, 12), "Nam", email,
            "0912345678", "Công nghệ thông tin", "Thạc sĩ", "Đang giảng dạy", "portrait-01", classes);

    [Fact]
    public void AddValidLecturerCreatesNewSnapshot()
    {
        var original = Sample();
        var source = new[] { original };
        var store = new LecturerStore(source);
        var snapshot = store.Items;
        Assert.True(store.TrySave(Sample("GV0002", "lan@example.edu.vn"), true, out var errors));
        Assert.Empty(errors);
        Assert.Single(snapshot);
        Assert.Equal(2, store.Items.Count);
        Assert.Same(original, store.Items[0]);
        Assert.Single(source);
        Assert.Equal("GV0003", store.NextId());
    }

    [Fact]
    public void UpdatePreservesOldRecordAndSnapshot()
    {
        var original = Sample();
        var store = new LecturerStore([original]);
        var snapshot = store.Items;
        var changed = original with { Name = "Trần Thị Lan", Phone = "+84912345678" };
        Assert.True(store.TrySave(changed, false, out var errors));
        Assert.Empty(errors);
        Assert.Equal("Nguyễn Đăng Minh", snapshot[0].Name);
        Assert.Equal("Trần Thị Lan", store.Items[0].Name);
        Assert.Equal("0912345678", store.Items[0].Phone);
    }

    [Fact]
    public void InvalidFieldsHavePropertyErrorsWithoutChangingStore()
    {
        var store = new LecturerStore([Sample()]);
        var invalid = Sample() with {
            Id = "INVALID?", Name = "1", BirthDate = DateTime.Today.AddDays(1), Gender = "?",
            Email = "bad", Phone = "123", Faculty = "", Degree = "", Status = "?", ActiveClasses = -1
        };
        Assert.False(store.TrySave(invalid, true, out var errors));
        foreach (var property in new[] { "Id", "Name", "BirthDate", "Gender", "Email", "Phone", "Faculty", "Degree", "Status", "ActiveClasses" })
            Assert.Contains(property, errors.Keys);
        Assert.Single(store.Items);
    }

    [Theory]
    [InlineData("gv0001", "unique@example.edu.vn", "Id")]
    [InlineData("GV0002", "MINH@example.edu.vn", "Email")]
    public void DuplicateValuesAreRejectedIgnoringCase(string id, string email, string key)
    {
        var store = new LecturerStore([Sample()]);
        Assert.False(store.TrySave(Sample(id, email), true, out var errors));
        Assert.Contains(key, errors.Keys);
    }

    [Fact]
    public void MissingUpdateIsRejected()
    {
        var store = new LecturerStore([]);
        Assert.False(store.TrySave(Sample(), false, out var errors));
        Assert.Contains("Id", errors.Keys);
        Assert.Equal("GV0001", store.NextId());
    }

    [Fact]
    public void UpdateCannotUseAnotherLecturersEmail()
    {
        var store = new LecturerStore([Sample(), Sample("GV0002", "lan@example.edu.vn")]);
        Assert.False(store.TrySave(Sample() with { Email = "lan@example.edu.vn" }, false, out var errors));
        Assert.Contains("Email", errors.Keys);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(17)]
    public void UnderageBirthDateIsRejected(int age)
    {
        var store = new LecturerStore([]);
        Assert.False(store.TrySave(Sample() with { BirthDate = DateTime.Today.AddYears(-age) }, true, out var errors));
        Assert.Contains("BirthDate", errors.Keys);
    }

    [Fact]
    public void DefaultBirthDateIsRejected()
    {
        var store = new LecturerStore([]);
        Assert.False(store.TrySave(Sample() with { BirthDate = default }, true, out var errors));
        Assert.Contains("BirthDate", errors.Keys);
    }

    [Fact]
    public void DeleteProtectsAssignedLecturers()
    {
        var store = new LecturerStore([Sample(classes: 2)]);
        Assert.False(store.TryDelete("GV0001", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Single(store.Items);
        Assert.False(store.TryDelete("GV9999", out error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DeleteUnassignedLecturerCreatesNewSnapshot()
    {
        var store = new LecturerStore([Sample()]);
        var snapshot = store.Items;
        Assert.True(store.TryDelete("gv0001", out var error));
        Assert.Null(error);
        Assert.Empty(store.Items);
        Assert.Single(snapshot);
    }

    [Theory]
    [InlineData("nguyen dang", 1)]
    [InlineData("GV0001", 1)]
    [InlineData("example.edu", 2)]
    [InlineData("091234", 2)]
    [InlineData("cong nghe", 1)]
    [InlineData("no-match", 0)]
    public void SearchIsAccentInsensitiveAcrossFields(string query, int count)
    {
        var other = Sample("GV0002", "lan@example.edu.vn") with { Name = "Trần Thị Lan", Faculty = "Kinh tế" };
        Assert.Equal(count, LecturerQuery.Filter([Sample(), other], query, "Tất cả khoa", "Tất cả trạng thái").Count);
    }

    [Fact]
    public void QueryCombinesFiltersWithoutChangingInput()
    {
        var original = Sample();
        var other = Sample("GV0002", "lan@example.edu.vn") with { Status = "Tạm nghỉ" };
        var input = new[] { original, other };
        Assert.Equal(2, LecturerQuery.Filter(input, "", "", "").Count);
        Assert.Single(LecturerQuery.Filter(input, "", "Công nghệ thông tin", "Tạm nghỉ"));
        Assert.Empty(LecturerQuery.Filter(input, "", "Kinh tế", ""));
        Assert.Equal(2, input.Length);
        Assert.Same(original, input[0]);
    }

    [Fact]
    public void SaveTrimsFieldsAndAllowsElevenDigitPhone()
    {
        var store = new LecturerStore([]);
        Assert.True(store.TrySave(Sample() with { Id = " gv0001 ", Name = " Nguyễn Đăng Minh ", Email = " minh@example.edu.vn ", Phone = "01234567890" }, true, out var errors));
        Assert.Empty(errors);
        Assert.Equal("GV0001", store.Items[0].Id);
        Assert.Equal("Nguyễn Đăng Minh", store.Items[0].Name);
    }
}
