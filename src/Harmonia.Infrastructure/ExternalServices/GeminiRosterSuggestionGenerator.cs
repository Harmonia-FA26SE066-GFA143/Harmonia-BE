using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Calls Gemini's generateContent REST endpoint with a JSON response schema. Base address and timeout are set at registration.</summary>
public class GeminiRosterSuggestionGenerator(
    HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiRosterSuggestionGenerator> logger) : IRosterSuggestionGenerator
{
    // String enums so the model reads "Advanced" rather than 2.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private const string Instructions =
        """
        You assign choir members to service slots for one liturgical event.
        Each slot is one song and one skill, with neededCount and its eligible candidates.
        Rules:
        - Pick at most neededCount members per slot, only from that slot's candidates, no repeats in a slot.
        - Prefer a higher level (Advanced > Intermediate > Beginner > null).
        - Among similar levels prefer a lower recentServiceCount, to spread the load fairly.
        - The same member may fill slots of several songs, and several skills of one song.
        Return one entry per slot with the chosen memberCodes.
        Slots:
        """;

    // Gemini's OpenAPI-subset schema for List<RosterSlotPick>.
    private static readonly object ResponseSchema = new
    {
        type = "ARRAY",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                slotCode = new { type = "STRING" },
                memberCodes = new { type = "ARRAY", items = new { type = "STRING" } },
            },
            required = new[] { "slotCode", "memberCodes" },
        },
    };

    public async Task<Result<List<RosterSlotPick>>> GenerateAsync(List<RosterSlotInput> slots, CancellationToken cancellationToken)
    {
        var body = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = Instructions + JsonSerializer.Serialize(slots, Json) } } } },
            generationConfig = new { responseMimeType = "application/json", responseSchema = ResponseSchema, temperature = 0.2 },
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{options.Value.Model}:generateContent")
            {
                Content = JsonContent.Create(body, options: Json),
            };
            request.Headers.Add("x-goog-api-key", options.Value.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini roster suggestion failed with status {StatusCode}", (int)response.StatusCode);
                return Result<List<RosterSlotPick>>.Failure(ErrorCodes.ExternalAiFailed);
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var text = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            var picks = text is null ? null : JsonSerializer.Deserialize<List<RosterSlotPick>>(text, Json);

            return picks is null
                ? Result<List<RosterSlotPick>>.Failure(ErrorCodes.ExternalAiFailed)
                : Result<List<RosterSlotPick>>.Success(picks);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Timeout or an unexpected response shape. The prompt is not logged.
            logger.LogWarning(ex, "Gemini roster suggestion failed");
            return Result<List<RosterSlotPick>>.Failure(ErrorCodes.ExternalAiFailed);
        }
    }
}
