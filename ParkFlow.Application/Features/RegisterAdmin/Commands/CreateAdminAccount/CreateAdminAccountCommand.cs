using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;
using System;

namespace ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount;

public record CreateAdminAccountCommand(
	AccountDto Account,
	ProfileDto Profile,
	RoleLevel RoleLevel,
	string? RegistrationKey = null,
	Guid? CurrentUserId = null
) : IRequest<Result<Guid>>;

public record AccountDto(
	string Email,
	string Password,
	string PhoneNumber
);

public record ProfileDto(
	string FirstName,
	string LastName,
	string? MiddleName,
	string? ProfilePictureUrl
);
