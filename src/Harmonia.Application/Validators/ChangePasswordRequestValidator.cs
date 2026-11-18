using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithErrorCode(ErrorCodes.AuthPasswordRequired);

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .StrongPassword();
    }
}
