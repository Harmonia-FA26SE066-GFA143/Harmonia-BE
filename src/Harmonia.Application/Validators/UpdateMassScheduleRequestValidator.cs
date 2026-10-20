using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the catalogs and the day + time + location uniqueness are checked in MassScheduleService.</summary>
public class UpdateMassScheduleRequestValidator : AbstractValidator<UpdateMassScheduleRequest>
{
    public UpdateMassScheduleRequestValidator()
    {
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.MassTypeId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200);
    }
}
