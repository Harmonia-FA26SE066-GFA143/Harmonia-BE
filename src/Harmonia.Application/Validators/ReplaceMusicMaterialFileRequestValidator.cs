using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Form fields only; the file is checked in MusicMaterialService, as for an upload.</summary>
public class ReplaceMusicMaterialFileRequestValidator : AbstractValidator<ReplaceMusicMaterialFileRequest>
{
    public ReplaceMusicMaterialFileRequestValidator()
    {
        RuleFor(x => x.Instrument).IsInEnum();
    }
}
