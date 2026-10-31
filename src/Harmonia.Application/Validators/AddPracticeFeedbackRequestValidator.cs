using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class AddPracticeFeedbackRequestValidator : AbstractValidator<AddPracticeFeedbackRequest>
{
    public AddPracticeFeedbackRequestValidator()
    {
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(1000);

        RuleFor(x => x.Result)
            .Must(x => x is SubmissionStatus.Passed or SubmissionStatus.NeedsRevision)
            .When(x => x.Result.HasValue)
            .WithMessage("Result must be Passed or NeedsRevision.");
    }
}
