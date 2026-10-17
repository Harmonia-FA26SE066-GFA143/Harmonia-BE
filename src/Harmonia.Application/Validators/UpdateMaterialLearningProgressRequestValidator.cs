using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Validators;

public class UpdateMaterialLearningProgressRequestValidator : AbstractValidator<UpdateMaterialLearningProgressRequest>
{
    public UpdateMaterialLearningProgressRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is LearningStatus.Learned or LearningStatus.NeedsPractice)
            .WithErrorCode(ErrorCodes.MaterialLearningStatusInvalid);
    }
}
