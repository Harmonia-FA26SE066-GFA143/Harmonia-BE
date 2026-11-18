using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchSongSuggestionsRequestValidator : AbstractValidator<SearchSongSuggestionsRequest>
{
    public SearchSongSuggestionsRequestValidator()
    {
        RuleFor(x => x.SlotId).NotEqual(Guid.Empty);
    }
}
