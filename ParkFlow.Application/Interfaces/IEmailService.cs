using System.Threading.Tasks;

namespace ParkFlow.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
}
