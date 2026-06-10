using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount;

public class CreateAdminAccountHandler : IRequestHandler<CreateAdminAccountCommand, Result<Guid>>
{
	private readonly IUserAccountRepository _userAccountRepository;
	private readonly IAuthIdentityRepository _authIdentityRepository;
	private readonly IUserProfileRepository _userProfileRepository;
	private readonly IAdminRepository _adminRepository;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IValidator<CreateAdminAccountCommand> _validator;
	private readonly IConfiguration _configuration;

	public CreateAdminAccountHandler(
		IUserAccountRepository userAccountRepository,
		IAuthIdentityRepository authIdentityRepository,
		IUserProfileRepository userProfileRepository,
		IAdminRepository adminRepository,
		IPasswordHasher passwordHasher,
		IValidator<CreateAdminAccountCommand> validator,
		IConfiguration configuration)
	{
		_userAccountRepository = userAccountRepository;
		_authIdentityRepository = authIdentityRepository;
		_userProfileRepository = userProfileRepository;
		_adminRepository = adminRepository;
		_passwordHasher = passwordHasher;
		_validator = validator;
		_configuration = configuration;
	}

	public async Task<Result<Guid>> Handle(CreateAdminAccountCommand request, CancellationToken cancellationToken)
	{
		var validationResult = await _validator.ValidateAsync(request, cancellationToken);

		if (!validationResult.IsValid)
		{
			var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
			return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
		}

		// Security check: Dual-Gate Verification
		bool isAuthorized = false;

		// 1. Bootstrap Registration Key Verification
		var expectedKey = _configuration["AdminSettings:RegistrationKey"];
		if (!string.IsNullOrWhiteSpace(expectedKey) && request.RegistrationKey == expectedKey)
		{
			isAuthorized = true;
		}

		// 2. Role-Based Verification (Caller must be an existing SuperAdmin)
		if (!isAuthorized && request.CurrentUserId.HasValue && request.CurrentUserId.Value != Guid.Empty)
		{
			var callingAdmin = await _adminRepository.GetByUserProfileIdAsync(request.CurrentUserId.Value);
			if (callingAdmin != null && callingAdmin.RoleLevel == RoleLevel.SuperAdmin)
			{
				isAuthorized = true;
			}
		}

		if (!isAuthorized)
		{
			return Result<Guid>.Failure("Unauthorized to register admin. A valid bootstrap key or SuperAdmin privileges are required.", ErrorCode.Forbidden);
		}

		var existingUser = await _userAccountRepository.GetByEmailAsync(request.Account.Email);
		if (existingUser != null)
			return Result<Guid>.Failure("User account with this email already exists.", ErrorCode.Conflict);

		var hashedPassword = _passwordHasher.HashPassword(request.Account.Password);
		var user = new UserAccount(hashedPassword, request.Account.PhoneNumber);
		user.UpdateOnboardingStep(OnboardingStep.Done); // Admins bypass onboarding steps
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

		var existingAdmin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
		if (existingAdmin != null)
			return Result<Guid>.Failure("Admin already exists for this profile.", ErrorCode.Conflict);

		var admin = new Admin(userProfile, request.RoleLevel);
		await _adminRepository.AddAsync(admin);

		return Result<Guid>.Success(user.Id, "Admin account created successfully.");
	}
}
