using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchMaterialLearningProgressRequestValidator : AbstractValidator<SearchMaterialLearningProgressRequest>
{
    public SearchMaterialLearningProgressRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
