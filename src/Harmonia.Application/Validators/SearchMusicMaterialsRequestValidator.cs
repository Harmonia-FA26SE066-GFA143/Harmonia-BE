using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchMusicMaterialsRequestValidator : AbstractValidator<SearchMusicMaterialsRequest>
{
    public SearchMusicMaterialsRequestValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.SongId).NotEqual(Guid.Empty);
        RuleFor(x => x.LiturgicalSeasonId).NotEqual(Guid.Empty);
        RuleFor(x => x.SkillId).NotEqual(Guid.Empty);
        RuleFor(x => x.MaterialType).IsInEnum();
        RuleFor(x => x.LearningStatus).IsInEnum();
    }
}
