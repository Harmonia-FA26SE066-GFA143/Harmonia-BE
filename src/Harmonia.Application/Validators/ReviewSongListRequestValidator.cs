using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class ReviewSongListRequestValidator : AbstractValidator<ReviewSongListRequest>
{
    public ReviewSongListRequestValidator()
    {
        RuleFor(x => x.Decision).IsInEnum();

        RuleFor(x => x.Notes)
            .NotEmpty()
            .When(x => x.Decision is ReviewDecision.Reject or ReviewDecision.RequestRevision)
            .WithErrorCode(ErrorCodes.ReviewNotesRequired);

        // Matches the column length, so a long note is a 400 instead of a database error.
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}