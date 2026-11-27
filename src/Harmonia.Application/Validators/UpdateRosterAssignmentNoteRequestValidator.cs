using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class UpdateRosterAssignmentNoteRequestValidator : AbstractValidator<UpdateRosterAssignmentNoteRequest>
{
    public UpdateRosterAssignmentNoteRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(200);
    }
}
