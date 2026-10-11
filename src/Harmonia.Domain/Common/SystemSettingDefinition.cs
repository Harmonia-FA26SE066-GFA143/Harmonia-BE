using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Common;

/// <summary>
/// What the system knows about one setting key: its type, the value it starts with, whether every signed-in
/// role may read it, and for integers the range the Admin may pick from.
/// </summary>
public record SystemSettingDefinition(
    string Key,
    SettingDataType DataType,
    string DefaultValue,
    string Description,
    bool IsPublic = false,
    int? MinValue = null,
    int? MaxValue = null);
