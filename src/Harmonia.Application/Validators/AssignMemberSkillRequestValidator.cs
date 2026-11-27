using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the member being in the director's scope and the skill rules are checked in MemberSkillService.</summary>
public class AssignMemberSkillRequestValidator : AbstractValidator<AssignMemberSkillRequest>
{
    public AssignMemberSkillRequestValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.SkillId).NotEmpty();
    }
}
