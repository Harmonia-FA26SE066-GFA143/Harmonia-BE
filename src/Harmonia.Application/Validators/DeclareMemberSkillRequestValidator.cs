using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class DeclareMemberSkillRequestValidator : AbstractValidator<DeclareMemberSkillRequest>
{
    public DeclareMemberSkillRequestValidator()
    {
        RuleFor(x => x.SkillId).NotEmpty();
        RuleFor(x => x.Level).IsInEnum();
    }
}
