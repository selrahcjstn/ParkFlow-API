using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

public record RegisterUserAggregateCommand(
    AccountDto Account,
    ProfileDto Profile,
    StudentDto? Student,
    PersonnelDto? Personnel,
    CorDto? CorSubmission,
    List<ParkingScheduleItemDto>? ParkingSchedules,
    List<VehicleDto>? Vehicles
) : IRequest<Result<RegisterResultDto>>;

public record AccountDto(string Email, string Password, string PhoneNumber);

public record ProfileDto(string FirstName, string LastName, string? ProfilePictureUrl);

public record CorDto(string AcademicTerm, string CorDocumentUrl);

public record ParkingScheduleItemDto(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime);

public record VehicleDto(string PlateNumber, string Brand, VehicleType VehicleType);

public record VehicleResultDto(Guid Id, string PlateNumber, string Brand, string QrCodeHash, VehicleType VehicleType);

public record RegisterResultDto(Guid UserId, Guid? SubmissionId, List<VehicleResultDto>? Vehicles);

public record StudentDto(
    string StudentNumber,
    string Course,
    string Section,
    int YearLevel
);

public record PersonnelDto(
    string IdCardNumber,
    string Department
);
