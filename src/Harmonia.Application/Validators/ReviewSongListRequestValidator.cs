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
    }
}