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
    IGenericRepository<PracticeAssignment> practiceAssignmentRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    ISongRepository songRepository,
    IMusicMaterialRepository musicMaterialRepository,
    IGenericRepository<Skill> skillRepository,
    IMemberProfileRepository memberProfileRepository,
    IMemberSkillRepository memberSkillRepository,
    INotificationService notificationService,
    IMapper mapper) : IPracticeAssignmentService
{
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
