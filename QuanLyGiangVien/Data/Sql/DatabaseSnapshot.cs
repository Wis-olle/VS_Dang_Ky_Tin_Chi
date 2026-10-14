using System.Collections.Immutable;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Data.Sql;

public sealed record DatabaseIdentity(string AccountId, string? StudentId, string Role, string DisplayName, string? ClassId,
    string? MajorName = null);
public sealed record Faculty(string Id, string Name);
public sealed record DatabaseRegistration(string StudentId, string ClassId, string Status);
public sealed record DatabaseStores(LecturerStore Lecturers, ScheduleStore Scheduling);

public sealed record DatabaseSnapshot(DatabaseIdentity Identity, ImmutableArray<Faculty> Faculties,
    ImmutableArray<Lecturer> Lecturers, ImmutableArray<Course> Courses, ImmutableArray<Semester> Semesters,
    ImmutableArray<Room> Rooms, ImmutableArray<CourseClass> Classes, ImmutableArray<DatabaseRegistration> Registrations)
{
    internal SqlMinhRepository? Repository { get; init; }

    public DatabaseStores CreateStores()
    {
        var lecturers = new LecturerStore(Lecturers, Repository);
        var catalog = new TeachingCatalog(Courses, Semesters, () => lecturers.Items);
        var scheduling = new ScheduleStore(catalog, Rooms, Classes, Repository);
        return new(lecturers, scheduling);
    }
}
