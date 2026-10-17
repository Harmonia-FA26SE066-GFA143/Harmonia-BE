using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class ReviewPracticeSubmissionRequestValidator : AbstractValidator<ReviewPracticeSubmissionRequest>
{
    public ReviewPracticeSubmissionRequestValidator()
    {
        RuleFor(x => x.Result)
            .Must(x => x is SubmissionStatus.Passed or SubmissionStatus.NeedsRevision)
            .WithMessage("Result must be Passed or NeedsRevision.");

        RuleFor(x => x.Comment).MaximumLength(1000);

        RuleFor(x => x.Comment)
            .NotEmpty()
            .When(x => x.Result == SubmissionStatus.NeedsRevision)
            .WithMessage("A comment is required when the submission needs revision.");
    }
}
