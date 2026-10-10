using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SuggestServiceRosterRequestValidator : AbstractValidator<SuggestServiceRosterRequest>
{
    public SuggestServiceRosterRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
    }
}
