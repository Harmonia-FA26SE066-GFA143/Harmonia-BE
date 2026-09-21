namespace Harmonia.Application.Common;

/// <summary>
/// Business error codes returned to clients. The full catalogue and the Vietnamese wording
/// live in doc/error-codes.md, which is the contract with the frontend.
/// Only codes that are not tied to a single feature are declared here up front; a
/// feature-specific code is added when that feature is actually built.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Shared by every endpoint.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    public const string Unauthorized = "UNAUTHORIZED";

    public const string Forbidden = "FORBIDDEN";

    public const string NotFound = "NOT_FOUND";

    public const string Conflict = "CONFLICT";

    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>Shared by the nine Admin-configured lookup tables.</summary>
    public const string LookupNotFound = "LOOKUP_NOT_FOUND";

    public const string LookupNameDuplicate = "LOOKUP_NAME_DUPLICATE";

    public const string LookupInUse = "LOOKUP_IN_USE";

    public const string LookupInactive = "LOOKUP_INACTIVE";
}
