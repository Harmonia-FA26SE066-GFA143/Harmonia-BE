using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Lengths mirror SongConfiguration.</summary>
public class CreateSongRequestValidator : AbstractValidator<CreateSongRequest>
{
    public CreateSongRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Composer).MaximumLength(150);
        RuleFor(x => x.Lyricist).MaximumLength(150);
        RuleFor(x => x.MusicalKey).MaximumLength(10);
        RuleFor(x => x.Tempo).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
