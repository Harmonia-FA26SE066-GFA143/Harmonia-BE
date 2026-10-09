using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; event existence and each recipient being an active Choir Director are checked in DirectorNoteService.</summary>
public class CreateDirectorNoteRequestValidator : AbstractValidator<CreateDirectorNoteRequest>
{
    public CreateDirectorNoteRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);

        RuleFor(x => x.ToUserIds)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.DirectorNoteRecipientInvalid)
            .WithMessage("At least one Choir Director is required.");
        RuleForEach(x => x.ToUserIds).NotEmpty();

        RuleFor(x => x)
            .Must(x => x.NoteDate is not null || x.EventId is not null)
            .WithName(nameof(CreateDirectorNoteRequest.EventId))
            .WithErrorCode(ErrorCodes.DirectorNoteTargetRequired)
            .WithMessage("Either a note date or an event is required.");
    }
}
