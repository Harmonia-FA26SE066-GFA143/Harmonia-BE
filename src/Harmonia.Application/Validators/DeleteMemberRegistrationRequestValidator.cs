using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class DeleteMemberRegistrationRequestValidator : AbstractValidator<DeleteMemberRegistrationRequest>
{
    public DeleteMemberRegistrationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
