using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchMyPracticeAssignmentsRequestValidator : AbstractValidator<SearchMyPracticeAssignmentsRequest>
{
    public SearchMyPracticeAssignmentsRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
