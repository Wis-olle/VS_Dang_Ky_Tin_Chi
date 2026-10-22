using System.Collections.Immutable;
using System.Data;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data.Sql;

/// <summary>Read-only projection through the existing authenticated student-list procedure.</summary>
public sealed class TimetableDirectoryRepository
{
    private readonly Func<Task<DataTable>> loadStudents;

    public TimetableDirectoryRepository(string connectionString)
        : this(() => new StudentRepository(connectionString).ListAsync("", true)) { }

    internal TimetableDirectoryRepository(Func<Task<DataTable>> loadStudents)
    {
        ArgumentNullException.ThrowIfNull(loadStudents);
        this.loadStudents = loadStudents;
    }

    public async Task<ImmutableArray<TimetableStudent>> LoadStudentsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The existing student repository has no token overload. Await ownership of its table even
        // if canceled in flight, then dispose it before discarding the stale result.
        using var table = await loadStudents().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var fields = new[] { "ma_sv", "ho_ten", "ma_lop", "ten_lop" };
        if (fields.Any(field => !table.Columns.Contains(field))) throw InvalidDirectory();
        var students = table.Rows.Cast<DataRow>().Select(row => fields.Select(field =>
            row.IsNull(field) ? "" : Convert.ToString(row[field])!.Trim()).ToArray()).ToArray();
        if (students.Any(values => values.Any(string.IsNullOrWhiteSpace))
            || students.Select(values => values[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() != students.Length)
            throw InvalidDirectory();
        return students.Select(values => new TimetableStudent(values[0], values[1], values[2], values[3])).ToImmutableArray();
    }

    private static PersistenceException InvalidDirectory() =>
        new("Danh mục thời khóa biểu sinh viên chưa hợp lệ. Vui lòng tải lại dữ liệu.");
}
