using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class GetScheduleRehearsalsRequestValidator : AbstractValidator<GetScheduleRehearsalsRequest>
{
    /// <summary>The response is not paged, so a range is capped to about a quarter.</summary>
    public const int MaxRangeDays = 93;

    public GetScheduleRehearsalsRequestValidator()
    {
        // Reuses the report codes: they mean "invalid / too large date range" and the client already translates them.
        RuleFor(x => x.FromDate)
            .NotNull().When(x => x.ToDate.HasValue)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid);

        RuleFor(x => x.ToDate)
            .NotNull().When(x => x.FromDate.HasValue)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid);

        RuleFor(x => x.ToDate!.Value)
            .Cascade(CascadeMode.Stop)
            .Must((request, toDate) => request.FromDate!.Value <= toDate)
            .WithErrorCode(ErrorCodes.ReportDateRangeInvalid)
            .Must((request, toDate) => toDate.DayNumber - request.FromDate!.Value.DayNumber < MaxRangeDays)
            .WithErrorCode(ErrorCodes.ReportDateRangeTooLarge)
            .OverridePropertyName(nameof(GetScheduleRehearsalsRequest.ToDate))
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
    }
}
