using QuanLyGiangVien.Domain.Scheduling;

namespace QuanLyGiangVien.Domain;

public sealed class PersistenceException(string message) : Exception(message);

public interface ILecturerPersistence
{
    void SaveLecturer(Lecturer value, bool isNew);
    void DeleteLecturer(string id);
}

public interface ISchedulePersistence
{
    void SaveRoom(Room value, bool isNew);
    void DeleteRoom(string id);
    int SaveClass(CourseClass value, bool isNew);
    void DeleteClass(string id);
}
