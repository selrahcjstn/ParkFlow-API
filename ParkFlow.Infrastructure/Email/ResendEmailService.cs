using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Interfaces;
using Resend;
using System;
using System.Threading.Tasks;

namespace ParkFlow.Infrastructure.Email;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _fromEmail;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(IResend resend, IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _resend = resend;
        _logger = logger;
        var from = configuration.GetSection("Resend")["FromEmail"] 
                   ?? configuration.GetSection("Resend")["From"] 
                   ?? "onboarding@resend.dev";
        _fromEmail = from.Trim('<', '>');
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            _logger.LogInformation("Sending email via Resend to {To} with subject '{Subject}' from '{From}'", to, subject, _fromEmail);

            var message = new EmailMessage
            {
                From = _fromEmail,
                Subject = subject,
                HtmlBody = htmlBody
            };
            message.To.Add(to);

            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Email successfully sent via Resend to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Resend to {To}. Error: {Message}", to, ex.Message);
            throw;
        }
    }
}
