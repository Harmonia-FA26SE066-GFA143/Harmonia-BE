namespace Harmonia.API.Filters;

/// <summary>
/// Marks an action a user may still call while they must replace the password the Admin emailed
/// (see <see cref="PasswordChangeRequiredFilter"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowWhenPasswordChangeRequiredAttribute : Attribute;
