using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; skill existence and active state are checked in EventPersonnelRequirementService.</summary>
public class UpdateEventPersonnelRequirementsRequestValidator : AbstractValidator<UpdateEventPersonnelRequirementsRequest>
{
    public UpdateEventPersonnelRequirementsRequestValidator()
    {
        RuleFor(x => x.Requirements).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.Select(r => r.SkillId).Distinct().Count() == x.Count).WithErrorCode(ErrorCodes.PersonnelRequirementDuplicate);

        RuleForEach(x => x.Requirements).ChildRules(item =>
        {
            item.RuleFor(x => x.SkillId).NotEmpty();
            item.RuleFor(x => x.RequiredCount)
                .InclusiveBetween(1, UpdateSongPersonnelRequirementsRequestValidator.MaxRequiredCount)
                .WithErrorCode(ErrorCodes.PersonnelRequiredCountInvalid);
        });
    }
}
