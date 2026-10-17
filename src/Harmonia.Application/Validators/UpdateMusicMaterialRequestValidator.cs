using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class UpdateMusicMaterialRequestValidator : AbstractValidator<UpdateMusicMaterialRequest>
{
    public UpdateMusicMaterialRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}
