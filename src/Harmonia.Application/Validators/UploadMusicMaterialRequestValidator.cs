using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

/// <summary>
/// Form fields only. The file is checked in MusicMaterialService: this validator runs before the
/// action, when the file has not been attached yet.
/// </summary>
public class UploadMusicMaterialRequestValidator : AbstractValidator<UploadMusicMaterialRequest>
{
    public const int MaxLyricsTextLength = 20_000;

    public UploadMusicMaterialRequestValidator()
    {
        RuleFor(x => x.SongId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialType).IsInEnum();
        RuleFor(x => x.Instrument).IsInEnum();
        RuleFor(x => x.LyricsText).MaximumLength(MaxLyricsTextLength);
        RuleFor(x => x.LyricsText).Empty().When(x => x.MaterialType != MaterialType.Lyrics)
            .WithErrorCode(ErrorCodes.MaterialLyricsSourceInvalid);
    }
}
