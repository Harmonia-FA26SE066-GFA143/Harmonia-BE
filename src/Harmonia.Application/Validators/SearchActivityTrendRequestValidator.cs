using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SearchActivityTrendRequestValidator : AbstractValidator<SearchActivityTrendRequest>
{
    // Three years: enough for a year-by-year comparison, small enough to load in one request.
    public const int MaxDays = 3 * 366;

    public SearchActivityTrendRequestValidator()
    {
        RuleFor(x => x.GroupBy).IsInEnum();
        RuleFor(x => x.FromDate).NotNull().WithErrorCode(ErrorCodes.ReportDateRangeInvalid);
        RuleFor(x => x.ToDate)
            .NotNull()
            .Must((request, toDate) => request.FromDate is not { } from
                || (from <= toDate && toDate!.Value.DayNumber - from.DayNumber < MaxDays))
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid);
    }
}
