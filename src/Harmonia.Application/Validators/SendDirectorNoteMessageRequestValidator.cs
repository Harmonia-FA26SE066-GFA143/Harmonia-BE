using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the sender and recipient being Parish Priest and Choir Director is checked in DirectorNoteService.</summary>
public class SendDirectorNoteMessageRequestValidator : AbstractValidator<SendDirectorNoteMessageRequest>
{
    public SendDirectorNoteMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);

        RuleFor(x => x.ToUserId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.DirectorNoteRecipientInvalid);
    }
}
