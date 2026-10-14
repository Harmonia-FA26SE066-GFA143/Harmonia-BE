using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>One item of a list request; the parent validator applies it to each element.</summary>
public class UpdateSongSkillRequirementRequestValidator : AbstractValidator<UpdateSongSkillRequirementRequest>
{
    public UpdateSongSkillRequirementRequestValidator()
    {
        RuleFor(x => x.SkillId).NotEmpty();
    }
}
