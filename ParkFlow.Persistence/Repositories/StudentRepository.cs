using System;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Application.Common;
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
        await SaveIdChangesAsync();
    }

    public async Task UpdateAsync(Student student)
    {
        var entry = _context.Entry(student);
        if (entry.State == EntityState.Detached)
        {
            student.UserProfile = null!;
            _context.Students.Update(student);
        }
        await SaveIdChangesAsync();
    }

    public async Task DeleteAsync(Student student)
    {
        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
    }

    private async Task SaveIdChangesAsync()
    {
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Students_StudentNumber" })
        {
            throw new RegistrationIdConflictException("This student ID number is already registered. Please check your ID number or contact campus administration.", ex);
        }
    }

    public async Task<Student?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Students
            .Include(s => s.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }

    public async Task<Student?> GetByStudentNumberAsync(string studentNumber, Guid? excludeProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(studentNumber)) return null;
        var normalized = Student.NormalizeNumber(studentNumber);
        // Exclude the current owner before selecting a match, including legacy formatted IDs.
        return await _context.Students
            .Where(x => excludeProfileId == null || x.UserProfileId != excludeProfileId)
            .Include(s => s.UserProfile)
            .FirstOrDefaultAsync(x => x.StudentNumber.Trim().Replace("-", "").Replace(" ", "").ToUpper() == normalized);
    }
}
