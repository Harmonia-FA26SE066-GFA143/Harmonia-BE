using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchMyPracticeSubmissionsRequestValidator : AbstractValidator<SearchMyPracticeSubmissionsRequest>
{
    public SearchMyPracticeSubmissionsRequestValidator()
    {
        RuleFor(x => x.AssignmentId).NotEqual(Guid.Empty);
    }
}
