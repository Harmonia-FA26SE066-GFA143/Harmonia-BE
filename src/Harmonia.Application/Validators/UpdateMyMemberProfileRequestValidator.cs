using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdateMyMemberProfileRequestValidator : AbstractValidator<UpdateMyMemberProfileRequest>
{
    public UpdateMyMemberProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.SaintName).MaximumLength(50);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => VietnamTime.Today);
    }
}
