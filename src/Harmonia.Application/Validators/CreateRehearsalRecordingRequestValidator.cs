using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>
/// Form fields only. The audio file is checked in RehearsalRecordingService: this validator runs before the
/// action, when the file has not been attached yet.
/// </summary>
public class CreateRehearsalRecordingRequestValidator : AbstractValidator<CreateRehearsalRecordingRequest>
{
    public CreateRehearsalRecordingRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.DurationSeconds).InclusiveBetween(1, 14400).When(x => x.DurationSeconds.HasValue);
    }
}
