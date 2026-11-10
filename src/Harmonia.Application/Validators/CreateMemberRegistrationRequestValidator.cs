using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class CreateMemberRegistrationRequestValidator : AbstractValidator<CreateMemberRegistrationRequest>
{
    public CreateMemberRegistrationRequestValidator()
    {
        // Same limits as the User columns the Admin later creates from this row.
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.SaintName).MaximumLength(50);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
