using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape of the lines only; the audio length and the song of both materials are checked in MusicMaterialService.</summary>
public class UpdateTimedLyricsRequestValidator : AbstractValidator<UpdateTimedLyricsRequest>
{
    public const int MaxLines = 500;

    public const int MaxLineLength = 500;

    public UpdateTimedLyricsRequestValidator()
    {
        RuleFor(x => x.LyricsMaterialId).NotEmpty();
        RuleFor(x => x.Granularity).IsInEnum();
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= MaxLines)
            .WithMessage($"At most {MaxLines} lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Text).NotEmpty().MaximumLength(MaxLineLength);
            line.RuleFor(l => l.StartSeconds).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.EndSeconds).GreaterThan(l => l.StartSeconds).WithErrorCode(ErrorCodes.TimedLyricsLinesInvalid);
        });

        // Lines play one after another: orders strictly increase and a line starts no earlier than the previous one ends.
        RuleFor(x => x.Lines)
            .Must(lines => lines.Zip(lines.Skip(1)).All(p => p.Second.Order > p.First.Order && p.Second.StartSeconds >= p.First.EndSeconds))
            .WithErrorCode(ErrorCodes.TimedLyricsLinesInvalid)
            .WithMessage("Lines must be in increasing order and must not overlap.");
    }
}
