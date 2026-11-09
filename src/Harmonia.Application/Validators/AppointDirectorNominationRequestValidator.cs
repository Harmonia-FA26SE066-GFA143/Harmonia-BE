using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class AppointDirectorNominationRequestValidator : AbstractValidator<AppointDirectorNominationRequest>
{
    public AppointDirectorNominationRequestValidator()
    {
        RuleFor(x => x.ChoirId).NotEmpty();
    }
}
