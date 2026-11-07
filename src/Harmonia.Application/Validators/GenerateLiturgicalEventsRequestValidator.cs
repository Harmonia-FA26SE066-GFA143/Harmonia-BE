using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class GenerateLiturgicalEventsRequestValidator : AbstractValidator<GenerateLiturgicalEventsRequest>
{
    public const int MaxDays = 62;

    public GenerateLiturgicalEventsRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.FromDate >= VietnamTime.Today
                && x.ToDate >= x.FromDate
                && x.ToDate.DayNumber - x.FromDate.DayNumber < MaxDays)
            .WithErrorCode(ErrorCodes.LiturgicalEventGenerationRangeInvalid)
            .WithMessage($"FromDate must not be in the past and the range must span 1 to {MaxDays} days.");
    }
}
