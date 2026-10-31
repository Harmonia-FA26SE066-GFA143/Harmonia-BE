using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SaveCatalogItemRequestValidator : AbstractValidator<SaveCatalogItemRequest>
{
    public SaveCatalogItemRequestValidator()
    {
        // 50 is the shortest Name column among the catalogs sharing this request (SkillCategory).
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}
