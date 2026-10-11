using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class SearchReportsRequestValidator : AbstractValidator<SearchReportsRequest>
{
    public SearchReportsRequestValidator()
    {
        RuleFor(x => x.ToDate)
            .Must((request, toDate) => request.FromDate is null || toDate is null || request.FromDate <= toDate)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid);
    }
}
