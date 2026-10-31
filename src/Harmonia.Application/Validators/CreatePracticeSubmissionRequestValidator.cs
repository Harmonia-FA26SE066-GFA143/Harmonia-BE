using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>
/// Form fields only. The audio file is checked in PracticeAssignmentService: this validator runs before the
/// action, when the file has not been attached yet.
/// </summary>
public class CreatePracticeSubmissionRequestValidator : AbstractValidator<CreatePracticeSubmissionRequest>
{
    public CreatePracticeSubmissionRequestValidator()
    {
        RuleFor(x => x.DurationSeconds).InclusiveBetween(1, 3600).When(x => x.DurationSeconds.HasValue);
    }
}
