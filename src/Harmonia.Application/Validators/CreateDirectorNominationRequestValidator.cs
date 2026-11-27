using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the member being active and in the caller's scope is checked in DirectorNominationService.</summary>
public class CreateDirectorNominationRequestValidator : AbstractValidator<CreateDirectorNominationRequest>
{
    public CreateDirectorNominationRequestValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
