using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SaveLiturgicalSeasonRequestValidator : AbstractValidator<SaveLiturgicalSeasonRequest>
{
    public SaveLiturgicalSeasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithErrorCode(ErrorCodes.LiturgicalSeasonDateInvalid);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}$");
    }
}
