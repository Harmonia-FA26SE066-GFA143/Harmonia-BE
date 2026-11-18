using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchSongsRequestValidator : AbstractValidator<SearchSongsRequest>
{
    public SearchSongsRequestValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.LiturgicalSeasonId).NotEqual(Guid.Empty);
        RuleFor(x => x.MassTypeId).NotEqual(Guid.Empty);
        RuleFor(x => x.CeremonyTypeId).NotEqual(Guid.Empty);
        RuleFor(x => x.SongThemeId).NotEqual(Guid.Empty);
        RuleFor(x => x.LiturgicalSlotId).NotEqual(Guid.Empty);
        RuleFor(x => x.SkillId).NotEqual(Guid.Empty);
    }
}
