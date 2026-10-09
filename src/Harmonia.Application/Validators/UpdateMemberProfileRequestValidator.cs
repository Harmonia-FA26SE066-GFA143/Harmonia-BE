using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdateMemberProfileRequestValidator : AbstractValidator<UpdateMemberProfileRequest>
{
    public UpdateMemberProfileRequestValidator()
    {
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => VietnamTime.Today);
        RuleFor(x => x.JoinedDate)
            .NotEmpty()
            .LessThanOrEqualTo(_ => VietnamTime.Today)
            .WithErrorCode(ErrorCodes.MemberJoinedDateInFuture);
        RuleFor(x => x.Status).IsInEnum();
    }
}
