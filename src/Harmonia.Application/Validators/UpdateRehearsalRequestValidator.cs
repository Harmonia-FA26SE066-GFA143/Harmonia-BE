using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdateRehearsalRequestValidator : AbstractValidator<UpdateRehearsalRequest>
{
    public UpdateRehearsalRequestValidator()
    {
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty();

        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithErrorCode(ErrorCodes.RehearsalTimeInvalid)
            .WithMessage("EndTime must be after StartTime.")
            .When(x => x.StartTime != default && x.EndTime != default);

        RuleFor(x => x.EventId).NotEmpty().When(x => x.EventId.HasValue);
        RuleFor(x => x.LocationId).NotEmpty().When(x => x.LocationId.HasValue);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
