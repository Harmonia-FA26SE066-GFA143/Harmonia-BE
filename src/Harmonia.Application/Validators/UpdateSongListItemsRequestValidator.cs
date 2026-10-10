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

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SongId).NotEmpty();
            item.RuleFor(i => i.SlotId).NotEmpty();
        });

        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.SlotId).Distinct().Count() == items.Count)
            .WithErrorCode(ErrorCodes.SongListSlotDuplicate)
            .WithMessage("Each slot may only appear once in the list.");
    }
}