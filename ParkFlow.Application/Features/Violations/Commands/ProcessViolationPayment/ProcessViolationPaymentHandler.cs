using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Violations.Commands.ProcessViolationPayment;

public class ProcessViolationPaymentHandler : IRequestHandler<ProcessViolationPaymentCommand, Result<ViolationPaymentReceiptDto>>
{
    private readonly IViolationRepository _violationRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IValidator<ProcessViolationPaymentCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly INotificationService? _notificationService;

    private readonly IAdminRepository? _adminRepository;

    private readonly ICacheService? _cacheService;

    public ProcessViolationPaymentHandler(
        IViolationRepository violationRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IParkingLogRepository parkingLogRepository,
        IValidator<ProcessViolationPaymentCommand> validator,
        ISignalRNotificationSender notificationSender,
        INotificationService? notificationService = null,
        IAdminRepository? adminRepository = null,
        ICacheService? cacheService = null)
    {
        _violationRepository = violationRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _parkingLogRepository = parkingLogRepository;
        _validator = validator;
        _notificationSender = notificationSender;
        _notificationService = notificationService;
        _adminRepository = adminRepository;
        _cacheService = cacheService;
    }

    public Task<Result<ViolationPaymentReceiptDto>> Handle(ProcessViolationPaymentCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result<ViolationPaymentReceiptDto>.Failure("Parking fees have been removed. Historical payment records are read-only.", ErrorCode.Forbidden));
}
