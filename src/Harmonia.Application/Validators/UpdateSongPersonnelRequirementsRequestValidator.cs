using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; skill existence and active state are checked in SongPersonnelRequirementService.</summary>
public class UpdateSongPersonnelRequirementsRequestValidator : AbstractValidator<UpdateSongPersonnelRequirementsRequest>
{
    public const int MaxRequiredCount = 50;

    public UpdateSongPersonnelRequirementsRequestValidator()
    {
        RuleFor(x => x.Requirements).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => BeDistinct(x.Select(r => r.SkillId))).WithErrorCode(ErrorCodes.PersonnelRequirementDuplicate);

        RuleForEach(x => x.Requirements).ChildRules(r =>
        {
            r.RuleFor(x => x.SkillId).NotEmpty();
            r.RuleFor(x => x.RequiredCount).InclusiveBetween(1, MaxRequiredCount).WithErrorCode(ErrorCodes.PersonnelRequiredCountInvalid);
        });
    }

    private static bool BeDistinct(IEnumerable<Guid> ids)
    {
        var seen = new HashSet<Guid>();
        return ids.All(seen.Add);
    }
}
