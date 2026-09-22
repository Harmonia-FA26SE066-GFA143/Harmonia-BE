namespace Harmonia.Application.Common.Models;

/// <summary>Outcome of a service operation that returns <typeparamref name="T"/> on success.</summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? code, string? message)
        : base(isSuccess, code, message)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public static new Result<T> Failure(string code, string? message = null) => new(false, default, code, message);
}
