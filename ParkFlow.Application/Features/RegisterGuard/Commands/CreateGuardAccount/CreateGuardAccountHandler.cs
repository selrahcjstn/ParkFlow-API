using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

public class CreateGuardAccountHandler : IRequestHandler<CreateGuardAccountCommand, Result<Guid>>
{
	private readonly IUserAccountRepository _userAccountRepository;
	private readonly IAuthIdentityRepository _authIdentityRepository;
	private readonly IUserProfileRepository _userProfileRepository;
	private readonly IGuardRepository _guardRepository;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IValidator<CreateGuardAccountCommand> _validator;

	public CreateGuardAccountHandler(
		IUserAccountRepository userAccountRepository,
		IAuthIdentityRepository authIdentityRepository,
		IUserProfileRepository userProfileRepository,
		IGuardRepository guardRepository,
		IPasswordHasher passwordHasher,
		IValidator<CreateGuardAccountCommand> validator)
	{
		_userAccountRepository = userAccountRepository;
		_authIdentityRepository = authIdentityRepository;
		_userProfileRepository = userProfileRepository;
		_guardRepository = guardRepository;
		_passwordHasher = passwordHasher;
		_validator = validator;
	}

	public async Task<Result<Guid>> Handle(CreateGuardAccountCommand request, CancellationToken cancellationToken)
	{
		var validationResult = await _validator.ValidateAsync(request, cancellationToken);

		if (!validationResult.IsValid)
		{
			var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
			return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
		}

		var existingUser = await _userAccountRepository.GetByEmailAsync(request.Account.Email);
		if (existingUser != null)
			return Result<Guid>.Failure("User account with this email already exists.", ErrorCode.Conflict);

		var hashedPassword = _passwordHasher.HashPassword(request.Account.Password);
		var user = new UserAccount(request.Account.Email, hashedPassword, request.Account.PhoneNumber);
		user.UpdateOnboardingStep(OnboardingStep.Done); // Guards bypass onboarding steps
		await _userAccountRepository.AddAsync(user);

		var identity = AuthIdentity.CreateManual(user.Id, request.Account.Email, hashedPassword);
		await _authIdentityRepository.AddAsync(identity);

		var userProfile = new UserProfile(
			user.Id,
			request.Profile.FirstName,
			request.Profile.LastName,
			request.Profile.ProfilePictureUrl);

		await _userProfileRepository.AddAsync(userProfile);

		var existingGuard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
		if (existingGuard != null)
			return Result<Guid>.Failure("Guard already exists for this profile.", ErrorCode.Conflict);

		var guard = new Guard(userProfile, request.AssignedGate);
		await _guardRepository.AddAsync(guard);

		return Result<Guid>.Success(user.Id, "Guard account created successfully.");
	}
}