using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

public class RegistrationWriter(AppDbContext db) : IRegistrationWriter
{
    public async Task<Result<UserAccount>> SaveAsync(CompleteRegistration r, string passwordHash, string qrHash, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                // The same session ID makes retries safe even if the commit response is lost.
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var d = r.Details;
                if (d.Role == Roles.Student && r.ExtractedFields != null)
                {
                    var termErrors = CorAcademicTerm.Validate(r.ExtractedFields, d.AcademicYear, d.Semester);
                    if (termErrors.Count > 0) return Result<UserAccount>.Failure(string.Join(" ", termErrors), ErrorCode.BadRequest);
                }
                var identifier = d.Role == Roles.Student ? d.StudentNumber : Personnel.NormalizeId(d.IdCardNumber);
                foreach (var key in new[] { "registration:" + r.RegistrationId, "email:" + d.Email, "id:" + identifier, "plate:" + d.PlateNumber }.Order())
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                var saved = await db.UserAccounts.Include(u => u.AuthIdentities).Include(u => u.UserProfile!).ThenInclude(p => p.Student)
                    .FirstOrDefaultAsync(u => u.Id == r.RegistrationId, ct);
                if (saved != null)
                {
                    if (!saved.AuthIdentities.Any(i => i.Email == r.VerifiedEmail)) return Result<UserAccount>.Failure("Registration session is invalid.", ErrorCode.Conflict);
                    await transaction.CommitAsync(ct);
                    return Result<UserAccount>.Success(saved);
                }
                if (await db.AuthIdentities.AnyAsync(i => i.Email != null && i.Email.ToLower() == d.Email, ct))
                    return Result<UserAccount>.Failure("This email is already registered.", ErrorCode.Conflict);
                if (await db.Vehicles.AnyAsync(v => v.PlateNumber.ToUpper() == d.PlateNumber, ct))
                    return Result<UserAccount>.Failure("This plate number is already registered.", ErrorCode.Conflict);
                if (d.Role == Roles.Student ? await db.Students.AnyAsync(s => s.StudentNumber == identifier, ct) :
                    await db.Personnel.AnyAsync(p => p.IdCardNumber == identifier, ct))
                    return Result<UserAccount>.Failure("This student or employee ID number is already registered.", ErrorCode.Conflict);
                var account = new UserAccount(passwordHash, d.PhoneNumber) { Id = r.RegistrationId };
                account.UpdateOnboardingStep(OnboardingStep.Done);
                var identity = AuthIdentity.CreateManual(account.Id, d.Email, passwordHash, isPrimary: true);
                identity.MarkVerified(); account.AuthIdentities.Add(identity);
                account.PasswordHistories.Add(new PasswordHistory(account.Id, passwordHash));
                var profile = new UserProfile(account.Id, d.FirstName, d.LastName, d.MiddleName, r.ProfilePhotoUrl);
                profile.SubmitLicense(r.LicenseUrl);
                account.UserProfile = profile;
                if (d.Role == Roles.Student) profile.Student = new Student(profile.Id, identifier, d.Course?.Trim(), d.Section, d.YearLevel);
                else profile.Personnel = new Personnel(profile.Id, identifier, d.Department.Trim(), d.Role);
                var vehicle = new Vehicle(account.Id, d.PlateNumber, d.Brand, qrHash, d.VehicleType, r.OrcrUrl, r.MotorUrl);
                vehicle.SetPrimary(true);
                var cor = new CorSubmission(account.Id, d.Role == Roles.Student ? d.AcademicYear : "Employee ID", r.CorUrl, r.OrcrUrl, r.MotorUrl,
                    d.Role == Roles.Student ? CorVerificationStatus.Verified : CorVerificationStatus.Pending);
                db.UserAccounts.Add(account); db.Vehicles.Add(vehicle); db.CorSubmissions.Add(cor);
                if (d.Role == Roles.Student)
                {
                    cor.UpdateScheduleVerification(CorVerificationStatus.Verified);
                    db.ParkingSchedules.AddRange(CorStudentMapping.ConfirmableSchedules(r.ExtractedFields!).Select(s => new ParkingSchedule(cor.Id, s.Day, s.Start, s.End)));
                }
                await db.SaveChangesAsync(ct);
                var store = new VerificationStore(db);
                if (d.Role == Roles.Student)
                {
                    var source = await store.GetSubjectAsync(VerificationType.Cor, cor.Id, ct);
                    var information = new ConfirmedCorStudentInformation(d.FirstName, d.LastName, d.MiddleName, d.PhoneNumber,
                        identifier, d.College?.Trim(), d.Course?.Trim(), d.YearLevel, d.Section, d.AcademicYear, d.Semester);
                    db.VerificationRecords.Add(new VerificationRecord { UserAccountId = account.Id, SubjectId = cor.Id, Type = VerificationType.Cor,
                        Status = VerificationState.Approved, Method = VerificationMethod.Automatic, VerifiedAt = DateTime.UtcNow,
                        Fingerprint = source!.Fingerprint, OcrOutcome = "RegistrationExtractionConfirmed", Version = 1,
                        Reason = "COR information and daily schedule confirmed; no admin approval required.",
                        ExtractedFieldsJson = JsonSerializer.Serialize(r.ExtractedFields! with { SourceDocumentUrl = r.CorUrl, ConfirmedCollege = d.College,
                            ConfirmedInformation = information, ConfirmedWeek = CorStudentMapping.AuthoritativeWeek(r.ExtractedFields!) }) });
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    // Keep the existing personnel identity and document review process.
                    await store.EnqueueAsync(VerificationType.Account, account.Id, ct);
                    await store.EnqueueAsync(VerificationType.Cor, cor.Id, ct);
                }
                await transaction.CommitAsync(ct);
                return Result<UserAccount>.Success(account);
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            return Result<UserAccount>.Failure("This email or institutional ID is already registered. Check your details and try again.", ErrorCode.Conflict);
        }
    }
}
