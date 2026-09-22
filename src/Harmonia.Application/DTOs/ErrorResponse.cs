using System.Text.Json.Serialization;

namespace Harmonia.Application.DTOs;

/// <summary>Shape of every error response, per doc/error-codes.md.</summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errors = null);
