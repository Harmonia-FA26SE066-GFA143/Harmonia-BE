using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class PracticeAssignmentService(
    IPracticeAssignmentRepository practiceAssignmentRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    ISongRepository songRepository,
    IMusicMaterialRepository musicMaterialRepository,
    IGenericRepository<Skill> skillRepository,
    IMemberProfileRepository memberProfileRepository,
    IMemberSkillRepository memberSkillRepository,
    IPracticeSubmissionRepository practiceSubmissionRepository,
    INotificationService notificationService,
    IFileStorageService fileStorageService,
    IMapper mapper) : IPracticeAssignmentService
{
    public const long MaxAudioSizeBytes = 20 * 1024 * 1024;

    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".wav"];

    public async Task<Result<PracticeAssignmentDto>> CreateAsync(
        CreatePracticeAssignmentRequest request, CancellationToken cancellationToken)
    {
        var referenceError = await CheckReferencesAsync(request, cancellationToken);
        if (referenceError is not null)
        {
            return Result<PracticeAssignmentDto>.Failure(referenceError);
        }

        var assignment = new PracticeAssignment
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            SongId = request.SongId,
            MaterialId = request.MaterialId,
            Title = request.Title,
            Instruction = request.Instruction,
            Scope = request.Scope,
            DueDate = request.DueDate,
        };

        List<Guid> recipientUserIds;
        switch (request.Scope)
        {
            case AssignmentScope.SkillGroup:
            {
                var skillIds = request.SkillIds!.Distinct().ToList();
                var skills = await skillRepository.ListAsync(x => skillIds.Contains(x.Id), cancellationToken);
                if (skills.Count != skillIds.Count)
                {
                    return Result<PracticeAssignmentDto>.Failure(ErrorCodes.SkillNotFound);
                }

                if (skills.Any(x => !x.IsActive))
                {
                    return Result<PracticeAssignmentDto>.Failure(ErrorCodes.SkillInactive);
                }

                assignment.Targets = skillIds
                    .Select(id => new PracticeAssignmentTarget { Id = Guid.NewGuid(), TargetType = TargetType.Skill, SkillId = id })
                    .ToList();
                recipientUserIds = await memberSkillRepository.GetActiveMemberUserIdsBySkillsAsync(skillIds, cancellationToken);
                break;
            }

            case AssignmentScope.Individual:
            {
                var memberIds = request.MemberIds!.Distinct().ToList();
                var members = await memberProfileRepository.ListAsync(
                    x => memberIds.Contains(x.Id) && x.Status == MemberStatus.Active, cancellationToken);

                // An inactive or departed member counts as missing, same as an unknown id.
                if (members.Count != memberIds.Count)
                {
                    return Result<PracticeAssignmentDto>.Failure(ErrorCodes.MemberNotFound);
                }

                assignment.Targets = memberIds
                    .Select(id => new PracticeAssignmentTarget { Id = Guid.NewGuid(), TargetType = TargetType.Member, MemberId = id })
                    .ToList();
                recipientUserIds = members.Select(x => x.UserId).ToList();
                break;
            }

            default:
            {
                var members = await memberProfileRepository.ListAsync(x => x.Status == MemberStatus.Active, cancellationToken);
                recipientUserIds = members.Select(x => x.UserId).ToList();
                break;
            }
        }

        await practiceAssignmentRepository.AddAsync(assignment, cancellationToken);
        await practiceAssignmentRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.AssignmentNotice,
                Title: "New practice assignment",
                Content: $"You have a new practice assignment: {assignment.Title}.",
                RecipientUserIds: recipientUserIds,
                ReferenceType: nameof(PracticeAssignment),
                ReferenceId: assignment.Id),
            cancellationToken);

        return Result<PracticeAssignmentDto>.Success(mapper.Map<PracticeAssignmentDto>(assignment));
    }

    public async Task<Result<PagedList<PracticeAssignmentDetailDto>>> GetMineAsync(
        Guid userId, SearchMyPracticeAssignmentsRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PagedList<PracticeAssignmentDetailDto>>.Failure(ErrorCodes.MemberNotFound);

        var page = await practiceAssignmentRepository.GetForMemberAsync(member.Id, request, cancellationToken);
        return Result<PagedList<PracticeAssignmentDetailDto>>.Success(new PagedList<PracticeAssignmentDetailDto>(
            mapper.Map<List<PracticeAssignmentDetailDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<PracticeAssignmentDetailDto>> GetMineByIdAsync(
        Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PracticeAssignmentDetailDto>.Failure(ErrorCodes.MemberNotFound);

        var assignment = await practiceAssignmentRepository.GetByIdForMemberAsync(id, member.Id, cancellationToken);
        return assignment is null
            ? Result<PracticeAssignmentDetailDto>.Failure(ErrorCodes.PracticeAssignmentNotFound)
            : Result<PracticeAssignmentDetailDto>.Success(mapper.Map<PracticeAssignmentDetailDto>(assignment));
    }

    public async Task<Result<PracticeSubmissionDto>> SubmitAsync(
        Guid userId, Guid assignmentId, CreatePracticeSubmissionRequest request, FileContent? file,
        CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PracticeSubmissionDto>.Failure(ErrorCodes.MemberNotFound);

        // Same scope as GetMineAsync: an assignment the member does not receive is reported as missing.
        var assignment = await practiceAssignmentRepository.GetByIdForMemberAsync(assignmentId, member.Id, cancellationToken);
        if (assignment is null) return Result<PracticeSubmissionDto>.Failure(ErrorCodes.PracticeAssignmentNotFound);

        // Submissions holds only the member's newest attempt.
        var latest = assignment.Submissions.MaxBy(x => x.AttemptNo);
        if (latest?.Status == SubmissionStatus.Passed)
            return Result<PracticeSubmissionDto>.Failure(ErrorCodes.PracticeSubmissionAlreadyPassed);

        if (DateTime.UtcNow > assignment.DueDate)
            return Result<PracticeSubmissionDto>.Failure(ErrorCodes.PracticeSubmissionPastDue);

        if (CheckAudio(file) is { } fileError) return Result<PracticeSubmissionDto>.Failure(fileError);

        var upload = await fileStorageService.UploadAsync(
            file!.Content, file.FileName, "practice-submissions", isPrivate: true, cancellationToken);
        if (!upload.IsSuccess) return Result<PracticeSubmissionDto>.Failure(upload.Code!);

        var submission = new PracticeSubmission
        {
            Id = Guid.NewGuid(),
            PracticeAssignmentId = assignment.Id,
            MemberId = member.Id,
            AudioPublicId = upload.Value!.PublicId,
            DurationSeconds = request.DurationSeconds,
            AttemptNo = (latest?.AttemptNo ?? 0) + 1,
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Submitted,
        };

        bool added;
        try
        {
            added = await practiceSubmissionRepository.TryAddAsync(submission, cancellationToken);
        }
        catch
        {
            // No row points at the file, so remove it rather than leave an orphan in storage.
            await fileStorageService.DeleteAsync(submission.AudioPublicId, isPrivate: true, CancellationToken.None);
            throw;
        }

        if (!added)
        {
            await fileStorageService.DeleteAsync(submission.AudioPublicId, isPrivate: true, CancellationToken.None);
            return Result<PracticeSubmissionDto>.Failure(ErrorCodes.PracticeSubmissionConflict);
        }

        var dto = mapper.Map<PracticeSubmissionDto>(submission);
        dto.AudioUrl = fileStorageService.GetSignedUrl(submission.AudioPublicId);
        return Result<PracticeSubmissionDto>.Success(dto);
    }

    /// <summary>Extension and size come from the server-side upload; the client's Content-Type is never trusted.</summary>
    private static string? CheckAudio(FileContent? file)
    {
        if (file is null || file.Length == 0) return ErrorCodes.PracticeAudioRequired;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AudioExtensions.Contains(extension)) return ErrorCodes.PracticeAudioTypeNotAllowed;

        return file.Length > MaxAudioSizeBytes ? ErrorCodes.PracticeAudioTooLarge : null;
    }

    /// <summary>Returns the error code of the first optional reference that is missing or unusable, or null.</summary>
    private async Task<string?> CheckReferencesAsync(
        CreatePracticeAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (request.EventId is { } eventId)
        {
            var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
            if (liturgicalEvent is null) return ErrorCodes.EventNotFound;
            if (liturgicalEvent.Status == EventStatus.Cancelled) return ErrorCodes.EventCancelled;
        }

        if (request.SongId is { } songId)
        {
            var song = await songRepository.GetByIdAsync(songId, cancellationToken);
            if (song is null) return ErrorCodes.SongNotFound;
            if (!song.IsActive) return ErrorCodes.SongInactive;
        }

        if (request.MaterialId is { } materialId)
        {
            var material = await musicMaterialRepository.GetByIdAsync(materialId, cancellationToken);
            if (material is null
                || !material.IsActive
                || (request.SongId.HasValue && material.SongId != request.SongId))
            {
                return ErrorCodes.MaterialNotFound;
            }
        }

        return null;
    }
}
