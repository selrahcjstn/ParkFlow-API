using System.Text.RegularExpressions;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Onboarding.Queries.CheckRegistrationId;

public class CheckRegistrationIdHandler(
    IUserProfileRepository profiles,
    IStudentRepository students,
    IPersonnelRepository personnel) : IRequestHandler<CheckRegistrationIdQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(CheckRegistrationIdQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            return Result<bool>.Failure(false, "Please sign in to continue registration.", ErrorCode.Unauthorized);

        var number = request.Number?.Trim() ?? "";
        var student = string.Equals(request.Role, "Student", StringComparison.OrdinalIgnoreCase);
        var staff = string.Equals(request.Role, "UniversityStaff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Role, "NonAcademicPersonnel", StringComparison.OrdinalIgnoreCase);
        if (!student && !staff)
            return Result<bool>.Failure(false, "Please select Student, Faculty, or University Staff.", ErrorCode.BadRequest);
        if (student && !Regex.IsMatch(number, @"^[0-9]{10}$"))
            return Result<bool>.Failure(false, "Student ID number must contain exactly 10 digits (e.g. 2023106763).", ErrorCode.BadRequest);
        if (staff && (string.IsNullOrWhiteSpace(number) || number.Length > 50))
            return Result<bool>.Failure(false, "Please enter a valid employee ID number.", ErrorCode.BadRequest);

        var profile = await profiles.GetByUserIdAsync(request.UserId);
        // A saved ID can be resumed by its owner; it cannot be claimed by another account.
        var exists = student
            ? await students.StudentNumberExistsAsync(number, profile?.Id)
            : await personnel.IdCardNumberExistsAsync(number, profile?.Id);
        if (exists)
            return Result<bool>.Failure(false, $"This {(student ? "student" : "employee")} ID number is already registered. Please check your ID number or contact campus administration.", ErrorCode.Conflict);
        return Result<bool>.Success(true, "ID number is available.");
    }
}
