using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdateLiturgicalEventRequestValidator : AbstractValidator<UpdateLiturgicalEventRequest>
{
    public UpdateLiturgicalEventRequestValidator()
    {
        RuleFor(x => x.EventDate).NotEmpty();
        RuleFor(x => x.Time).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.SpecialRequirements).MaximumLength(1000);

        RuleFor(x => x)
            .Must(x => x.MassTypeId.HasValue || x.CeremonyTypeId.HasValue)
            .WithErrorCode(ErrorCodes.EventTypeRequired)
            .WithMessage("Either MassTypeId or CeremonyTypeId is required.");
    }
}
