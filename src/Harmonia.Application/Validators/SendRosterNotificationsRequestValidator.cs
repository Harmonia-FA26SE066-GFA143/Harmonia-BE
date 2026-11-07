using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; whether each member is on the roster is checked in RosterService.</summary>
public class SendRosterNotificationsRequestValidator : AbstractValidator<SendRosterNotificationsRequest>
{
    public SendRosterNotificationsRequestValidator()
    {
        RuleFor(x => x.MemberIds).NotNull();
        RuleForEach(x => x.MemberIds).NotEmpty();
    }
}
