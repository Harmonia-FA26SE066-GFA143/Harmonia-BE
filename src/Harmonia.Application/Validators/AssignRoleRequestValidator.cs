using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty()
            .Must(r => r is RoleNames.Admin or RoleNames.ParishPriest
                        or RoleNames.ChoirDirector or RoleNames.ChoirMember);
    }
}