using FluentValidation;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public static class PasswordRuleExtensions
{
    /// <summary>At least 8 characters with at least one letter and one digit.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithErrorCode(ErrorCodes.AuthPasswordRequired)
            .Must(password => password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit))
            .WithErrorCode(ErrorCodes.AuthPasswordTooWeak);
}
