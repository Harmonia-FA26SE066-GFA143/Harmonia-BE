using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; whether the text fits the setting's type and range is checked in SystemSettingService.</summary>
public class UpdateSystemSettingRequestValidator : AbstractValidator<UpdateSystemSettingRequest>
{
    public UpdateSystemSettingRequestValidator()
    {
        RuleFor(x => x.Value).NotNull().MaximumLength(1000);
    }
}
