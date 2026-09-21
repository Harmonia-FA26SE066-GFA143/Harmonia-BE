namespace Harmonia.Application.Exceptions;

/// <summary>
/// Request validation failed. <see cref="Errors"/> maps a field name to the error codes raised for it.
/// </summary>
public class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
