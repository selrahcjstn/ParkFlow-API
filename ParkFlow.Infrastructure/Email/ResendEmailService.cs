using Microsoft.Extensions.Configuration;
using ParkFlow.Application.Interfaces;
using Resend;
using System.Threading.Tasks;

namespace ParkFlow.Infrastructure.Email;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _fromEmail;

    public ResendEmailService(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        var from = configuration.GetSection("Resend")["FromEmail"] 
                   ?? configuration.GetSection("Resend")["From"] 
                   ?? "onboarding@resend.dev";
        _fromEmail = from.Trim('<', '>');
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        var message = new EmailMessage
        {
            From = _fromEmail,
            Subject = subject,
            HtmlBody = htmlBody
        };
        message.To.Add(to);

        await _resend.EmailSendAsync(message);
    }
}
