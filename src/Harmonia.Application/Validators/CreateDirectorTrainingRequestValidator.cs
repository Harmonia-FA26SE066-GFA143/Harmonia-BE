using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class CreateDirectorTrainingRequestValidator : AbstractValidator<CreateDirectorTrainingRequest>
{
    public CreateDirectorTrainingRequestValidator()
    {
        RuleFor(x => x.CourseName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Organizer).MaximumLength(200);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null);
    }
}
