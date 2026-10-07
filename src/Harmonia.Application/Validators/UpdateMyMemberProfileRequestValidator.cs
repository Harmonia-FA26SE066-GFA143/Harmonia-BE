using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class UpdateMyMemberProfileRequestValidator : AbstractValidator<UpdateMyMemberProfileRequest>
{
    public UpdateMyMemberProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Now));
    }
}
