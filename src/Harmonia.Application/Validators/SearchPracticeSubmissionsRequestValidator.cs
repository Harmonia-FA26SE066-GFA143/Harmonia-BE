using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchPracticeSubmissionsRequestValidator : AbstractValidator<SearchPracticeSubmissionsRequest>
{
    public SearchPracticeSubmissionsRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
