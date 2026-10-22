using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; name uniqueness is checked in ChoirService.</summary>
public class CreateChoirRequestValidator : AbstractValidator<CreateChoirRequest>
{
    public CreateChoirRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
