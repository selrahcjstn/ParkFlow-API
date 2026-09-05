using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.DTOs;

namespace ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;

public record RegisterManualAccountCommand(
    string Email,
    string? Password = null,
    string? FirstName = null,
    string? LastName = null,
    string? MiddleName = null,
    string? PhoneNumber = null,
    string? Role = null,
    string? Status = null,
    RegisterStudentDto? Student = null,
    RegisterPersonnelDto? Personnel = null,
    RegisterGuardDto? Guard = null,
    bool IsAdminCreated = false) : IRequest<Result<string>>;
