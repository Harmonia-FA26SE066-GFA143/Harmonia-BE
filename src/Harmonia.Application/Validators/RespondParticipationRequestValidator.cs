using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class RespondParticipationRequestValidator : AbstractValidator<RespondParticipationRequest>
{
    public RespondParticipationRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .NotEqual(ParticipationStatus.Invited);

        RuleFor(x => x.Note).MaximumLength(300);
    }
}