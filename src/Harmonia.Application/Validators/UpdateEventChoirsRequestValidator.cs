using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>
/// Shape only; each choir existing and being active is checked in ChoirService. An empty list is allowed and takes
/// no lead; otherwise the lead must be one of the listed choirs.
/// </summary>
public class UpdateEventChoirsRequestValidator : AbstractValidator<UpdateEventChoirsRequest>
{
    public UpdateEventChoirsRequestValidator()
    {
        RuleFor(x => x.ChoirIds).NotNull();
        RuleForEach(x => x.ChoirIds).NotEmpty();
        RuleFor(x => x.LeadChoirId)
            .Must((request, leadId) => request.ChoirIds is null || request.ChoirIds.Count == 0
                ? leadId is null
                : leadId is { } id && request.ChoirIds.Contains(id))
            .WithErrorCode(ErrorCodes.ChoirLeadInvalid)
            .WithMessage("LeadChoirId must be one of ChoirIds, and empty when ChoirIds is empty.");
    }
}
