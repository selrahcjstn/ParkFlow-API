using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

public class CreateGuardAccountHandler : IRequestHandler<CreateGuardAccountCommand, Result<Guid>>
{
	private readonly IUserAccountRepository _userAccountRepository;
	private readonly IAuthIdentityRepository _authIdentityRepository;
	private readonly IUserProfileRepository _userProfileRepository;
	private readonly IGuardRepository _guardRepository;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IValidator<CreateGuardAccountCommand> _validator;
	private readonly IEmailService? _emailService;

	public CreateGuardAccountHandler(
		IUserAccountRepository userAccountRepository,
		IAuthIdentityRepository authIdentityRepository,
		IUserProfileRepository userProfileRepository,
		IGuardRepository guardRepository,
		IPasswordHasher passwordHasher,
		IValidator<CreateGuardAccountCommand> validator,
		IEmailService? emailService = null)
	{
		_userAccountRepository = userAccountRepository;
		_authIdentityRepository = authIdentityRepository;
		_userProfileRepository = userProfileRepository;
		_guardRepository = guardRepository;
		_passwordHasher = passwordHasher;
		_validator = validator;
		_emailService = emailService;
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

		var plainPassword = string.IsNullOrWhiteSpace(request.Account.Password)
			? PasswordGenerator.GenerateTemporaryPassword()
			: request.Account.Password;

		var hashedPassword = _passwordHasher.HashPassword(plainPassword);
		var user = new UserAccount(hashedPassword, request.Account.PhoneNumber);
		user.Verify(); // Guard accounts are verified upon provision
		user.UpdateOnboardingStep(OnboardingStep.Done); // Guards bypass onboarding steps
		user.PasswordHistories.Add(new PasswordHistory(user.Id, hashedPassword));
		await _userAccountRepository.AddAsync(user);

		var identity = AuthIdentity.CreateManual(user.Id, request.Account.Email, hashedPassword, isPrimary: true);
		await _authIdentityRepository.AddAsync(identity);

		var userProfile = new UserProfile(
			user.Id,
			request.Profile.FirstName,
			request.Profile.LastName,
			request.Profile.MiddleName,
			request.Profile.ProfilePictureUrl);

		await _userProfileRepository.AddAsync(userProfile);

		var guard = new Guard(userProfile, request.AssignedGate);
		await _guardRepository.AddAsync(guard);

		if (_emailService != null)
		{
			try
			{
				var emailSubject = "Welcome to ParkFlow - Security Guard Account Credentials";
				var emailBody = $@"Hello {request.Profile.FirstName}, your ParkFlow Security Guard account has been created.\nEmail: {request.Account.Email}\nTemporary Password: {plainPassword}";
				await _emailService.SendEmailAsync(request.Account.Email, emailSubject, emailBody);
			}
			catch
			{
			}
		}

		return Result<Guid>.Success(user.Id, "Guard account created successfully.");
	}
}
