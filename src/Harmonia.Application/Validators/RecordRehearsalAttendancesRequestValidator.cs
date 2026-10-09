using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; member existence and active state are checked in RehearsalAttendanceService.</summary>
public class RecordRehearsalAttendancesRequestValidator : AbstractValidator<RecordRehearsalAttendancesRequest>
{
    public RecordRehearsalAttendancesRequestValidator()
    {
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(x => x.Select(i => i.MemberId).Distinct().Count() == x.Count)
            .WithErrorCode(ErrorCodes.AttendanceMemberDuplicate);

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.MemberId).NotEmpty();
            i.RuleFor(x => x.Status).IsInEnum();
        });
    }
}
