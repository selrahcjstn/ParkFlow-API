using MediatR;
using ParkFlow.Application.Common;
using System;

namespace ParkFlow.Application.Features.Users.Commands.AdminRequestResetOtp;

public record AdminRequestResetOtpCommand(
    Guid AdminUserId,
    string TargetEmail,
    string? AdminEmailOverride = null) : IRequest<Result<string>>;
