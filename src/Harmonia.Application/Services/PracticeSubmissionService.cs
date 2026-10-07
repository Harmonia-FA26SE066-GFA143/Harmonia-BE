using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class PracticeSubmissionService(
    IPracticeSubmissionRepository practiceSubmissionRepository,
    IPracticeAssignmentRepository practiceAssignmentRepository,
    IFileStorageService fileStorageService,
    IMapper mapper) : IPracticeSubmissionService
{
    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> GetByAssignmentAsync(
        Guid assignmentId, SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        if (await practiceAssignmentRepository.GetByIdAsync(assignmentId, cancellationToken) is null)
            return Result<PagedList<PracticeSubmissionDetailDto>>.Failure(ErrorCodes.PracticeAssignmentNotFound);

        var page = await practiceSubmissionRepository.SearchAsync(assignmentId, request, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> SearchAsync(
        SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        var page = await practiceSubmissionRepository.SearchAsync(null, request, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PracticeSubmissionDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await practiceSubmissionRepository.GetWithMemberAsync(id, cancellationToken);

        return submission is null
            ? Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionNotFound)
            : Result<PracticeSubmissionDetailDto>.Success(ToDto(submission));
    }

    private PracticeSubmissionDetailDto ToDto(PracticeSubmission submission)
    {
        var dto = mapper.Map<PracticeSubmissionDetailDto>(submission);
        dto.AudioUrl = fileStorageService.GetSignedUrl(submission.AudioPublicId);
        return dto;
    }

    private PagedList<PracticeSubmissionDetailDto> ToDtoPage(PagedList<PracticeSubmission> page) =>
        new(page.Items.Select(ToDto).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
}
