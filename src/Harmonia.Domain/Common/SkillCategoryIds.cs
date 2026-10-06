namespace Harmonia.Domain.Common;

/// <summary>
/// Fixed ids of the seeded SkillCategory rows (SkillCategoryConfiguration). Code compares against
/// these ids, never against names, so renaming a category in the admin screen breaks nothing.
/// </summary>
public static class SkillCategoryIds
{
    public static readonly Guid Vocal = new("c4829abd-bb1d-4c6c-b401-9a177a88e66a");

    public static readonly Guid Instrument = new("0904ad8b-87f2-46c5-9938-f6cfbf0fa70b");

    public static readonly Guid Solo = new("550a67bb-0393-4bc4-8fe0-d7a453871f81");

    public static readonly Guid Psalm = new("a1c322c5-14af-4ca4-892a-b6e8d0eacbbd");

    public static readonly Guid ConductingSupport = new("e2722720-3745-48ea-9e8e-14ec7a63907d");
}
