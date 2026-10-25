using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Common;

/// <summary>
/// What the system knows about one setting key: its type, the value it starts with, the label and group the
/// settings page shows it under, whether every signed-in role may read it (<paramref name="IsPublic"/>) or even a
/// visitor who has not signed in (<paramref name="IsAnonymous"/>, only together with IsPublic), and for integers
/// the range the Admin may pick from.
/// </summary>
public record SystemSettingDefinition(
    string Key,
    SettingDataType DataType,
    string DefaultValue,
    string DisplayName,
    string Group,
    string Description,
    bool IsPublic = false,
    bool IsAnonymous = false,
    int? MinValue = null,
    int? MaxValue = null);
