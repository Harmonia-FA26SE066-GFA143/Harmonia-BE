using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class RejectChoirJoinRequestRequestValidator : AbstractValidator<RejectChoirJoinRequestRequest>
{
    public RejectChoirJoinRequestRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
