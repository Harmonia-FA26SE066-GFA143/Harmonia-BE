using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithErrorCode(ErrorCodes.AuthResetTokenInvalid);

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .StrongPassword();
    }
}
