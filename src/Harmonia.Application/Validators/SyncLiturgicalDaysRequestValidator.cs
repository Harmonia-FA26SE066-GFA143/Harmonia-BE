using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>The year range the Liturgical Calendar API serves.</summary>
public class SyncLiturgicalDaysRequestValidator : AbstractValidator<SyncLiturgicalDaysRequest>
{
    public SyncLiturgicalDaysRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(1970, 9999);
    }
}
