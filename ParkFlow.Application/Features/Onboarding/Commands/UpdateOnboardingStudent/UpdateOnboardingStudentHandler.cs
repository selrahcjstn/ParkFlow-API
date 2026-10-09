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
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<UpdateOnboardingStudentCommand> _validator;
    private readonly ICacheService? _cacheService;

    public UpdateOnboardingStudentHandler(
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<UpdateOnboardingStudentCommand> validator,
        ICacheService? cacheService = null)
    {
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
        _cacheService = cacheService;
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

        if (await _studentRepository.StudentNumberExistsAsync(request.StudentNumber, profile.Id))
        {
            return Result<Guid>.Failure("This student ID number is already registered. Please check your ID number or contact campus administration.", ErrorCode.Conflict);
        }

        // Remove any conflicting personnel record if role was changed during onboarding
        var existingPersonnel = await _personnelRepository.GetByUserProfileIdAsync(profile.Id);
        if (existingPersonnel != null)
        {
            await _personnelRepository.DeleteAsync(existingPersonnel);
        }

        var existingStudent = await _studentRepository.GetByUserProfileIdAsync(profile.Id);
        if (existingStudent == null)
        {
            var student = new Student(profile.Id, request.StudentNumber, request.Course, request.Section, request.YearLevel);
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

        if (_cacheService != null)
            await _cacheService.RemoveAsync(CacheKeys.UserProfile(request.UserId), cancellationToken);

        return Result<Guid>.Success(existingStudent.UserProfileId, "Student onboarding completed.");
    }
}
