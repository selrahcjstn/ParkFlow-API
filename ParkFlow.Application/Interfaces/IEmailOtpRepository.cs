using ParkFlow.Domain.Entities;
using System.Threading.Tasks;

namespace ParkFlow.Application.Interfaces;

public interface IEmailOtpRepository
{
    Task AddAsync(EmailOtp emailOtp);
    Task<EmailOtp?> GetLatestOtpByEmailAsync(string email);
    Task UpdateAsync(EmailOtp emailOtp);
}
