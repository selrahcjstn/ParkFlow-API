namespace ParkFlow.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByUserProfileIdAsync(Guid userProfileId);
    Task AddAsync(Student student);
}
