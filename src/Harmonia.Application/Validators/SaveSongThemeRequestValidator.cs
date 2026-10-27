using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; the parent's existence and the no-cycle rule are checked in LookupService.</summary>
public class SaveSongThemeRequestValidator : AbstractValidator<SaveSongThemeRequest>
{
    public SaveSongThemeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.ParentId).NotEqual(Guid.Empty);
    }
}
