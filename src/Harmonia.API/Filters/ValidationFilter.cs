using System.Text.Json;
using FluentValidation;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = Harmonia.Application.Exceptions.ValidationException;

namespace Harmonia.API.Filters;

/// <summary>
/// Runs the FluentValidation validator registered for each action argument and turns every
/// failure into a <see cref="ValidationException"/>, so all 400 responses share one shape.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, HashSet<string>>();

        // Binding failures such as malformed JSON or a wrong value type.
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count > 0)
            {
                // "$" is the JSON root, i.e. the whole body could not be read.
                var field = key.StartsWith("$.") ? key[2..] : key;
                Add(errors, field is "$" or "" ? "request" : field, ErrorCodes.ValidationFailed);
            }
        }

        if (errors.Count == 0)
        {
            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument is null ||
                    context.HttpContext.RequestServices.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType()))
                        is not IValidator validator)
                {
                    continue;
                }

                var result = await validator.ValidateAsync(
                    new ValidationContext<object>(argument),
                    context.HttpContext.RequestAborted);

                foreach (var failure in result.Errors)
                {
                    // Without .WithErrorCode(...) FluentValidation reports the validator's class name.
                    var code = failure.ErrorCode is { Length: > 0 } c && !c.EndsWith("Validator")
                        ? c
                        : ErrorCodes.ValidationFailed;
                    Add(errors, failure.PropertyName, code);
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(
                errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }

        await next();
    }

    private static void Add(Dictionary<string, HashSet<string>> errors, string field, string code)
    {
        var name = string.Join('.', field.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
        if (!errors.TryGetValue(name, out var codes))
        {
            errors[name] = codes = [];
        }

        codes.Add(code);
    }
}
