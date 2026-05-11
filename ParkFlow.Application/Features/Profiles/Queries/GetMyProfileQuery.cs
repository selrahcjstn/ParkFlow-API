using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;

namespace ParkFlow.Application.Features.Profiles.Queries;

public record GetMyProfileQuery() : IRequest<Result<UserProfileDto>>;
