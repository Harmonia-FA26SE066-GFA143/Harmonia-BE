using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; song existence and active state are checked in RehearsalSongService.</summary>
public class UpdateRehearsalSongsRequestValidator : AbstractValidator<UpdateRehearsalSongsRequest>
{
    public UpdateRehearsalSongsRequestValidator()
    {
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.Select(i => i.SongId).Distinct().Count() == x.Count)
            .WithErrorCode(ErrorCodes.RehearsalSongDuplicate);

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.SongId).NotEmpty();
            i.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}
