using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class ReplaceRosterAssignmentRequestValidator : AbstractValidator<ReplaceRosterAssignmentRequest>
{
    public ReplaceRosterAssignmentRequestValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
    }
}
