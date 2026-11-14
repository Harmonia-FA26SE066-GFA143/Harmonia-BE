using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class ReviewDirectorNominationRequestValidator : AbstractValidator<ReviewDirectorNominationRequest>
{
    public ReviewDirectorNominationRequestValidator()
    {
        RuleFor(x => x.Note)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.DirectorNominationNoteRequired)
            .WithMessage("A reason is required.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
