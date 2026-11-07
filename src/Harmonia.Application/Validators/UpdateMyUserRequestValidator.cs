using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class UpdateMyUserRequestValidator : AbstractValidator<UpdateMyUserRequest>
{
    public UpdateMyUserRequestValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}
