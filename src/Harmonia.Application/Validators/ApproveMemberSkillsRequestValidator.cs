using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class ApproveMemberSkillsRequestValidator : AbstractValidator<ApproveMemberSkillsRequest>
{
    public ApproveMemberSkillsRequestValidator()
    {
        // One page of the pending list; each id costs a review, a notification and an email.
        RuleFor(x => x.Ids).NotEmpty().Must(x => x.Count <= 50).WithMessage("At most 50 ids.");
    }
}
