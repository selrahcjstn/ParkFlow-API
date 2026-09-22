using System;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Persistence.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public StudentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Student student)
    {
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Student student)
    {
        var entry = _context.Entry(student);
        if (entry.State == EntityState.Detached)
        {
            student.UserProfile = null!;
            _context.Students.Update(student);
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Student student)
    {
        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
    }

    public async Task<Student?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Students
            .Include(s => s.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }

    public async Task<Student?> GetByStudentNumberAsync(string studentNumber)
    {
        return await _context.Students
            .Include(s => s.UserProfile)
            .FirstOrDefaultAsync(x => x.StudentNumber == studentNumber);
    }
}
