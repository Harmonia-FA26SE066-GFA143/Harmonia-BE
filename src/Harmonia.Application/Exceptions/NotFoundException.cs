namespace Harmonia.Application.Exceptions;

/// <summary>
/// Data that must exist is missing, which points to a system fault. A client sending an
/// unknown id is an expected case and goes through <c>Result.Failure(XXX_NOT_FOUND)</c> instead.
/// </summary>
public class NotFoundException(string message) : Exception(message);
