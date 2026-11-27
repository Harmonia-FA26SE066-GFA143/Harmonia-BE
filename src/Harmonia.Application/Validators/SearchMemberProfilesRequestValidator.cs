using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchMemberProfilesRequestValidator : AbstractValidator<SearchMemberProfilesRequest>
{
    public SearchMemberProfilesRequestValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.SkillId).NotEqual(Guid.Empty);
    }
}
