using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class UpdateSongListItemsRequestValidator : AbstractValidator<UpdateSongListItemsRequest>
{
    public UpdateSongListItemsRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.SongListEmpty);

        RuleForEach(x => x.Items).SetValidator(new UpdateSongListItemRequestValidator());

        RuleFor(x => x.Items)
            // A slot may hold several songs (e.g. two at Communion), but the same song only once.
            .Must(items => items.Select(i => (i.SlotId, i.SongId)).Distinct().Count() == items.Count)
            .WithErrorCode(ErrorCodes.SongListSlotDuplicate)
            .WithMessage("A song may only appear once in the same slot.");
    }
}