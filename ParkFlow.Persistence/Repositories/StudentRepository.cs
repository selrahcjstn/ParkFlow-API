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
        if (string.IsNullOrWhiteSpace(studentNumber)) return null;
        var trimmed = studentNumber.Trim();
        var normalized = trimmed.Replace("-", "").Replace(" ", "");

        var student = await _context.Students
            .Include(s => s.UserProfile)
            .FirstOrDefaultAsync(x => x.StudentNumber == trimmed || EF.Functions.ILike(x.StudentNumber, trimmed));

        if (student == null && !string.IsNullOrEmpty(normalized))
        {
            student = await _context.Students
                .Include(s => s.UserProfile)
                .FirstOrDefaultAsync(x => x.StudentNumber.Replace("-", "").Replace(" ", "") == normalized);
        }

        return student;
    }
}
