using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class RejectMemberSkillRequestValidator : AbstractValidator<RejectMemberSkillRequest>
{
    public RejectMemberSkillRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithErrorCode(ErrorCodes.MemberSkillRejectReasonRequired)
            .MaximumLength(500);
    }
}
