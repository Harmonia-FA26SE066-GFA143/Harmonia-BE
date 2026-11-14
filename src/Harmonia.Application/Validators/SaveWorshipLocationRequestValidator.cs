using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SaveWorshipLocationRequestValidator : AbstractValidator<SaveWorshipLocationRequest>
{
    public SaveWorshipLocationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}
