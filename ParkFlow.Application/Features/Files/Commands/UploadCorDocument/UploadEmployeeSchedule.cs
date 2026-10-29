using MediatR;
using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Files.Commands.UploadCorDocument;

public record UploadEmployeeSchedule(IFormFile File) : IRequest<Result<string>>;
public class UploadEmployeeScheduleHandler(IUserContext user, IUserProfileRepository profiles,
    ICorSubmissionRepository cors, IParkingLogRepository parking, ICloudinaryService cloud,
    ICorDocumentReader reader, IVerificationStore verification) : IRequestHandler<UploadEmployeeSchedule, Result<string>>
{
    public async Task<Result<string>> Handle(UploadEmployeeSchedule request, CancellationToken ct)
    {
        var owner=user.GetUserId(); var profile=await profiles.GetByUserIdAsync(owner);
        if(profile?.Personnel==null) return Result<string>.Failure("Only personnel can submit an employee schedule.",ErrorCode.Forbidden);
        if(await parking.HasActiveParkingLogByUserIdAsync(owner)) return Result<string>.Failure("Exit parking before replacing your employee schedule.",ErrorCode.Conflict);
        var cor=await cors.GetLatestByUserIdAsync(owner);
        if(cor==null) return Result<string>.Failure("Submit your employee ID before the schedule.",ErrorCode.BadRequest);
        var valid=await new UploadCorDocumentCommandValidator().ValidateAsync(new UploadCorDocumentCommand(request.File),ct);
        if(!valid.IsValid) return Result<string>.Failure(string.Join(" ",valid.Errors.Select(e=>e.ErrorMessage)),ErrorCode.BadRequest);
        try
        {
            await using(var stream=request.File.OpenReadStream()) await reader.ValidateAsync(stream,request.File.FileName,ct);
            var pdf=Path.GetExtension(request.File.FileName).Equals(".pdf",StringComparison.OrdinalIgnoreCase);
            var uploaded=pdf ? await cloud.UploadPdfAsync(request.File,"parkflow/licenses") : await cloud.UploadImageAsync(request.File,"parkflow/licenses");
            cor.SubmitEmployeeSchedule(uploaded.SecureUrl);
            await cors.SaveEmployeeScheduleAsync(cor);
            await verification.EnqueueAsync(VerificationType.Schedule,cor.Id,ct);
            return Result<string>.Success(uploaded.SecureUrl,"Employee schedule submitted for administrator review.");
        }
        catch(DocumentValidationException error) { return Result<string>.Failure(error.Message,ErrorCode.BadRequest); }
    }
}
