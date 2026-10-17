using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; event, requirement and member eligibility are checked in RosterService.</summary>
public class CreateRosterAssignmentRequestValidator : AbstractValidator<CreateRosterAssignmentRequest>
{
    public CreateRosterAssignmentRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.SongListItemId).NotEmpty();
        RuleFor(x => x.SkillId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}
