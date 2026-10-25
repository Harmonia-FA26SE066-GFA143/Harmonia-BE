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
    ISystemSettingService systemSettingService,
    IChoirScopeService choirScope,
    IMapper mapper) : IPracticeAssignmentService
{
    public const long MaxAudioSizeBytes = 20 * 1024 * 1024;

    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".wav"];

    public async Task<Result<PagedList<PracticeAssignmentDto>>> SearchAsync(
        SearchPracticeAssignmentsRequest request, CancellationToken cancellationToken)
    {
        var choirIds = await choirScope.GetDirectedChoirIdsAsync(cancellationToken);
        var page = await practiceAssignmentRepository.SearchAsync(request, choirIds, cancellationToken);

        return Result<PagedList<PracticeAssignmentDto>>.Success(new PagedList<PracticeAssignmentDto>(
            mapper.Map<List<PracticeAssignmentDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<PracticeAssignmentDto>> CreateAsync(
        CreatePracticeAssignmentRequest request, CancellationToken cancellationToken)
    {
        var referenceError = await CheckReferencesAsync(request, cancellationToken);
        if (referenceError is not null)
        {
            return Result<PracticeAssignmentDto>.Failure(referenceError);
        }

        if (await choirScope.CheckChoirForNewRecordAsync(request.ChoirId, request.EventId, cancellationToken)
            is { } choirError)
        {
            return Result<PracticeAssignmentDto>.Failure(choirError);
        }

        // Every scope stays inside the choir: All means the whole choir, targets must be its members.
        var choirMemberIds = (await choirScope.GetChoirMemberIdsAsync(request.ChoirId, cancellationToken))!;
        var choirMembers = await memberProfileRepository.ListAsync(
            x => choirMemberIds.Contains(x.Id) && x.Status == MemberStatus.Active, cancellationToken);
        var choirUserIds = choirMembers.Select(x => x.UserId).ToHashSet();

        var assignment = new PracticeAssignment
        {
            Id = Guid.NewGuid(),
            ChoirId = request.ChoirId,
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
                recipientUserIds = (await memberSkillRepository.GetActiveMemberUserIdsBySkillsAsync(skillIds, cancellationToken))
                    .Where(choirUserIds.Contains)
                    .ToList();
                break;
            }

            case AssignmentScope.Individual:
            {
                var memberIds = request.MemberIds!.Distinct().ToList();
                var members = choirMembers.Where(x => memberIds.Contains(x.Id)).ToList();

                // An inactive or departed member, or one outside the choir, counts as missing, same as an unknown id.
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
                recipientUserIds = choirUserIds.ToList();
                break;
        }

        await practiceAssignmentRepository.AddAsync(assignment, cancellationToken);
        await practiceAssignmentRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.AssignmentNotice,
                Title: "Bài tập mới",
                Content: $"Bạn có bài tập mới: {assignment.Title}.",
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
        var items = mapper.Map<List<PracticeAssignmentDetailDto>>(page.Items);
        await FillCanSubmitAsync(items, cancellationToken);
        return Result<PagedList<PracticeAssignmentDetailDto>>.Success(new PagedList<PracticeAssignmentDetailDto>(
            items, page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<PracticeAssignmentDetailDto>> GetMineByIdAsync(
        Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PracticeAssignmentDetailDto>.Failure(ErrorCodes.MemberNotFound);

        var assignment = await practiceAssignmentRepository.GetByIdForMemberAsync(id, member.Id, cancellationToken);
        if (assignment is null) return Result<PracticeAssignmentDetailDto>.Failure(ErrorCodes.PracticeAssignmentNotFound);

        var dto = mapper.Map<PracticeAssignmentDetailDto>(assignment);
        await FillCanSubmitAsync([dto], cancellationToken);
        return Result<PracticeAssignmentDetailDto>.Success(dto);
    }

    // Reads the setting once per request; the rule itself is CannotSubmitCode, shared with SubmitAsync.
    private async Task FillCanSubmitAsync(List<PracticeAssignmentDetailDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;

        var allowLate = await systemSettingService.GetBoolAsync(SystemSettingKeys.PracticeAllowLateSubmission, cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.CannotSubmitReason = CannotSubmitCode(item.LatestSubmissionStatus, now > item.DueDate, allowLate);
            item.CanSubmit = item.CannotSubmitReason is null;
        }
    }

    // A late recording is still counted as overdue in progress and reports; the setting only lets it in.
    private static string? CannotSubmitCode(SubmissionStatus? latestStatus, bool isPastDue, bool allowLate)
    {
        if (latestStatus == SubmissionStatus.Passed) return ErrorCodes.PracticeSubmissionAlreadyPassed;
        return isPastDue && !allowLate ? ErrorCodes.PracticeSubmissionPastDue : null;
    }

    public async Task<Result<MyPracticeAssignmentCountsResponse>> GetMyCountsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<MyPracticeAssignmentCountsResponse>.Failure(ErrorCodes.MemberNotFound);

        // ponytail: counts in memory over one row per assignment; move to SQL GROUP BY if a member ever gets thousands.
        var progress = await practiceAssignmentRepository.GetProgressForMemberAsync(member.Id, cancellationToken);
        var now = DateTime.UtcNow;

        return Result<MyPracticeAssignmentCountsResponse>.Success(new MyPracticeAssignmentCountsResponse(
            Total: progress.Count,
            NotSubmitted: progress.Count(x => x.LatestStatus is null),
            Submitted: progress.Count(x => x.LatestStatus == SubmissionStatus.Submitted),
            NeedsRevision: progress.Count(x => x.LatestStatus == SubmissionStatus.NeedsRevision),
            Passed: progress.Count(x => x.LatestStatus == SubmissionStatus.Passed),
            Overdue: progress.Count(x => PracticeAssignment.IsOverdue(x.DueDate, x.LatestStatus, now))));
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
        var isPastDue = DateTime.UtcNow > assignment.DueDate;
        // The setting is read only when it can matter.
        var allowLate = isPastDue
            && await systemSettingService.GetBoolAsync(SystemSettingKeys.PracticeAllowLateSubmission, cancellationToken);
        if (CannotSubmitCode(latest?.Status, isPastDue, allowLate) is { } cannotSubmit)
            return Result<PracticeSubmissionDto>.Failure(cannotSubmit);

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
            if (liturgicalEvent is null) return ErrorCodes.LiturgicalEventNotFound;
            if (liturgicalEvent.Status == EventStatus.Cancelled) return ErrorCodes.LiturgicalEventCancelled;
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
