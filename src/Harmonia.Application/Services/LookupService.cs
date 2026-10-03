using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Entities;

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
    public async Task<Result<List<MassTypeDto>>> GetMassTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await massTypes.ListAsync(x => x.IsActive, cancellationToken);
        return Result<List<MassTypeDto>>.Success(mapper.Map<List<MassTypeDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<CeremonyTypeDto>>> GetCeremonyTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await ceremonyTypes.ListAsync(x => x.IsActive, cancellationToken);
        return Result<List<CeremonyTypeDto>>.Success(mapper.Map<List<CeremonyTypeDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<EventCategoryDto>>> GetEventCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await eventCategories.ListAsync(x => x.IsActive, cancellationToken);
        return Result<List<EventCategoryDto>>.Success(mapper.Map<List<EventCategoryDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<SongThemeDto>>> GetSongThemesAsync(CancellationToken cancellationToken)
    {
        var rows = await songThemes.ListAsync(x => x.IsActive, cancellationToken);
        return Result<List<SongThemeDto>>.Success(mapper.Map<List<SongThemeDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<SkillCategoryDto>>> GetSkillCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await skillCategories.ListAsync(x => x.IsActive, cancellationToken);
        return Result<List<SkillCategoryDto>>.Success(mapper.Map<List<SkillCategoryDto>>(rows.OrderBy(x => x.Name)));
    }

    public async Task<Result<List<LiturgicalSeasonDto>>> GetLiturgicalSeasonsAsync(CancellationToken cancellationToken)
    {
        var rows = await liturgicalSeasons.ListAsync(x => x.IsActive, cancellationToken);
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

    public async Task<Result<List<SkillDto>>> GetSkillsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        var rows = await skills.ListAsync(
            x => x.IsActive && x.Category.IsActive && (categoryId == null || x.CategoryId == categoryId),
            cancellationToken);
        return Result<List<SkillDto>>.Success(mapper.Map<List<SkillDto>>(rows.OrderBy(x => x.Name)));
    }
}
