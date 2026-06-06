using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.SendEmailOtp;

public class SendEmailOtpCommandHandler : IRequestHandler<SendEmailOtpCommand, Result<bool>>
{
    private readonly IEmailOtpRepository _emailOtpRepository;
    private readonly IEmailService _emailService;
    private readonly IValidator<SendEmailOtpCommand> _validator;

    public SendEmailOtpCommandHandler(
        IEmailOtpRepository emailOtpRepository,
        IEmailService emailService,
        IValidator<SendEmailOtpCommand> validator)
    {
        _emailOtpRepository = emailOtpRepository;
        _emailService = emailService;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(SendEmailOtpCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        try
        {
            // Generate a random 6-digit OTP code
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Set expiration to 5 minutes from creation
            var expiresAt = DateTime.UtcNow.AddMinutes(5);

            // Save the OTP in the EmailOtps table
            var emailOtp = new EmailOtp(request.Email, otpCode, expiresAt);
            await _emailOtpRepository.AddAsync(emailOtp);

            // Send the OTP via email
            var subject = "Your ParkFlow OTP Verification Code";
            var htmlBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                    <h2>ParkFlow Email Verification</h2>
                    <p>You requested a one-time password (OTP) to verify your email address.</p>
                    <div style='background-color: #f4f4f4; padding: 15px; border-radius: 5px; font-size: 24px; font-weight: bold; letter-spacing: 2px; text-align: center; margin: 20px 0;'>
                        {otpCode}
                    </div>
                    <p>This code is valid for exactly <strong>5 minutes</strong>. If you did not request this code, please ignore this email.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                    <p style='font-size: 12px; color: #888;'>This is an automated email from ParkFlow. Please do not reply.</p>
                </div>";

            await _emailService.SendEmailAsync(request.Email, subject, htmlBody);

            return Result<bool>.Success(true, "OTP code generated and sent successfully.");
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(false, $"Failed to send OTP: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
