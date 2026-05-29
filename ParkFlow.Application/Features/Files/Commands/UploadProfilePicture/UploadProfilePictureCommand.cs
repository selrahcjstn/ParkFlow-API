using MediatR;
using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using System;

namespace ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;

public record UploadProfilePictureCommand(
    IFormFile File,
    Guid UserId
) : IRequest<Result<UploadFileResponse>>;
