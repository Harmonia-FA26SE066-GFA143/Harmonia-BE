using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>One item of a list request; the parent validator applies it to each element.</summary>
public class UpdateSongListItemRequestValidator : AbstractValidator<UpdateSongListItemRequest>
{
    public UpdateSongListItemRequestValidator()
    {
        RuleFor(x => x.SongId).NotEmpty();
        RuleFor(x => x.SlotId).NotEmpty();
    }
}
