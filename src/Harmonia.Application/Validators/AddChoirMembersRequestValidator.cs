using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; each member existing and being active is checked in ChoirService.</summary>
public class AddChoirMembersRequestValidator : AbstractValidator<AddChoirMembersRequest>
{
    public AddChoirMembersRequestValidator()
    {
        RuleFor(x => x.MemberIds)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.ChoirMemberInvalid)
            .WithMessage("At least one member is required.");
        RuleForEach(x => x.MemberIds).NotEmpty();
    }
}
