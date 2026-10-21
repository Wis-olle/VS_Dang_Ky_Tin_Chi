using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Domain;

public enum PersistenceFailure { General, SessionExpired, DataConflict, SchemaMismatch, BusinessRejected }

public sealed class PersistenceException(string message, PersistenceFailure kind = PersistenceFailure.General) : Exception(message)
{
    public PersistenceFailure Kind { get; } = kind;
}

public interface ILecturerPersistence
{
    Lecturer SaveLecturer(Lecturer value, bool isNew);
    void DeleteLecturer(string id, string version = "");
}

public interface ISchedulePersistence
{
    Room SaveRoom(Room value, bool isNew);
    void DeleteRoom(string id, string version = "");
    CourseClass SaveClass(CourseClass value, bool isNew);
    void DeleteClass(string id, string version = "");
}
