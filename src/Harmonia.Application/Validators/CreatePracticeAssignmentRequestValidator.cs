using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class CreatePracticeAssignmentRequestValidator : AbstractValidator<CreatePracticeAssignmentRequest>
{
    public CreatePracticeAssignmentRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Instruction).MaximumLength(1000);
        RuleFor(x => x.Scope).IsInEnum();

        RuleFor(x => x.DueDate)
            .GreaterThan(_ => DateTime.UtcNow)
            .WithErrorCode(ErrorCodes.PracticeDueDateInPast)
            .WithMessage("Due date cannot be in the past.");

        RuleFor(x => x.SkillIds)
            .NotEmpty()
            .When(x => x.Scope == AssignmentScope.SkillGroup)
            .WithErrorCode(ErrorCodes.PracticeTargetRequired)
            .WithMessage("At least one skill is required for a skill group assignment.");

        RuleFor(x => x.MemberIds)
            .NotEmpty()
            .When(x => x.Scope == AssignmentScope.Individual)
            .WithErrorCode(ErrorCodes.PracticeTargetRequired)
            .WithMessage("At least one member is required for an individual assignment.");
    }
}
