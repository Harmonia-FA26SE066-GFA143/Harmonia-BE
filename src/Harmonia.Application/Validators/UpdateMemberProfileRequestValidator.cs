using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdateMemberProfileRequestValidator : AbstractValidator<UpdateMemberProfileRequest>
{
    public UpdateMemberProfileRequestValidator()
    {
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Now));
        RuleFor(x => x.JoinedDate)
            .NotEmpty()
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Now))
            .WithErrorCode(ErrorCodes.MemberJoinedDateInFuture);
        RuleFor(x => x.Status).IsInEnum();
    }
}
