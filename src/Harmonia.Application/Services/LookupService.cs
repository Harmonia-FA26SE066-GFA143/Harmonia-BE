using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using System.Linq.Expressions;

namespace Harmonia.Application.Services;

public class LookupService(
    IGenericRepository<MassType> massTypes,
    IGenericRepository<CeremonyType> ceremonyTypes,
    IGenericRepository<EventCategory> eventCategories,
    IGenericRepository<SongTheme> songThemes,
    IGenericRepository<SkillCategory> skillCategories,
    IGenericRepository<LiturgicalSeason> liturgicalSeasons,
    IGenericRepository<LiturgicalSlot> liturgicalSlots,
    IGenericRepository<WorshipLocation> worshipLocations,
    IGenericRepository<Skill> skills,
    IGenericRepository<SongVocalRequirement> songVocalRequirements,
    IGenericRepository<SongInstrumentRequirement> songInstrumentRequirements,
    IGenericRepository<LiturgicalSlotRequirement> slotRequirements,
    IMapper mapper) : ILookupService
{
    public async Task<Result<List<MassTypeDto>>> GetMassTypesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await massTypes.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<MassTypeDto>>.Success(mapper.Map<List<MassTypeDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<CeremonyTypeDto>>> GetCeremonyTypesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await ceremonyTypes.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<CeremonyTypeDto>>.Success(mapper.Map<List<CeremonyTypeDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<EventCategoryDto>>> GetEventCategoriesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await eventCategories.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<EventCategoryDto>>.Success(mapper.Map<List<EventCategoryDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<SongThemeDto>>> GetSongThemesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await songThemes.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<SongThemeDto>>.Success(mapper.Map<List<SongThemeDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<SkillCategoryDto>>> GetSkillCategoriesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await skillCategories.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<SkillCategoryDto>>.Success(mapper.Map<List<SkillCategoryDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<LiturgicalSeasonDto>>> GetLiturgicalSeasonsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await liturgicalSeasons.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<LiturgicalSeasonDto>>.Success(
            mapper.Map<List<LiturgicalSeasonDto>>(rows.OrderBy(x => x.StartDate).ThenBy(x => x.Name)));
    }

    public async Task<Result<List<LiturgicalSlotDto>>> GetLiturgicalSlotsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await liturgicalSlots.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<LiturgicalSlotDto>>.Success(
            mapper.Map<List<LiturgicalSlotDto>>(rows.OrderBy(x => x.DefaultOrder).ThenBy(x => x.Name)));
    }

    public async Task<Result<List<WorshipLocationDto>>> GetWorshipLocationsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await worshipLocations.ListAsync(x => includeInactive || x.IsActive, cancellationToken);
        return Result<List<WorshipLocationDto>>.Success(mapper.Map<List<WorshipLocationDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<SkillDto>>> GetSkillsAsync(Guid? categoryId, bool includeInactive, CancellationToken cancellationToken)
    {
        var rows = await skills.ListAsync(
            x => (includeInactive || (x.IsActive && x.Category.IsActive)) && (categoryId == null || x.CategoryId == categoryId),
            cancellationToken);
        return Result<List<SkillDto>>.Success(mapper.Map<List<SkillDto>>(rows.OrderBy(x => x.Name)));
    }

    public Task<Result<MassTypeDto>> SaveMassTypeAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        return SaveAsync<MassType, MassTypeDto>(
            massTypes, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public Task<Result<CeremonyTypeDto>> SaveCeremonyTypeAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        return SaveAsync<CeremonyType, CeremonyTypeDto>(
            ceremonyTypes, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public Task<Result<EventCategoryDto>> SaveEventCategoryAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        return SaveAsync<EventCategory, EventCategoryDto>(
            eventCategories, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public Task<Result<SkillCategoryDto>> SaveSkillCategoryAsync(Guid? id, SaveCatalogItemRequest request, CancellationToken cancellationToken)
    {
        // Code refers to the seeded categories by id (SkillCategoryIds), so they can be renamed but must stay on.
        if (id is { } categoryId && !request.IsActive && SkillCategoryIds.IsFixed(categoryId))
        {
            return Task.FromResult(Result<SkillCategoryDto>.Failure(ErrorCodes.LookupInUse));
        }

        var name = request.Name.Trim();
        return SaveAsync<SkillCategory, SkillCategoryDto>(
            skillCategories, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public async Task<Result<SkillDto>> SaveSkillAsync(Guid? id, SaveSkillRequest request, CancellationToken cancellationToken)
    {
        var category = await skillCategories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result<SkillDto>.Failure(ErrorCodes.SkillCategoryNotFound);
        }

        var existing = id is { } skillId ? await skills.GetByIdAsync(skillId, cancellationToken) : null;
        if (id is not null && existing is null)
        {
            return Result<SkillDto>.Failure(ErrorCodes.SkillNotFound);
        }

        if (existing?.CategoryId != request.CategoryId)
        {
            // A skill already sitting in a switched-off category can still be edited there.
            if (!category.IsActive)
            {
                return Result<SkillDto>.Failure(ErrorCodes.LookupInactive);
            }

            // Songs keep vocal and instrument requirements in separate tables, split by this very boundary.
            if (existing is not null
                && (existing.CategoryId == SkillCategoryIds.Instrument) != (request.CategoryId == SkillCategoryIds.Instrument)
                && await IsRequiredBySongsAsync(existing.Id, cancellationToken))
            {
                return Result<SkillDto>.Failure(ErrorCodes.SkillInUse);
            }
        }

        var name = request.Name.Trim();
        return await SaveAsync<Skill, SkillDto>(
            skills, id, ErrorCodes.SkillNotFound,
            x => x.CategoryId == request.CategoryId && x.Name == name && x.Id != id, ErrorCodes.SkillNameDuplicate,
            x => { x.CategoryId = request.CategoryId; x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public async Task<Result<SongThemeDto>> SaveSongThemeAsync(Guid? id, SaveSongThemeRequest request, CancellationToken cancellationToken)
    {
        if (request.ParentId is { } parentId)
        {
            // The whole table is a few dozen rows, so walking up the tree in memory is cheaper than a recursive query.
            var parents = (await songThemes.ListAsync(_ => true, cancellationToken)).ToDictionary(x => x.Id);
            var current = id is { } themeId ? parents.GetValueOrDefault(themeId) : null;

            // An active theme never sits under a switched-off one; an inactive one may stay where it is.
            if (!parents.TryGetValue(parentId, out var parent) || (!parent.IsActive && (request.IsActive || current?.ParentId != parentId)))
                return Result<SongThemeDto>.Failure(ErrorCodes.SongThemeParentInvalid);

            for (Guid? ancestor = parentId; ancestor is { } ancestorId; ancestor = parents.GetValueOrDefault(ancestorId)?.ParentId)
            {
                if (ancestorId == id) return Result<SongThemeDto>.Failure(ErrorCodes.SongThemeParentInvalid);
            }
        }

        // Switching a theme off while children are on would leave a live branch under a dead one: children go first.
        if (id is { } existingId && !request.IsActive
            && (await songThemes.ListAsync(x => x.ParentId == existingId && x.IsActive, cancellationToken)).Count > 0)
            return Result<SongThemeDto>.Failure(ErrorCodes.SongThemeHasActiveChildren);

        var name = request.Name.Trim();
        return await SaveAsync<SongTheme, SongThemeDto>(
            songThemes, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; x.ParentId = request.ParentId; },
            cancellationToken);
    }

    public async Task<Result<List<LiturgicalSlotRequirementDto>>> GetLiturgicalSlotRequirementsAsync(
        Guid slotId, CancellationToken cancellationToken)
    {
        if (await liturgicalSlots.GetByIdAsync(slotId, cancellationToken) is null)
            return Result<List<LiturgicalSlotRequirementDto>>.Failure(ErrorCodes.LookupNotFound);

        var rows = await slotRequirements.ListAsync(x => x.SlotId == slotId, cancellationToken);
        var skillIds = rows.Select(x => x.SkillId).ToList();
        var skillById = (await skills.ListAsync(x => skillIds.Contains(x.Id), cancellationToken)).ToDictionary(x => x.Id);
        return Result<List<LiturgicalSlotRequirementDto>>.Success(ToSlotRequirementDtos(rows, skillById));
    }

    public async Task<Result<List<LiturgicalSlotRequirementDto>>> UpdateLiturgicalSlotRequirementsAsync(
        Guid slotId, UpdateLiturgicalSlotRequirementsRequest request, CancellationToken cancellationToken)
    {
        if (await liturgicalSlots.GetByIdAsync(slotId, cancellationToken) is null)
            return Result<List<LiturgicalSlotRequirementDto>>.Failure(ErrorCodes.LookupNotFound);

        var rows = await slotRequirements.ListAsync(x => x.SlotId == slotId, cancellationToken);
        var skillIds = request.Requirements.Select(x => x.SkillId).Concat(rows.Select(x => x.SkillId)).Distinct().ToList();
        var skillById = (await skills.ListAsync(x => skillIds.Contains(x.Id), cancellationToken)).ToDictionary(x => x.Id);

        foreach (var requirement in request.Requirements)
        {
            if (!skillById.TryGetValue(requirement.SkillId, out var skill))
                return Result<List<LiturgicalSlotRequirementDto>>.Failure(ErrorCodes.SkillNotFound);
            if (!skill.IsActive && rows.All(x => x.SkillId != skill.Id))
                return Result<List<LiturgicalSlotRequirementDto>>.Failure(ErrorCodes.SkillInactive);
        }

        var result = new List<LiturgicalSlotRequirement>();
        foreach (var row in rows)
        {
            var requested = request.Requirements.FirstOrDefault(x => x.SkillId == row.SkillId);
            if (requested is null)
            {
                slotRequirements.Remove(row);
                continue;
            }

            // ListAsync rows are untracked; the kept one is re-read tracked to change its count.
            var tracked = (await slotRequirements.GetByIdAsync(row.Id, cancellationToken))!;
            tracked.RequiredCount = requested.RequiredCount;
            result.Add(tracked);
        }

        foreach (var requirement in request.Requirements.Where(r => rows.All(x => x.SkillId != r.SkillId)))
        {
            var added = new LiturgicalSlotRequirement
            {
                Id = Guid.NewGuid(), SlotId = slotId, SkillId = requirement.SkillId, RequiredCount = requirement.RequiredCount,
            };
            await slotRequirements.AddAsync(added, cancellationToken);
            result.Add(added);
        }

        await slotRequirements.SaveChangesAsync(cancellationToken);
        return Result<List<LiturgicalSlotRequirementDto>>.Success(ToSlotRequirementDtos(result, skillById));
    }

    private static List<LiturgicalSlotRequirementDto> ToSlotRequirementDtos(
        IEnumerable<LiturgicalSlotRequirement> rows, Dictionary<Guid, Skill> skillById) =>
        rows.Select(x => new LiturgicalSlotRequirementDto
            {
                SkillId = x.SkillId,
                SkillName = skillById[x.SkillId].Name,
                SkillCategoryId = skillById[x.SkillId].CategoryId,
                RequiredCount = x.RequiredCount,
            })
            .OrderBy(x => x.SkillName)
            .ToList();

    public async Task<Guid?> FindLiturgicalSeasonIdAsync(DateOnly date, CancellationToken cancellationToken) =>
        (await liturgicalSeasons.ListAsync(x => x.IsActive && x.StartDate <= date && date <= x.EndDate, cancellationToken))
            .FirstOrDefault()?.Id;

    public Task<Result<LiturgicalSlotDto>> SaveLiturgicalSlotAsync(Guid? id, SaveLiturgicalSlotRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        return SaveAsync<LiturgicalSlot, LiturgicalSlotDto>(
            liturgicalSlots, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.DefaultOrder = request.DefaultOrder; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public Task<Result<WorshipLocationDto>> SaveWorshipLocationAsync(Guid? id, SaveWorshipLocationRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        return SaveAsync<WorshipLocation, WorshipLocationDto>(
            worshipLocations, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Address = request.Address; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public async Task<Result> CheckEventCatalogsAsync(
        DateOnly eventDate, Guid? liturgicalSeasonId, Guid? massTypeId, Guid? ceremonyTypeId, Guid? categoryId,
        Guid locationId, LiturgicalEvent? current, CancellationToken cancellationToken)
    {
        var season = liturgicalSeasonId is { } seasonId
            ? await liturgicalSeasons.GetByIdAsync(seasonId, cancellationToken)
            : null;

        var error =
            Check(liturgicalSeasonId, season, season?.IsActive, current?.LiturgicalSeasonId)
            ?? await CheckAsync(massTypes, massTypeId, x => x.IsActive, current?.MassTypeId, cancellationToken)
            ?? await CheckAsync(ceremonyTypes, ceremonyTypeId, x => x.IsActive, current?.CeremonyTypeId, cancellationToken)
            ?? await CheckAsync(eventCategories, categoryId, x => x.IsActive, current?.CategoryId, cancellationToken)
            ?? await CheckAsync(worshipLocations, locationId, x => x.IsActive, current?.LocationId, cancellationToken);
        if (error is not null) return Result.Failure(error);

        // Checked on every save, not only when the date or the season changed: either one moving breaks the pair.
        // The day before the start is allowed: an evening vigil Mass belongs to the next day's season (24/12 evening → Christmas).
        return season is not null && (eventDate < season.StartDate.AddDays(-1) || eventDate > season.EndDate)
            ? Result.Failure(ErrorCodes.LiturgicalEventDateOutsideLiturgicalSeason)
            : Result.Success();
    }

    private static async Task<string?> CheckAsync<T>(
        IGenericRepository<T> repository, Guid? id, Func<T, bool> isActive, Guid? currentId, CancellationToken cancellationToken)
        where T : BaseEntity
    {
        if (id is not { } value) return null;

        var row = await repository.GetByIdAsync(value, cancellationToken);
        return Check(id, row, row is null ? null : isActive(row), currentId);
    }

    private static string? Check(Guid? id, object? row, bool? isActive, Guid? currentId)
    {
        if (id is null) return null;
        if (row is null) return ErrorCodes.LookupNotFound;
        return isActive == false && id != currentId ? ErrorCodes.LookupInactive : null;
    }

    private async Task<bool> IsRequiredBySongsAsync(Guid skillId, CancellationToken cancellationToken) =>
        (await songVocalRequirements.ListAsync(x => x.SkillId == skillId, cancellationToken)).Count > 0
        || (await songInstrumentRequirements.ListAsync(x => x.SkillId == skillId, cancellationToken)).Count > 0;

    /// <summary>
    /// Season names repeat every year, so only the dates must not overlap another active season.
    /// An inactive season is not checked.
    /// </summary>
    public Task<Result<LiturgicalSeasonDto>> SaveLiturgicalSeasonAsync(
        Guid? id, SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken) =>
        SaveAsync<LiturgicalSeason, LiturgicalSeasonDto>(
            liturgicalSeasons, id, ErrorCodes.LookupNotFound,
            x => request.IsActive && x.IsActive && x.Id != id && x.StartDate <= request.EndDate && request.StartDate <= x.EndDate,
            ErrorCodes.LiturgicalSeasonDateOverlap,
            x =>
            {
                x.Name = request.Name.Trim();
                x.StartDate = request.StartDate;
                x.EndDate = request.EndDate;
                x.ColorHex = request.ColorHex;
                x.IsActive = request.IsActive;
            },
            cancellationToken);

    /// <summary>
    /// Creates the row when <paramref name="id"/> is null, otherwise updates it. Rows matching
    /// <paramref name="conflict"/> block the save with <paramref name="conflictCode"/>.
    /// Rows are never deleted: other tables reference them, so they are switched off with IsActive.
    /// </summary>
    private async Task<Result<TDto>> SaveAsync<TEntity, TDto>(
        IGenericRepository<TEntity> repository,
        Guid? id,
        string notFoundCode,
        Expression<Func<TEntity, bool>> conflict,
        string conflictCode,
        Action<TEntity> apply,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity, new()
    {
        var entity = id is { } existingId
            ? await repository.GetByIdAsync(existingId, cancellationToken)
            : new TEntity { Id = Guid.NewGuid() };
        if (entity is null)
        {
            return Result<TDto>.Failure(notFoundCode);
        }

        if ((await repository.ListAsync(conflict, cancellationToken)).Count > 0)
        {
            return Result<TDto>.Failure(conflictCode);
        }

        apply(entity);
        if (id is null)
        {
            await repository.AddAsync(entity, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result<TDto>.Success(mapper.Map<TDto>(entity));
    }
}
