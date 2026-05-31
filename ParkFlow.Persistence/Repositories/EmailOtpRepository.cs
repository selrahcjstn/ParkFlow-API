using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace ParkFlow.Persistence.Repositories;

public class EmailOtpRepository : IEmailOtpRepository
{
    private readonly AppDbContext _appDbContext;

    public EmailOtpRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task AddAsync(EmailOtp emailOtp)
    {
        await _appDbContext.EmailOtps.AddAsync(emailOtp);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task<EmailOtp?> GetLatestOtpByEmailAsync(string email)
    {
        return await _appDbContext.EmailOtps
            .Where(e => e.Email.ToLower() == email.ToLower())
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(EmailOtp emailOtp)
    {
        _appDbContext.EmailOtps.Update(emailOtp);
        await _appDbContext.SaveChangesAsync();
    }
}
