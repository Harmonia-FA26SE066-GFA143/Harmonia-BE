using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class SongPersonnelRequirementService(
    ISongListItemRepository songListItemRepository, ICurrentUserService currentUser, IMapper mapper) : ISongPersonnelRequirementService
{
    public async Task<Result<List<SongPersonnelRequirementDto>>> GetAsync(Guid songListItemId, CancellationToken cancellationToken)
    {
        var item = await songListItemRepository.GetWithPersonnelRequirementsAsync(songListItemId, cancellationToken);
        if (item is null || !IsVisibleToCaller(item.SongList))
            return Result<List<SongPersonnelRequirementDto>>.Failure(ErrorCodes.SongListItemNotFound);

        return Result<List<SongPersonnelRequirementDto>>.Success(ToDtos(item));
    }

    public async Task<Result<List<SongPersonnelRequirementDto>>> UpdateAsync(
        Guid songListItemId, UpdateSongPersonnelRequirementsRequest request, CancellationToken cancellationToken)
    {
        var item = await songListItemRepository.GetWithPersonnelRequirementsAsync(songListItemId, cancellationToken);
        if (item is null) return Result<List<SongPersonnelRequirementDto>>.Failure(ErrorCodes.SongListItemNotFound);

        var songList = item.SongList;
        if (!await songListItemRepository.IsLatestVersionAsync(songList.EventId, songList.Version, cancellationToken))
            return Result<List<SongPersonnelRequirementDto>>.Failure(ErrorCodes.SongListNotLatestVersion);

        if (songList.LiturgicalEvent.ServiceRoster is { Status: RosterStatus.Finalized })
            return Result<List<SongPersonnelRequirementDto>>.Failure(ErrorCodes.RosterAlreadyFinalized);

        var skillIds = request.Requirements.Select(x => x.SkillId).ToList();
        var skills = skillIds.Count == 0 ? new Dictionary<Guid, Skill>() : await songListItemRepository.GetSkillsAsync(skillIds, cancellationToken);

        // Validate everything before touching the tracked item, so a failure leaves it untouched.
        var current = item.PersonnelRequirements.Select(x => x.SkillId).ToHashSet();
        foreach (var skillId in skillIds)
        {
            if (!skills.TryGetValue(skillId, out var skill)) return Result<List<SongPersonnelRequirementDto>>.Failure(ErrorCodes.SkillNotFound);
            if (!skill.IsActive && !current.Contains(skillId)) return Result<List<SongPersonnelRequirementDto>>.Failure(ErrorCodes.SkillInactive);
        }

        foreach (var removed in item.PersonnelRequirements.Where(x => !skillIds.Contains(x.SkillId)).ToList())
            item.PersonnelRequirements.Remove(removed);

        foreach (var requirement in request.Requirements)
        {
            var existing = item.PersonnelRequirements.FirstOrDefault(x => x.SkillId == requirement.SkillId);
            if (existing is not null)
            {
                existing.RequiredCount = requirement.RequiredCount;
                continue;
            }

            item.PersonnelRequirements.Add(new SongPersonnelRequirement
            {
                SongListItemId = item.Id,
                SkillId = requirement.SkillId,
                Skill = skills[requirement.SkillId],
                RequiredCount = requirement.RequiredCount,
            });
        }

        await songListItemRepository.SaveChangesAsync(cancellationToken);
        return Result<List<SongPersonnelRequirementDto>>.Success(ToDtos(item));
    }

    /// <summary>ChoirMembers only see approved song lists of published events; other roles see everything.</summary>
    private bool IsVisibleToCaller(SongList songList) =>
        currentUser.RoleName != RoleNames.ChoirMember
        || (songList.Status == SongListStatus.Approved && songList.LiturgicalEvent.Status == EventStatus.Published);

    private List<SongPersonnelRequirementDto> ToDtos(SongListItem item) =>
        mapper.Map<List<SongPersonnelRequirementDto>>(item.PersonnelRequirements.OrderBy(x => x.Skill.Name));
}
