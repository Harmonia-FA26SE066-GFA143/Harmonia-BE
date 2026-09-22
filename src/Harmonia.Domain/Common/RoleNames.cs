namespace Harmonia.Domain.Common;

/// <summary>
/// Fixed role names used for seeding and <c>[Authorize(Roles = ...)]</c>. Must match the
/// <c>Role.Name</c> rows exactly, since role checks compare against these strings.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";

    public const string ParishPriest = "ParishPriest";

    public const string ChoirDirector = "ChoirDirector";

    public const string ChoirMember = "ChoirMember";
}
