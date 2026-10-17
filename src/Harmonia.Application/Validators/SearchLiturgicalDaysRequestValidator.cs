using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SearchLiturgicalDaysRequestValidator : AbstractValidator<SearchLiturgicalDaysRequest>
{
    public const int MaxRangeDays = 366;

    public SearchLiturgicalDaysRequestValidator()
    {
        RuleFor(x => x.FromDate).NotEmpty();
        RuleFor(x => x.ToDate).NotEmpty();

        // Reuses the report codes: they mean "invalid / too large date range" and the client already translates them.
        RuleFor(x => x.ToDate)
            .Cascade(CascadeMode.Stop)
            .Must((request, toDate) => request.FromDate <= toDate)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid)
            .Must((request, toDate) => toDate.DayNumber - request.FromDate.DayNumber < MaxRangeDays)
            .WithErrorCode(ErrorCodes.ReportDateRangeTooLarge)
            .When(x => x.FromDate != default && x.ToDate != default);
    }
}
