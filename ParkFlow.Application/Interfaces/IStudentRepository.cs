using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByUserProfileIdAsync(Guid userProfileId);
    Task<Student?> GetByStudentNumberAsync(string studentNumber);
    Task AddAsync(Student student);
    Task UpdateAsync(Student student);
}
