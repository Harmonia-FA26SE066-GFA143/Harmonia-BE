using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>One item of a list request; the parent validator applies it to each element.</summary>
public class RecordRehearsalAttendanceRequestValidator : AbstractValidator<RecordRehearsalAttendanceRequest>
{
    public RecordRehearsalAttendanceRequestValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
