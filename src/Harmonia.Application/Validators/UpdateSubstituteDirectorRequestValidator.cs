using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the substitute being an active Choir Director is checked in ChoirService.</summary>
public class UpdateSubstituteDirectorRequestValidator : AbstractValidator<UpdateSubstituteDirectorRequest>
{
    public UpdateSubstituteDirectorRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.DirectorId is not null)
            .WithErrorCode(ErrorCodes.ChoirChangeReasonRequired)
            .WithMessage("Reason is required when setting a substitute director.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
