using MediatR;
using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using System;

namespace ParkFlow.Application.Features.Files.Commands.UploadCorDocument;

public record UploadCorDocumentCommand(
    IFormFile File,
    Guid CorSubmissionId
) : IRequest<Result<UploadFileResponse>>;
