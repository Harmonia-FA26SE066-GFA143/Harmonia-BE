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

    public async Task<Result<List<SongThemeDto>>> GetSongThemesAsync(CancellationToken cancellationToken)
    {
        var rows = await songThemes.ListAsync(x => x.IsActive, cancellationToken);
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

    public async Task<Result<List<LiturgicalSlotDto>>> GetLiturgicalSlotsAsync(CancellationToken cancellationToken)
    {
        var rows = await liturgicalSlots.ListAsync(x => x.IsActive, cancellationToken);
        return Result<List<LiturgicalSlotDto>>.Success(
            mapper.Map<List<LiturgicalSlotDto>>(rows.OrderBy(x => x.DefaultOrder).ThenBy(x => x.Name)));
    }

    public async Task<Result<List<WorshipLocationDto>>> GetWorshipLocationsAsync(CancellationToken cancellationToken)
    {
        var rows = await worshipLocations.ListAsync(x => x.IsActive, cancellationToken);
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
        var name = request.Name.Trim();
        return SaveAsync<SkillCategory, SkillCategoryDto>(
            skillCategories, id, ErrorCodes.LookupNotFound,
            x => x.Name == name && x.Id != id, ErrorCodes.LookupNameDuplicate,
            x => { x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    public async Task<Result<SkillDto>> SaveSkillAsync(Guid? id, SaveSkillRequest request, CancellationToken cancellationToken)
    {
        if (await skillCategories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
        {
            return Result<SkillDto>.Failure(ErrorCodes.SkillCategoryNotFound);
        }

        var name = request.Name.Trim();
        return await SaveAsync<Skill, SkillDto>(
            skills, id, ErrorCodes.SkillNotFound,
            x => x.CategoryId == request.CategoryId && x.Name == name && x.Id != id, ErrorCodes.SkillNameDuplicate,
            x => { x.CategoryId = request.CategoryId; x.Name = name; x.Description = request.Description; x.IsActive = request.IsActive; },
            cancellationToken);
    }

    /// <summary>
    /// Season names repeat every year, so only the dates must not overlap another active season.
    /// An inactive season is not checked.
    /// </summary>
    public Task<Result<LiturgicalSeasonDto>> SaveLiturgicalSeasonAsync(
        Guid? id, SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken) =>
        SaveAsync<LiturgicalSeason, LiturgicalSeasonDto>(
            liturgicalSeasons, id, ErrorCodes.LookupNotFound,
            x => request.IsActive && x.IsActive && x.Id != id && x.StartDate <= request.EndDate && request.StartDate <= x.EndDate,
            ErrorCodes.SeasonDateOverlap,
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
