using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Text.RegularExpressions;

namespace ParkFlow.Application.Features.Auth.Registration;

// No password or form data is persisted until this request is submitted.
public sealed class RegistrationDetails
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public Roles Role { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string StudentNumber { get; set; } = "";
    public string? Course { get; set; }
    public string Section { get; set; } = "";
    public int YearLevel { get; set; }
    public string AcademicYear { get; set; } = "";
    public string Semester { get; set; } = "";
    public string? College { get; set; }
    public string IdCardNumber { get; set; } = "";
    public string Department { get; set; } = "";
    public string PlateNumber { get; set; } = "";
    public string Brand { get; set; } = "";
    public VehicleType VehicleType { get; set; }
}

public record CompleteRegistration(Guid RegistrationId, RegistrationDetails Details, string VerifiedEmail,
    string CorUrl, string OrcrUrl, string MotorUrl, CorFields? ExtractedFields, string LicenseUrl = "", string ProfilePhotoUrl = "") : IRequest<Result<string>>;

public class CompleteRegistrationHandler(IRegistrationWriter writer, IPasswordHasher passwords, IJwtService jwt,
    IQrCodeService qr, ISignalRNotificationSender signalR, ICacheService cache) : IRequestHandler<CompleteRegistration, Result<string>>
{
    public async Task<Result<string>> Handle(CompleteRegistration request, CancellationToken ct)
    {
        var d = request.Details;
        var errors = await ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<string>.Failure(string.Join(" ", errors.Distinct()), ErrorCode.BadRequest);
        d.Email = d.Email.Trim().ToLowerInvariant();
        d.FirstName = d.FirstName.Trim(); d.LastName = d.LastName.Trim();
        d.MiddleName = string.IsNullOrWhiteSpace(d.MiddleName) ? null : d.MiddleName.Trim();
        d.PhoneNumber = d.PhoneNumber.Trim(); d.PlateNumber = d.PlateNumber.Trim().ToUpperInvariant(); d.Brand = d.Brand.Trim();
        if (d.Role == Roles.Student) { d.StudentNumber = Student.NormalizeNumber(d.StudentNumber); d.Section = CorStudentMapping.Section(d.Section)!; }
        var hash = passwords.HashPassword(d.Password);
        // Do not retain the raw password in the command after hashing it.
        d.Password = "";
        var qrHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(qr.GenerateQrCode($"{request.RegistrationId}:{d.PlateNumber}:{d.Brand}")));
        var result = await writer.SaveAsync(request, hash, qrHash, ct);
        if (!result.IsSuccess || result.Data == null) return Result<string>.Failure(result.Message, result.ErrorCode);
        var token = jwt.GenerateToken(result.Data, result.Data.UserProfile?.Student != null ? "student" : "personnel", "pwd");
        await cache.RemoveByPrefixAsync(CacheKeys.UserPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(CacheKeys.VehiclePrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(CacheKeys.SchedulePrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, CancellationToken.None);
        // Realtime consumers only see committed registrations.
        try { await signalR.SendToAllAsync("ApprovalListUpdated", new { type = "Vehicle", userId = result.Data.Id }); }
        catch { /* The committed registration is available by normal API reads. */ }
        return Result<string>.Success(token, "Registration completed successfully.");
    }

    public static async Task<List<string>> ValidateAsync(CompleteRegistration request, CancellationToken ct)
    {
        var d = request.Details;
        var errors = new List<string>();
        // JSON clients can send null despite the non-null form defaults.
        if (d.Email == null || d.Password == null || d.FirstName == null || d.LastName == null || d.PhoneNumber == null ||
            d.StudentNumber == null || d.Section == null || d.AcademicYear == null || d.Semester == null ||
            d.IdCardNumber == null || d.Department == null || d.PlateNumber == null || d.Brand == null)
            return ["Complete all required registration fields."];
        async Task Check<T>(IValidator<T> validator, T value) => errors.AddRange((await validator.ValidateAsync(value, ct)).Errors.Select(e => e.ErrorMessage));
        await Check(new RegisterManualAccountValidator(), new RegisterManualAccountCommand(d.Email, d.Password, d.FirstName, d.LastName, d.MiddleName, d.PhoneNumber));
        await Check(new UpdateOnboardingProfileValidator(), new UpdateOnboardingProfileCommand(request.RegistrationId, d.PhoneNumber, d.FirstName, d.LastName, d.MiddleName, null));
        await Check(new UpdateOnboardingVehicleValidator(), new UpdateOnboardingVehicleCommand(request.RegistrationId, d.PlateNumber, d.Brand, d.VehicleType));
        if (!d.Email.Trim().Equals(request.VerifiedEmail, StringComparison.OrdinalIgnoreCase)) errors.Add("Verify the registration email before submitting.");
        if (d.Role is not (Roles.Student or Roles.UniversityStaff or Roles.NonAcademicPersonnel)) errors.Add("Select a valid registration role.");
        if (!Regex.IsMatch(d.PhoneNumber, @"^09\d{9}$")) errors.Add("Enter an 11-digit phone number starting with 09.");
        if (!Regex.IsMatch(d.PlateNumber.Trim(), @"^[A-Za-z0-9-]{4,12}$") || d.Brand.Trim().Length is < 2 or > 30) errors.Add("Check your vehicle plate number and brand.");
        if (d.FirstName.Length > 100 || d.LastName.Length > 100 || d.MiddleName?.Length > 100 ||
            !Regex.IsMatch(d.FirstName, @"^[\p{L} .'-]+$") || !Regex.IsMatch(d.LastName, @"^[\p{L} .'-]+$") ||
            !string.IsNullOrEmpty(d.MiddleName) && !Regex.IsMatch(d.MiddleName, @"^[\p{L} .'-]+$")) errors.Add("Check your name format and length.");
        if (d.Role == Roles.Student)
        {
            await Check(new UpdateOnboardingStudentValidator(), new UpdateOnboardingStudentCommand(request.RegistrationId, d.StudentNumber, d.Course, d.Section, d.YearLevel));
            if (d.Course?.Length > 100 || d.Section.Length > 50 || d.College?.Length > 100) errors.Add("Check your course, section and college lengths.");
            if (!Regex.IsMatch(d.AcademicYear, @"^\d{4}-\d{4}$") ||
                !int.TryParse(d.AcademicYear.Split('-').FirstOrDefault(), out var year) ||
                !int.TryParse(d.AcademicYear.Split('-').LastOrDefault(), out var end) || end != year + 1 ||
                CorTextVerification.SemesterKey(d.Semester) is not ("1" or "2" or "SUMMER")) errors.Add("Complete the academic year and semester from your COR.");
            if (request.ExtractedFields == null) errors.Add("Upload your COR and fetch its information before submitting.");
            else try
                {
                    errors.AddRange(CorAcademicTerm.Validate(request.ExtractedFields, d.AcademicYear, d.Semester));
                    CorStudentMapping.ConfirmableSchedules(request.ExtractedFields);
                }
                catch (DocumentValidationException ex) { errors.Add(ex.Message); }
        }
        else
        {
            await Check(new UpdateOnboardingPersonnelValidator(), new UpdateOnboardingPersonnelCommand(request.RegistrationId, d.IdCardNumber, d.Department, d.Role.ToString()));
            if (d.Department.Length > 100) errors.Add("Department must not exceed 100 characters.");
        }
        if (string.IsNullOrWhiteSpace(request.CorUrl) || string.IsNullOrWhiteSpace(request.OrcrUrl) || string.IsNullOrWhiteSpace(request.MotorUrl)) errors.Add("All registration documents are required.");
        if (string.IsNullOrWhiteSpace(request.LicenseUrl) || string.IsNullOrWhiteSpace(request.ProfilePhotoUrl)) errors.Add("Your driver’s license and profile photo are required.");
        return errors;
    }
}
