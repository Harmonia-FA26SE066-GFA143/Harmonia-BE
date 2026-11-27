namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/system-settings/{id}. Only the value can change; the key and its type are fixed.</summary>
public class UpdateSystemSettingRequest
{
    /// <summary>Text form of the value: "42" for Int, "true" / "false" for Bool. May be empty for a String setting.</summary>
    public string Value { get; set; } = string.Empty;
}
