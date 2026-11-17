using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SaveLiturgicalSeasonRequestValidator : AbstractValidator<SaveLiturgicalSeasonRequest>
{
    public SaveLiturgicalSeasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CalendarKey)
            .Must(key => key is null || LiturgicalCalendarSeasons.All.Contains(key))
            .WithErrorCode(ErrorCodes.LiturgicalSeasonCalendarKeyInvalid)
            .WithMessage("CalendarKey must be Advent, Christmas, Lent, Easter or OrdinaryTime.");

        // A dated season needs both dates; a keyed one may leave both out, but not just one.
        RuleFor(x => x.StartDate).NotEmpty().When(x => x.CalendarKey is null || x.EndDate is not null);
        RuleFor(x => x.EndDate).NotEmpty().When(x => x.CalendarKey is null || x.StartDate is not null);
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate is not null && x.EndDate is not null)
            .WithErrorCode(ErrorCodes.LiturgicalSeasonDateInvalid);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}$");
    }
}
