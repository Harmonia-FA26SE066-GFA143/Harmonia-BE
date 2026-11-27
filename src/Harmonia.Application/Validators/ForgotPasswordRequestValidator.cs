using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.AuthEmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.AuthEmailInvalidFormat);

        RuleFor(x => x.Platform)
            .IsInEnum().WithErrorCode(ErrorCodes.ValidationFailed);
    }
}
