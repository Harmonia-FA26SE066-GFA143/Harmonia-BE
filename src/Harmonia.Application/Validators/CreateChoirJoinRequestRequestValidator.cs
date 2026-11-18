using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the choir being active and the member's pending rows are checked in ChoirJoinRequestService.</summary>
public class CreateChoirJoinRequestRequestValidator : AbstractValidator<CreateChoirJoinRequestRequest>
{
    public CreateChoirJoinRequestRequestValidator()
    {
        RuleFor(x => x.ChoirId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(500);
    }
}
