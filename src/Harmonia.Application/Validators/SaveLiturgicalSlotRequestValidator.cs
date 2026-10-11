using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SaveLiturgicalSlotRequestValidator : AbstractValidator<SaveLiturgicalSlotRequest>
{
    public SaveLiturgicalSlotRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultOrder).GreaterThanOrEqualTo(0);
    }
}
