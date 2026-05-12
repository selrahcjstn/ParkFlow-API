using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

public record RegisterUserAggregateCommand(
    AccountDto Account,
    ProfileDto Profile,
    CorDto? CorSubmission,
    List<ParkingScheduleItemDto>? ParkingSchedules,
    List<VehicleDto>? Vehicles
) : IRequest<Result<RegisterResultDto>>;

public record AccountDto(string Email, string Password, string PhoneNumber, Roles Role);

public record ProfileDto(string IdCardNumber, string FirstName, string LastName, string? ProfilePictureUrl, string? Course, string? Section, int? YearLevel, string? Office);

public record CorDto(string AcademicTerm, string CorDocumentUrl);

public record ParkingScheduleItemDto(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime);

public record VehicleDto(string PlateNumber, string Brand);

public record RegisterResultDto(Guid UserId, Guid? SubmissionId, List<Guid>? VehicleIds);
