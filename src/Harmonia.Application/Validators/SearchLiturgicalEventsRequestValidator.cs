using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SearchLiturgicalEventsRequestValidator : AbstractValidator<SearchLiturgicalEventsRequest>
{
    public SearchLiturgicalEventsRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();

        // Reuses the report code: it means "invalid date range" and the client already translates it.
        RuleFor(x => x.ToDate)
            .Must((request, toDate) => request.FromDate is null || toDate is null || request.FromDate <= toDate)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid);
    }
}
