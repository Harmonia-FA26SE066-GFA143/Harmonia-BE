namespace Harmonia.Application.Common.Models;

/// <summary>
/// Outcome of a service operation that has no return value. A failure carries an
/// <c>ErrorCodes</c> value; the controller maps it to an HTTP status.
/// See docs/error-codes.md for which errors go through <see cref="Result"/> vs. exceptions.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    /// <summary>Machine-readable facts about the failure (e.g. the id at fault), returned as <c>details</c>.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; }

    protected Result(bool isSuccess, string? code, string? message, IReadOnlyDictionary<string, string>? details = null)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Details = details;
    }

    public static Result Success() => new(true, null, null);

    public static Result Failure(
        string code, string? message = null, IReadOnlyDictionary<string, string>? details = null) =>
        new(false, code, message, details);
}
