namespace Harmonia.Application.Common.Models;

/// <summary>
/// Outcome of a service operation that has no return value. A failure carries an
/// <c>ErrorCodes</c> value; the controller maps it to an HTTP status.
/// See doc/error-codes.md for which errors go through <see cref="Result"/> vs. exceptions.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    protected Result(bool isSuccess, string? code, string? message)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
    }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string code, string? message = null) => new(false, code, message);
}
