using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class RevokeMemberSkillRequestValidator : AbstractValidator<RevokeMemberSkillRequest>
{
    public RevokeMemberSkillRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithErrorCode(ErrorCodes.MemberSkillRevokeReasonRequired)
            .MaximumLength(500);
    }
}
