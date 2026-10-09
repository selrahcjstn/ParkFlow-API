using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByUserProfileIdAsync(Guid userProfileId);
    Task<Student?> GetByStudentNumberAsync(string studentNumber, Guid? excludeProfileId = null);
    Task<bool> StudentNumberExistsAsync(string studentNumber, Guid? excludeProfileId = null);
    Task AddAsync(Student student);
    Task UpdateAsync(Student student);
    Task DeleteAsync(Student student);
}
