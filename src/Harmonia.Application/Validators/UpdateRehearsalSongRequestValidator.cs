using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>One item of a list request; the parent validator applies it to each element.</summary>
public class UpdateRehearsalSongRequestValidator : AbstractValidator<UpdateRehearsalSongRequest>
{
    public UpdateRehearsalSongRequestValidator()
    {
        RuleFor(x => x.SongId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
