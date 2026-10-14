using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SearchUsersRequestValidator : AbstractValidator<SearchUsersRequest>
{
    public SearchUsersRequestValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(100);
    }
}
