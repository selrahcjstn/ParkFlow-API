using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Queries.ListCorSubmissions;

public class ListCorSubmissionsHandler : IRequestHandler<ListCorSubmissionsQuery, Result<IEnumerable<CorSubmissionDto>>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public ListCorSubmissionsHandler(ICorSubmissionRepository corSubmissionRepository)
    {
        _corSubmissionRepository = corSubmissionRepository;
    }

    public async Task<Result<IEnumerable<CorSubmissionDto>>> Handle(ListCorSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var submissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        var dtos = submissions.Select(s => new CorSubmissionDto(
            s.Id,
            s.UserAccountId,
            s.AcademicTerm,
            s.CorDocumentUrl,
            s.VerificationStatus));

        return Result<IEnumerable<CorSubmissionDto>>.Success(dtos, "COR submissions retrieved.");
    }
}
