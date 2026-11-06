using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

/// <summary>
/// Shape only; each choir existing and being active is checked in ChoirService. The list cannot be empty, and the lead
/// must be one of the listed choirs.
/// </summary>
public class UpdateEventChoirsRequestValidator : AbstractValidator<UpdateEventChoirsRequest>
{
    public UpdateEventChoirsRequestValidator()
    {
        RuleFor(x => x.ChoirIds)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.LiturgicalEventChoirRequired)
            .WithMessage("At least one choir is required.");
        RuleForEach(x => x.ChoirIds).NotEmpty();
        RuleFor(x => x.LeadChoirId)
            .Must((request, leadId) => leadId is { } id && request.ChoirIds.Contains(id))
            .When(x => x.ChoirIds is { Count: > 0 })
            .WithErrorCode(ErrorCodes.ChoirLeadInvalid)
            .WithMessage("LeadChoirId must be one of ChoirIds.");
    }
}
