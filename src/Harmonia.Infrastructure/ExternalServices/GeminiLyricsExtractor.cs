using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Downloads the sheet from its signed URL, hands it to the Gemini File API (inline data caps the whole request
/// near 20 MB, below the 20 MB upload limit once base64 grows it), then asks generateContent for the lyrics.
/// Base address and timeout are set at registration.
/// </summary>
public class GeminiLyricsExtractor(
    HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiLyricsExtractor> logger) : ILyricsExtractor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Instructions =
        """
        This is Catholic hymn sheet music, usually in Vietnamese.
        Transcribe the sung lyrics exactly as printed, with every Vietnamese diacritic.
        - Keep the order of the song: verses in number order, refrain where printed, each labelled the way the sheet labels it (e.g. "ĐK:", "1.").
        - One sung phrase per line, a blank line between verses and refrain.
        - Join syllables split across notes or hyphens back into whole words.
        - Leave out the title, composer, chords, tempo, page numbers and any other text that is not sung.
        - If no lyrics are printed, return an empty lyricsText.
        """;

    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new { lyricsText = new { type = "STRING" } },
        required = new[] { "lyricsText" },
    };

    public async Task<Result<string>> ExtractAsync(string fileUrl, string fileName, CancellationToken cancellationToken)
    {
        var mimeType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            _ => null,
        };
        if (mimeType is null) return Result<string>.Failure(ErrorCodes.MaterialNotSheetMusic);

        string? geminiFileName = null;
        try
        {
            var content = await httpClient.GetByteArrayAsync(fileUrl, cancellationToken);
            var (fileUri, name) = await UploadAsync(content, mimeType, cancellationToken);
            geminiFileName = name;

            // shortcut: PDFs and images are ACTIVE once uploaded, so no polling for PROCESSING; add it if Gemini starts returning FAILED_PRECONDITION.
            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { fileData = new { mimeType, fileUri } }, new { text = Instructions } },
                    },
                },
                generationConfig = new { responseMimeType = "application/json", responseSchema = ResponseSchema, temperature = 0 },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{options.Value.Model}:generateContent")
            {
                Content = JsonContent.Create(body, options: Json),
            };
            request.Headers.Add("x-goog-api-key", options.Value.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Gemini's error body names the cause (retired model, quota); it never echoes the API key.
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Gemini lyrics extraction failed with status {StatusCode}: {Error}",
                    (int)response.StatusCode, error.Length > 500 ? error[..500] : error);
                return Result<string>.Failure(ErrorCodes.ExternalAiFailed);
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var text = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            using var answer = JsonDocument.Parse(text ?? "null");
            var lyrics = answer.RootElement.ValueKind == JsonValueKind.Object ? answer.RootElement.GetProperty("lyricsText").GetString() : null;

            return lyrics is null
                ? Result<string>.Failure(ErrorCodes.ExternalAiFailed)
                : Result<string>.Success(lyrics.Trim());
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Timeout, a failed download or upload, or an unexpected response shape. The signed URL is not logged.
            logger.LogWarning(ex, "Gemini lyrics extraction failed");
            return Result<string>.Failure(ErrorCodes.ExternalAiFailed);
        }
        finally
        {
            // Gemini drops files after 48 hours anyway; deleting now keeps the sheet there no longer than needed.
            if (geminiFileName is not null) await DeleteAsync(geminiFileName);
        }
    }

    /// <summary>Resumable upload in one chunk: start returns an upload URL, then one "upload, finalize" sends the bytes.</summary>
    private async Task<(string Uri, string Name)> UploadAsync(byte[] content, string mimeType, CancellationToken cancellationToken)
    {
        using var start = new HttpRequestMessage(HttpMethod.Post, "upload/v1beta/files")
        {
            Content = JsonContent.Create(new { file = new { displayName = "sheet-music" } }, options: Json),
        };
        start.Headers.Add("x-goog-api-key", options.Value.ApiKey);
        start.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        start.Headers.Add("X-Goog-Upload-Command", "start");
        start.Headers.Add("X-Goog-Upload-Header-Content-Length", content.Length.ToString());
        start.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);

        using var startResponse = await httpClient.SendAsync(start, cancellationToken);
        startResponse.EnsureSuccessStatusCode();
        var uploadUrl = startResponse.Headers.GetValues("X-Goog-Upload-URL").First();

        using var upload = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = new ByteArrayContent(content) };
        upload.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        upload.Headers.Add("X-Goog-Upload-Offset", "0");
        upload.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "upload, finalize");

        using var uploadResponse = await httpClient.SendAsync(upload, cancellationToken);
        uploadResponse.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await uploadResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var file = document.RootElement.GetProperty("file");
        return (file.GetProperty("uri").GetString()!, file.GetProperty("name").GetString()!);
    }

    // Best effort: a leftover file expires on its own.
    private async Task DeleteAsync(string geminiFileName)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"v1beta/{geminiFileName}");
            request.Headers.Add("x-goog-api-key", options.Value.ApiKey);
            using var response = await httpClient.SendAsync(request, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Gemini file delete failed with status {StatusCode}", (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Gemini file delete failed");
        }
    }
}
