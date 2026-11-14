using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SaveLiturgicalNameTranslationRequestValidator : AbstractValidator<SaveLiturgicalNameTranslationRequest>
{
    public SaveLiturgicalNameTranslationRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.EnglishName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VietnameseName).NotEmpty().MaximumLength(200);
    }
}
