using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdatePracticeAssignmentRequestValidator : AbstractValidator<UpdatePracticeAssignmentRequest>
{
    public UpdatePracticeAssignmentRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Instruction).MaximumLength(1000);

        RuleFor(x => x.DueDate)
            .GreaterThan(_ => DateTime.UtcNow)
            .WithErrorCode(ErrorCodes.PracticeDueDateInPast)
            .WithMessage("Due date cannot be in the past.");
    }
}
