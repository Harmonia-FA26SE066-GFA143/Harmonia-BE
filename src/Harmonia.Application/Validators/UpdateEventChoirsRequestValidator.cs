using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

/// <summary>Shape only; each choir existing and being active is checked in ChoirService. An empty list is allowed.</summary>
public class UpdateEventChoirsRequestValidator : AbstractValidator<UpdateEventChoirsRequest>
{
    public UpdateEventChoirsRequestValidator()
    {
        RuleFor(x => x.ChoirIds).NotNull();
        RuleForEach(x => x.ChoirIds).NotEmpty();
    }
}
