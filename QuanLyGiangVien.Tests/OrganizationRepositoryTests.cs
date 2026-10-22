using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Catalogs;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class OrganizationRepositoryTests
{
    private static OrganizationRecord Faculty => new(OrganizationKind.Faculty, "KT", "Kinh tế", null, true);
    [Theory]
    [InlineData(OrganizationKind.Faculty, null)]
    [InlineData(OrganizationKind.Major, "KT")]
    [InlineData(OrganizationKind.Class, "QTKD")]
    public void CommandsUseTypedCommonContract(OrganizationKind kind, string? parent)
    {
        using var command = OrganizationRepository.SaveCommand(Faculty with { Kind = kind, ParentCode = parent }, true);
        Assert.Equal("dbo.usp_ToChuc_Luu", command.CommandText);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        Assert.Equal((int)kind, command.Parameters["@loai"].Value);
        Assert.Equal(20, command.Parameters["@ma"].Size);
        Assert.Equal(SqlDbType.NVarChar, command.Parameters["@ten"].SqlDbType);
        Assert.Equal("Kinh tế", command.Parameters["@ten"].Value);
        Assert.Equal(DBNull.Value, command.Parameters["@phien_ban"].Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A B")]
    [InlineData("A'; DROP TABLE KHOA")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void InvalidCodeIsRejected(string code) => Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Code = code }, true));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejected(string name) => Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Name = name }, true));

    [Fact]
    public void SchemaLengthsAreValidatedBeforeConnection()
    {
        Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Name = new string('x', 101) }, true));
        Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Description = new string('x', 501) }, true));
        Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Kind = OrganizationKind.Major }, true));
        Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { ParentCode = "KT" }, true));
        Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Kind = (OrganizationKind)99 }, true));
    }

    [Theory]
    [InlineData(1899, "2024-2028")]
    [InlineData(2201, "2024-2028")]
    [InlineData(2024, "2024-2023")]
    [InlineData(2024, "2024/2028")]
    [InlineData(2024, "2024-2024")]
    public void InvalidClassCohortIsRejected(int cohort, string years) => Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(
        new(OrganizationKind.Class, "KT24A", "Kinh tế 24A", "KT", true, (short)cohort, years), true));

    [Fact]
    public void NullableClassMetadataAndSqlMetacharactersInNamesRemainData()
    {
        var value = new OrganizationRecord(OrganizationKind.Class, "KT24A", "  O'Brien | Kinh tế  ", "KT", true);
        using var command = OrganizationRepository.SaveCommand(value, true);
        Assert.Equal("O'Brien | Kinh tế", command.Parameters["@ten"].Value);
        Assert.Equal(DBNull.Value, command.Parameters["@khoa_hoc"].Value);
        Assert.Equal(DBNull.Value, command.Parameters["@nien_khoa"].Value);
        Assert.Equal(SqlDbType.SmallInt, command.Parameters["@khoa_hoc"].SqlDbType);
        Assert.Equal("  O'Brien | Kinh tế  ", value.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void ExistingWritesRequireValidVersion(string? version)
    {
        Assert.Throws<ArgumentException>(() => OrganizationRepository.SaveCommand(Faculty with { Version = version }, false));
        Assert.Throws<ArgumentException>(() => OrganizationRepository.StatusCommand(Faculty with { Version = version }, false));
    }

    [Fact]
    public void StatusCarriesOnlyIdentityActiveAndLoadedVersion()
    {
        using var command = OrganizationRepository.StatusCommand(Faculty with { Version = new string('A', 64) }, false);
        Assert.Equal("dbo.usp_ToChuc_TrangThai", command.CommandText);
        Assert.Equal(4, command.Parameters.Count);
        Assert.Equal(false, command.Parameters["@trang_thai"].Value);
        Assert.Equal(32, ((byte[])command.Parameters["@phien_ban"].Value).Length);
    }

    [Fact]
    public void MappingPreservesOptionalDataAndCopiesVersion()
    {
        using var table = new DataTable();
        foreach (var field in new[] { "code", "name", "parent_code", "description", "academic_years" }) table.Columns.Add(field, typeof(string));
        table.Columns.Add("active", typeof(bool)); table.Columns.Add("cohort", typeof(short)); table.Columns.Add("version", typeof(byte[]));
        var version = Enumerable.Repeat((byte)0xAB, 32).ToArray();
        table.Rows.Add("KT", "Kinh tế", DBNull.Value, DBNull.Value, DBNull.Value, false, DBNull.Value, version);
        var record = OrganizationRepository.FromRow(OrganizationKind.Faculty, table.Rows[0]); version[0] = 0;
        Assert.Equal(Faculty with { Active = false, Version = string.Concat(Enumerable.Repeat("AB", 32)) }, record);
    }
}

[Collection("Shared SQL fixture")]
public sealed class OrganizationRepositoryIntegrationTests
{
    [SqlIntegrationFact]
    public async Task PdtWithoutProfileEditCanListButCannotCreate()
    {
        var cs = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog, StringComparison.Ordinal);
        await using var cn = new SqlConnection(cs); await cn.OpenAsync(); using var tx = cn.BeginTransaction();
        try
        {
            using var restrict = new SqlCommand("UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL WHERE ten_dang_nhap=ORIGINAL_LOGIN(); DELETE dbo.CAMPUS_PERMISSION WHERE account_id=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()) AND permission='PROFILE_EDIT'", cn, tx);
            await restrict.ExecuteNonQueryAsync();
            using var list = new SqlCommand("dbo.usp_ToChuc_DanhSach", cn, tx) { CommandType = CommandType.StoredProcedure };
            list.Parameters.Add("@loai", SqlDbType.Int).Value = 0;
            using (var reader = await list.ExecuteReaderAsync()) Assert.True(await reader.ReadAsync());
            using var denied = OrganizationRepository.SaveCommand(new(OrganizationKind.Faculty, "DENIED_TEST", "Khoa thử", null, true), true);
            denied.Connection = cn; denied.Transaction = tx;
            Assert.Equal(52801, (await Assert.ThrowsAsync<SqlException>(() => denied.ExecuteNonQueryAsync())).Number);
        }
        finally { if (tx.Connection is not null) await tx.RollbackAsync(); }
    }

    [SqlIntegrationFact]
    public async Task StudentLinksPreventReparentingAndIncompleteFacultiesRemainVisible()
    {
        var cs = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog, StringComparison.Ordinal);
        var repo = new OrganizationRepository(cs); string suffix = Guid.NewGuid().ToString("N")[..8];
        string facultyA = "FA_" + suffix, facultyB = "FB_" + suffix, majorA = "NA_" + suffix, majorB = "NB_" + suffix,
            classA = "LA_" + suffix, studentCode = "SV_" + suffix;
        try
        {
            await repo.SaveAsync(new(OrganizationKind.Faculty, facultyA, "Khoa A", null, true), true);
            await repo.SaveAsync(new(OrganizationKind.Faculty, facultyB, "Khoa B", null, true), true);
            using (var choices = await new StudentRepository(cs).ChoicesAsync())
            {
                var empty = Assert.Single(choices.Rows.Cast<DataRow>(), r => Convert.ToString(r["ma_khoa"]) == facultyB);
                Assert.True(empty.IsNull("ma_nganh")); Assert.True(empty.IsNull("ma_lop"));
                Assert.True(Convert.ToBoolean(empty["khoa_hoat_dong"])); Assert.False(Convert.ToBoolean(empty["hoat_dong"]));
            }
            var major = new OrganizationRecord(OrganizationKind.Major, majorA, "Ngành A", facultyA, true);
            await repo.SaveAsync(major, true); major = await Read(repo, major);
            await repo.SaveAsync(new(OrganizationKind.Major, majorB, "Ngành B", facultyB, true), true);
            var item = new OrganizationRecord(OrganizationKind.Class, classA, "Lớp A", majorA, true);
            await repo.SaveAsync(item, true); item = await Read(repo, item);
            await new StudentRepository(cs).SaveAsync(new(studentCode, "Sinh viên kiểm thử", null, null, null, null, null, majorA, classA, true), true);
            var classError = await Assert.ThrowsAsync<PersistenceException>(() => repo.SaveAsync(item with { ParentCode = majorB }, false));
            var majorError = await Assert.ThrowsAsync<PersistenceException>(() => repo.SaveAsync(major with { ParentCode = facultyB }, false));
            Assert.Contains("không thể chuyển", classError.Message); Assert.Contains("không thể chuyển", majorError.Message);
            Assert.Equal(majorA, (await Read(repo, item)).ParentCode);
            Assert.Equal(facultyA, (await Read(repo, major)).ParentCode);
            await repo.SetStatusAsync(item, false);
            using var stopped = await new StudentRepository(cs).ChoicesAsync();
            var row = Assert.Single(stopped.Rows.Cast<DataRow>(), r => Convert.ToString(r["ma_lop"]) == classA);
            Assert.False(Convert.ToBoolean(row["hoat_dong"])); Assert.False(Convert.ToBoolean(row["lop_hoat_dong"]));
        }
        finally
        {
            await using var cn = new SqlConnection(cs); await cn.OpenAsync();
            using var cleanup = new SqlCommand("DELETE dbo.SINH_VIEN WHERE ma_sv=@sv; DELETE dbo.LOP WHERE ma_lop=@lop; DELETE dbo.NGANH WHERE ma_nganh IN(@na,@nb); DELETE dbo.KHOA WHERE ma_khoa IN(@fa,@fb)", cn);
            foreach (var (key, value) in new[] { ("@sv",studentCode),("@lop",classA),("@na",majorA),("@nb",majorB),("@fa",facultyA),("@fb",facultyB) })
                cleanup.Parameters.Add(key, SqlDbType.VarChar, 20).Value = value;
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [SqlIntegrationFact]
    public async Task SqlBoundaryRejectsOversizedInputAndStudentRoleWithoutChangingData()
    {
        var cs = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(cs).InitialCatalog, StringComparison.Ordinal);
        await using var cn = new SqlConnection(cs); await cn.OpenAsync();
        using (var invalid = OrganizationRepository.SaveCommand(new(OrganizationKind.Faculty, "BOUNDS_TEST", "Khoa thử", null, true), true))
        {
            invalid.Connection = cn; invalid.Parameters["@ma"].Size = 128; invalid.Parameters["@ma"].Value = new string('A',21);
            Assert.Equal(53400, (await Assert.ThrowsAsync<SqlException>(() => invalid.ExecuteNonQueryAsync())).Number);
        }
        using var tx = cn.BeginTransaction();
        try
        {
            using var role = new SqlCommand("UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv=(SELECT TOP(1) ma_sv FROM dbo.SINH_VIEN ORDER BY ma_sv) WHERE ten_dang_nhap=ORIGINAL_LOGIN()", cn, tx);
            Assert.Equal(1, await role.ExecuteNonQueryAsync());
            using var forbidden = OrganizationRepository.SaveCommand(new(OrganizationKind.Faculty, "FORBIDDEN_TEST", "Khoa thử", null, true), true);
            forbidden.Connection = cn; forbidden.Transaction = tx;
            Assert.Equal(52001, (await Assert.ThrowsAsync<SqlException>(() => forbidden.ExecuteNonQueryAsync())).Number);
        }
        finally { if (tx.Connection is not null) await tx.RollbackAsync(); }
    }

    [SqlIntegrationFact]
    public async Task IndependentListsCrudParentIntegrityAndStaleStatus()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINH_DB_TEST_CONNECTION")!;
        Assert.StartsWith("DangKyTinChi_Minh_Test_", new SqlConnectionStringBuilder(connectionString).InitialCatalog, StringComparison.Ordinal);
        var repo = new OrganizationRepository(connectionString);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var faculty = new OrganizationRecord(OrganizationKind.Faculty, "KF_" + suffix, "Khoa kiểm thử", null, true);
        var major = new OrganizationRecord(OrganizationKind.Major, "KN_" + suffix, "Ngành kiểm thử", faculty.Code, true);
        var item = new OrganizationRecord(OrganizationKind.Class, "KL_" + suffix, "Lớp kiểm thử", major.Code, true, 2026, "2026-2030");
        try
        {
            await repo.SaveAsync(faculty, true);
            faculty = await Read(repo, faculty);
            Assert.Equal(64, faculty.Version!.Length); // A faculty without children must still be listed.
            await repo.SaveAsync(major, true); major = await Read(repo, major);
            await repo.SaveAsync(item, true); item = await Read(repo, item);
            await repo.SetStatusAsync(faculty, false);
            var stale = await Assert.ThrowsAsync<PersistenceException>(() => repo.SaveAsync(faculty with { Name = "Ghi đè" }, false));
            Assert.Equal(PersistenceFailure.DataConflict, stale.Kind);
            Assert.False((await Read(repo, faculty)).Active);
            await Assert.ThrowsAsync<PersistenceException>(() => repo.SaveAsync(major with { Code = "KN2_" + suffix }, true));
            await repo.SaveAsync(major with { Name = "Đã sửa tên" }, false); // Preserve an existing inactive parent.
            await repo.SetStatusAsync(await Read(repo, faculty), true);
            await repo.SaveAsync(item with { Name = "Đã sửa lớp", Cohort = null, AcademicYears = null }, false);
            Assert.Null((await Read(repo, item)).Cohort);
            var duplicate = await Assert.ThrowsAsync<PersistenceException>(() => repo.SaveAsync(faculty, true));
            Assert.Contains("đã tồn tại", duplicate.Message);
        }
        finally
        {
            await using var cn = new SqlConnection(connectionString); await cn.OpenAsync();
            using var cleanup = new SqlCommand("DELETE dbo.LOP WHERE ma_lop=@lop; DELETE dbo.NGANH WHERE ma_nganh=@nganh; DELETE dbo.KHOA WHERE ma_khoa=@khoa", cn);
            cleanup.Parameters.AddWithValue("@lop", item.Code); cleanup.Parameters.AddWithValue("@nganh", major.Code); cleanup.Parameters.AddWithValue("@khoa", faculty.Code);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task<OrganizationRecord> Read(OrganizationRepository repo, OrganizationRecord item)
    {
        using var table = await repo.ListAsync(item.Kind, item.Code);
        return OrganizationRepository.FromRow(item.Kind, Assert.Single(table.Rows.Cast<DataRow>()));
    }
}
