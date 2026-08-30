using MediatR;
using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using System;

namespace ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;

public record UploadMotorPictureCommand(
    IFormFile File,
    Guid? CorSubmissionId = null
) : IRequest<Result<UploadFileResponse>>;
