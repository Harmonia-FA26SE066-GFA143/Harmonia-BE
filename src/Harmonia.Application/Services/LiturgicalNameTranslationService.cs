using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class LiturgicalNameTranslationService(
    ILiturgicalNameTranslationRepository translationRepository,
    IMapper mapper) : ILiturgicalNameTranslationService
{
    public async Task<Result<PagedList<LiturgicalNameTranslationDto>>> SearchAsync(
        SearchLiturgicalNameTranslationsRequest request, CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var page = await translationRepository.SearchAsync(keyword, request.Kind, request, cancellationToken);

        return Result<PagedList<LiturgicalNameTranslationDto>>.Success(new PagedList<LiturgicalNameTranslationDto>(
            mapper.Map<List<LiturgicalNameTranslationDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<LiturgicalNameTranslationDto>> CreateAsync(
        SaveLiturgicalNameTranslationRequest request, CancellationToken cancellationToken)
    {
        var translation = new LiturgicalNameTranslation
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind,
            EnglishName = request.EnglishName.Trim(),
            VietnameseName = request.VietnameseName.Trim(),
        };

        if (await translationRepository.ExistsAsync(translation.Kind, translation.EnglishName, null, cancellationToken))
            return Result<LiturgicalNameTranslationDto>.Failure(ErrorCodes.LiturgicalNameTranslationDuplicate);

        await translationRepository.AddAsync(translation, cancellationToken);
        await translationRepository.SaveChangesAsync(cancellationToken);
        return Result<LiturgicalNameTranslationDto>.Success(mapper.Map<LiturgicalNameTranslationDto>(translation));
    }

    public async Task<Result<LiturgicalNameTranslationDto>> UpdateAsync(
        Guid id, SaveLiturgicalNameTranslationRequest request, CancellationToken cancellationToken)
    {
        var translation = await translationRepository.GetByIdAsync(id, cancellationToken);
        if (translation is null)
            return Result<LiturgicalNameTranslationDto>.Failure(ErrorCodes.LiturgicalNameTranslationNotFound);

        var englishName = request.EnglishName.Trim();
        if (await translationRepository.ExistsAsync(request.Kind, englishName, id, cancellationToken))
            return Result<LiturgicalNameTranslationDto>.Failure(ErrorCodes.LiturgicalNameTranslationDuplicate);

        translation.Kind = request.Kind;
        translation.EnglishName = englishName;
        translation.VietnameseName = request.VietnameseName.Trim();
        await translationRepository.SaveChangesAsync(cancellationToken);
        return Result<LiturgicalNameTranslationDto>.Success(mapper.Map<LiturgicalNameTranslationDto>(translation));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var translation = await translationRepository.GetByIdAsync(id, cancellationToken);
        if (translation is null) return Result.Failure(ErrorCodes.LiturgicalNameTranslationNotFound);

        translationRepository.Remove(translation);
        await translationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<List<UntranslatedLiturgicalNameResponse>>> GetMissingAsync(CancellationToken cancellationToken)
    {
        var table = await translationRepository.GetAllAsync(cancellationToken);
        var names = await translationRepository.GetCalendarNamesAsync(cancellationToken);

        return Result<List<UntranslatedLiturgicalNameResponse>>.Success(names
            .Where(x => !table.ContainsKey(x)
                && !(x.Kind == LiturgicalNameKind.Celebration && LiturgicalNamePatterns.Translate(x.EnglishName) is not null))
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.EnglishName, StringComparer.Ordinal)
            .Select(x => new UntranslatedLiturgicalNameResponse { Kind = x.Kind, EnglishName = x.EnglishName })
            .ToList());
    }
}
