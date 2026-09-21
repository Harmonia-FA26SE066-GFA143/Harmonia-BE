namespace Harmonia.Domain.Exceptions;

/// <summary>
/// Raised when an entity is asked to break one of its own business rules.
/// <see cref="Code"/> is an <c>ErrorCodes</c> value; the API layer maps it to an HTTP status.
/// </summary>
public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
