using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchDirectorNotesRequestValidator : AbstractValidator<SearchDirectorNotesRequest>
{
    public SearchDirectorNotesRequestValidator()
    {
        RuleFor(x => x.EventId).NotEqual(Guid.Empty);
    }
}
