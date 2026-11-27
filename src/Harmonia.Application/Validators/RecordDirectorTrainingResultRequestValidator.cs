using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class RecordDirectorTrainingResultRequestValidator : AbstractValidator<RecordDirectorTrainingResultRequest>
{
    public RecordDirectorTrainingResultRequestValidator()
    {
        RuleFor(x => x.Result).IsInEnum().NotEqual(DirectorTrainingResult.InProgress);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
