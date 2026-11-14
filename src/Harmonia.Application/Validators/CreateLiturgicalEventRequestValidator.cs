using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class CreateLiturgicalEventRequestValidator : AbstractValidator<CreateLiturgicalEventRequest>
{
    public CreateLiturgicalEventRequestValidator()
    {
        RuleFor(x => x.EventDate).NotEmpty();
        RuleFor(x => x.Time).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.SpecialRequirements).MaximumLength(1000);

        RuleFor(x => x)
            .Must(x => x.MassTypeId.HasValue || x.CeremonyTypeId.HasValue)
            .WithErrorCode(ErrorCodes.LiturgicalEventTypeRequired)
            .WithMessage("Either MassTypeId or CeremonyTypeId is required.");

        RuleFor(x => x.ChoirIds)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.LiturgicalEventChoirRequired)
            .WithMessage("At least one choir is required.");
        RuleForEach(x => x.ChoirIds).NotEmpty();
        RuleFor(x => x.LeadChoirId)
            .Must((request, leadId) => leadId is { } id && request.ChoirIds.Contains(id))
            .When(x => x.ChoirIds is { Count: > 0 })
            .WithErrorCode(ErrorCodes.ChoirLeadInvalid)
            .WithMessage("LeadChoirId must be one of ChoirIds.");
    }
}