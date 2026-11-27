using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>One item of a list request; the parent validator applies it to each element.</summary>
public class UpdateSongPersonnelRequirementRequestValidator : AbstractValidator<UpdateSongPersonnelRequirementRequest>
{
    public UpdateSongPersonnelRequirementRequestValidator()
    {
        RuleFor(x => x.SkillId).NotEmpty();
        RuleFor(x => x.RequiredCount)
            .InclusiveBetween(1, UpdateSongPersonnelRequirementsRequestValidator.MaxRequiredCount)
            .WithErrorCode(ErrorCodes.PersonnelRequiredCountInvalid);
    }
}
