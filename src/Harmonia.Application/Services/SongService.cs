using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class SongService(ISongRepository songRepository, IMapper mapper) : ISongService
{
    public async Task<Result<PagedList<SongDto>>> SearchAsync(SearchSongsRequest request, CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var page = await songRepository.SearchAsync(keyword, request, cancellationToken);

        return Result<PagedList<SongDto>>.Success(new PagedList<SongDto>(
            mapper.Map<List<SongDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<SongDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await GetActiveAsync(id, cancellationToken);

        return song is null
            ? Result<SongDto>.Failure(ErrorCodes.SongNotFound)
            : Result<SongDto>.Success(mapper.Map<SongDto>(song));
    }

    public async Task<Result<SongDto>> CreateAsync(CreateSongRequest request, CancellationToken cancellationToken)
    {
        var song = mapper.Map<Song>(request);
        song.Id = Guid.NewGuid();

        if (await songRepository.ExistsByTitleAsync(song.Title, song.Composer, null, cancellationToken))
            return Result<SongDto>.Failure(ErrorCodes.SongTitleDuplicate);

        await songRepository.AddAsync(song, cancellationToken);
        await songRepository.SaveChangesAsync(cancellationToken);
        return Result<SongDto>.Success(mapper.Map<SongDto>(song));
    }

    public async Task<Result<SongDto>> UpdateAsync(Guid id, UpdateSongRequest request, CancellationToken cancellationToken)
    {
        var song = await GetActiveAsync(id, cancellationToken);
        if (song is null) return Result<SongDto>.Failure(ErrorCodes.SongNotFound);

        // Mapped onto the tracked entity; on a duplicate nothing is saved, so the change is discarded with the request.
        mapper.Map(request, song);

        if (await songRepository.ExistsByTitleAsync(song.Title, song.Composer, id, cancellationToken))
            return Result<SongDto>.Failure(ErrorCodes.SongTitleDuplicate);

        await songRepository.SaveChangesAsync(cancellationToken);
        return Result<SongDto>.Success(mapper.Map<SongDto>(song));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await GetActiveAsync(id, cancellationToken);
        if (song is null) return Result.Failure(ErrorCodes.SongNotFound);

        song.IsActive = false;
        await songRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<SongClassificationDto>> GetClassificationAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetWithClassificationAsync(id, cancellationToken);

        return song is not { IsActive: true }
            ? Result<SongClassificationDto>.Failure(ErrorCodes.SongNotFound)
            : Result<SongClassificationDto>.Success(await ToClassificationDtoAsync(song, cancellationToken));
    }

    public async Task<Result<SongClassificationDto>> UpdateClassificationAsync(
        Guid id, UpdateSongClassificationRequest request, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetWithClassificationAsync(id, cancellationToken);
        if (song is not { IsActive: true }) return Result<SongClassificationDto>.Failure(ErrorCodes.SongNotFound);

        (ClassificationTarget Type, List<Guid> Ids)[] targets =
        [
            (ClassificationTarget.LiturgicalSeason, request.LiturgicalSeasonIds),
            (ClassificationTarget.MassType, request.MassTypeIds),
            (ClassificationTarget.CeremonyType, request.CeremonyTypeIds),
            (ClassificationTarget.SongTheme, request.SongThemeIds),
        ];

        // Validate everything before touching the tracked song, so a failure leaves it untouched.
        foreach (var (type, ids) in targets)
        {
            var added = ids.Where(x => !song.Classifications.Any(c => c.TargetType == type && c.TargetId == x)).ToList();
            if (added.Count == 0) continue;

            var found = await songRepository.GetClassificationTargetsAsync(type, added, cancellationToken);
            if (added.Any(x => !found.TryGetValue(x, out var target) || !target.IsActive))
                return Result<SongClassificationDto>.Failure(ErrorCodes.SongClassificationTargetInvalid);
        }

        var skillIds = request.VocalRequirements.Concat(request.InstrumentRequirements).Select(x => x.SkillId).ToList();
        var skills = skillIds.Count == 0 ? new Dictionary<Guid, Skill>() : await songRepository.GetSkillsAsync(skillIds, cancellationToken);

        var skillError =
            CheckSkills(request.VocalRequirements, skills, song.VocalRequirements.Select(x => x.SkillId), isInstrument: false)
            ?? CheckSkills(request.InstrumentRequirements, skills, song.InstrumentRequirements.Select(x => x.SkillId), isInstrument: true);
        if (skillError is not null) return Result<SongClassificationDto>.Failure(skillError);

        foreach (var (type, ids) in targets)
        {
            foreach (var removed in song.Classifications.Where(x => x.TargetType == type && !ids.Contains(x.TargetId)).ToList())
                song.Classifications.Remove(removed);

            foreach (var targetId in ids.Where(x => !song.Classifications.Any(c => c.TargetType == type && c.TargetId == x)).ToList())
                song.Classifications.Add(new SongClassification { SongId = song.Id, TargetType = type, TargetId = targetId });
        }

        SyncRequirements(song.VocalRequirements, request.VocalRequirements, x => x.SkillId, (x, isMandatory) => x.IsMandatory = isMandatory,
            r => new SongVocalRequirement { SongId = song.Id, SkillId = r.SkillId, Skill = skills[r.SkillId], IsMandatory = r.IsMandatory });
        SyncRequirements(song.InstrumentRequirements, request.InstrumentRequirements, x => x.SkillId, (x, isMandatory) => x.IsMandatory = isMandatory,
            r => new SongInstrumentRequirement { SongId = song.Id, SkillId = r.SkillId, Skill = skills[r.SkillId], IsMandatory = r.IsMandatory });

        await songRepository.SaveChangesAsync(cancellationToken);
        return Result<SongClassificationDto>.Success(await ToClassificationDtoAsync(song, cancellationToken));
    }

    /// <summary>
    /// Every skill must exist and sit in the right list: instrument requirements need the Instrument
    /// category, vocal requirements any other. Only skills new to the song must be active.
    /// </summary>
    private static string? CheckSkills(
        List<UpdateSongSkillRequirementRequest> requirements, Dictionary<Guid, Skill> skills, IEnumerable<Guid> currentSkillIds, bool isInstrument)
    {
        var current = currentSkillIds.ToHashSet();
        foreach (var requirement in requirements)
        {
            if (!skills.TryGetValue(requirement.SkillId, out var skill)) return ErrorCodes.SkillNotFound;
            if (!skill.IsActive && !current.Contains(skill.Id)) return ErrorCodes.SkillInactive;

            if ((skill.CategoryId == SkillCategoryIds.Instrument) != isInstrument)
                return ErrorCodes.SongSkillRequirementCategoryInvalid;
        }

        return null;
    }

    /// <summary>Removes requirements no longer requested, updates IsMandatory on kept ones and adds new ones.</summary>
    private static void SyncRequirements<T>(
        ICollection<T> current, List<UpdateSongSkillRequirementRequest> requested,
        Func<T, Guid> getSkillId, Action<T, bool> setMandatory, Func<UpdateSongSkillRequirementRequest, T> create)
    {
        foreach (var removed in current.Where(x => requested.All(r => r.SkillId != getSkillId(x))).ToList())
            current.Remove(removed);

        foreach (var requirement in requested)
        {
            var existing = current.FirstOrDefault(x => getSkillId(x) == requirement.SkillId);
            if (existing is null) current.Add(create(requirement));
            else setMandatory(existing, requirement.IsMandatory);
        }
    }

    private async Task<SongClassificationDto> ToClassificationDtoAsync(Song song, CancellationToken cancellationToken)
    {
        async Task<List<SongClassificationItemDto>> ItemsAsync(ClassificationTarget type, CancellationToken cancellationToken)
        {
            var ids = song.Classifications.Where(x => x.TargetType == type).Select(x => x.TargetId).ToList();
            if (ids.Count == 0) return [];

            // Inactive targets are included on purpose; a hard-deleted lookup simply drops out.
            var targets = await songRepository.GetClassificationTargetsAsync(type, ids, cancellationToken);
            return targets.Select(x => new SongClassificationItemDto { Id = x.Key, Name = x.Value.Name }).OrderBy(x => x.Name).ToList();
        }

        return new SongClassificationDto
        {
            SongId = song.Id,
            LiturgicalSeasons = await ItemsAsync(ClassificationTarget.LiturgicalSeason, cancellationToken),
            MassTypes = await ItemsAsync(ClassificationTarget.MassType, cancellationToken),
            CeremonyTypes = await ItemsAsync(ClassificationTarget.CeremonyType, cancellationToken),
            SongThemes = await ItemsAsync(ClassificationTarget.SongTheme, cancellationToken),
            VocalRequirements = mapper.Map<List<SongVocalRequirementDto>>(song.VocalRequirements),
            InstrumentRequirements = mapper.Map<List<SongInstrumentRequirementDto>>(song.InstrumentRequirements),
        };
    }

    private async Task<Song?> GetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetByIdAsync(id, cancellationToken);
        return song is { IsActive: true } ? song : null;
    }
}
