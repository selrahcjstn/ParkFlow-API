using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;

public class UpdateOnboardingStudentHandler : IRequestHandler<UpdateOnboardingStudentCommand, Result<Guid>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<UpdateOnboardingStudentCommand> _validator;

    public UpdateOnboardingStudentHandler(
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingStudentCommand> validator)
    {
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingStudentCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (profile == null)
            return Result<Guid>.Failure("User profile not found.", ErrorCode.NotFound);

        var existingStudent = await _studentRepository.GetByUserProfileIdAsync(profile.Id);
        if (existingStudent == null)
        {
            var student = new Student(profile, request.StudentNumber, request.Course, request.Section, request.YearLevel);
            await _studentRepository.AddAsync(student);
            existingStudent = student;
        }
        else
        {
            existingStudent.UpdateDetails(request.StudentNumber, request.Course, request.Section, request.YearLevel);
            await _studentRepository.UpdateAsync(existingStudent);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Vehicle);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<Guid>.Success(existingStudent.UserProfileId, "Student onboarding completed.");
    }
}
