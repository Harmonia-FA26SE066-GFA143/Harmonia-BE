using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harmonia.API.Tests;

/// <summary>Same wire format as the API: camelCase, enums as names.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
