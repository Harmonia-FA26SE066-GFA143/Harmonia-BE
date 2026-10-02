using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; existence, active state and skill category are checked in SongService.</summary>
public class UpdateSongClassificationRequestValidator : AbstractValidator<UpdateSongClassificationRequest>
{
    public UpdateSongClassificationRequestValidator()
    {
        RuleFor(x => x.LiturgicalSeasonIds).Cascade(CascadeMode.Stop).NotNull().Must(BeDistinct).WithErrorCode(ErrorCodes.SongClassificationDuplicate);
        RuleFor(x => x.MassTypeIds).Cascade(CascadeMode.Stop).NotNull().Must(BeDistinct).WithErrorCode(ErrorCodes.SongClassificationDuplicate);
        RuleFor(x => x.CeremonyTypeIds).Cascade(CascadeMode.Stop).NotNull().Must(BeDistinct).WithErrorCode(ErrorCodes.SongClassificationDuplicate);
        RuleFor(x => x.SongThemeIds).Cascade(CascadeMode.Stop).NotNull().Must(BeDistinct).WithErrorCode(ErrorCodes.SongClassificationDuplicate);

        RuleForEach(x => x.LiturgicalSeasonIds).NotEmpty();
        RuleForEach(x => x.MassTypeIds).NotEmpty();
        RuleForEach(x => x.CeremonyTypeIds).NotEmpty();
        RuleForEach(x => x.SongThemeIds).NotEmpty();

        RuleFor(x => x.VocalRequirements).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => BeDistinct(x.Select(r => r.SkillId))).WithErrorCode(ErrorCodes.SongSkillRequirementDuplicate);
        RuleFor(x => x.InstrumentRequirements).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => BeDistinct(x.Select(r => r.SkillId))).WithErrorCode(ErrorCodes.SongSkillRequirementDuplicate);

        RuleForEach(x => x.VocalRequirements).ChildRules(r => r.RuleFor(x => x.SkillId).NotEmpty());
        RuleForEach(x => x.InstrumentRequirements).ChildRules(r => r.RuleFor(x => x.SkillId).NotEmpty());
    }

    private static bool BeDistinct(IEnumerable<Guid> ids)
    {
        var seen = new HashSet<Guid>();
        return ids.All(seen.Add);
    }
}
