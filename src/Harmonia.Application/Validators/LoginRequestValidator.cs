using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode(ErrorCodes.AuthEmailRequired)
            .EmailAddress().WithErrorCode(ErrorCodes.AuthEmailInvalidFormat);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode(ErrorCodes.AuthPasswordRequired);
    }
}
