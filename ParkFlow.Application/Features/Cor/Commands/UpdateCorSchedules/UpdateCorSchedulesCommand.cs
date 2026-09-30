using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using System;
using System.Collections.Generic;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;

public record UpdateCorSchedulesCommand(
    Guid CorSubmissionId,
    List<UpdateScheduleItemDto> Schedules) : IRequest<Result<bool>>;
