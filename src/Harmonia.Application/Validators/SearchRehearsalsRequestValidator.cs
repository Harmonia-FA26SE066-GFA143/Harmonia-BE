using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SearchRehearsalsRequestValidator : AbstractValidator<SearchRehearsalsRequest>
{
    public SearchRehearsalsRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().When(x => x.EventId.HasValue);

        // Reuses the report code: it means "invalid date range" and the client already translates it.
        RuleFor(x => x.ToDate)
            .Must((request, toDate) => request.FromDate is null || toDate is null || request.FromDate <= toDate)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid);
    }
}
