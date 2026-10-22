using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>
/// Form fields only. The file is checked in MusicMaterialService: this validator runs before the
/// action, when the file has not been attached yet.
/// </summary>
public class UploadMusicMaterialRequestValidator : AbstractValidator<UploadMusicMaterialRequest>
{
    public UploadMusicMaterialRequestValidator()
    {
        RuleFor(x => x.SongId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialType).IsInEnum();
    }
}
