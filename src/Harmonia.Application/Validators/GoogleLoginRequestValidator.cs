using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithErrorCode(ErrorCodes.AuthGoogleTokenRequired);

        RuleFor(x => x.DeviceId).MaximumLength(100);
    }
}
