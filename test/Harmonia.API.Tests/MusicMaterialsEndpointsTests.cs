using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.API.Tests;

/// <summary>
/// First multipart endpoint of the API: checks that form fields and the file both reach the
/// service, and the metadata update. Every case stops before storage, so Cloudinary is never called.
/// </summary>
public class MusicMaterialsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private Task<Guid> SeedSongAsync(CancellationToken cancellationToken = default) =>
        factory.WithDbAsync(
            async (db, ct) =>
            {
                var song = new Song { Id = Guid.NewGuid(), Title = $"Song {Guid.NewGuid():N}" };
                db.Songs.Add(song);
                await db.SaveChangesAsync(ct);
                return song.Id;
            },
            cancellationToken);

    private static MultipartFormDataContent NewForm(Guid songId, string title, string fileName)
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new("application/pdf"); // Deliberately lies; the server must ignore it.
        return new MultipartFormDataContent
        {
            { new StringContent(songId.ToString()), "songId" },
            { new StringContent(title), "title" },
            { new StringContent("SheetMusic"), "materialType" },
            { file, "file", fileName },
        };
    }

    [Fact]
    public async Task Upload_AsChoirMember_Returns403_Async()
    {
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var response = await client.PostAsync("api/music-materials", NewForm(Guid.NewGuid(), "t", "a.pdf"), _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_DisallowedExtension_BindsFormAndReturnsFileTypeNotAllowed_Async()
    {
        var songId = await SeedSongAsync(_ct);
        var client = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await client.PostAsync("api/music-materials", NewForm(songId, "Ban nhac", "virus.exe"), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct);
        Assert.Equal(ErrorCodes.MaterialFileTypeNotAllowed, error!.Code);
    }

    [Fact]
    public async Task Upload_BlankTitle_RunsValidatorOnFormFields_Async()
    {
        var client = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await client.PostAsync("api/music-materials", NewForm(Guid.NewGuid(), " ", "a.pdf"), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct);
        Assert.Contains("title", error!.Errors!.Keys);
    }

    [Fact]
    public async Task GetBySong_WithoutSongId_Returns400_Async()
    {
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var response = await client.GetAsync("api/music-materials", _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBySong_AsChoirMember_ReturnsEmptyPage_Async()
    {
        var songId = await SeedSongAsync(_ct);
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var response = await client.GetAsync($"api/music-materials?songId={songId}", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal(0, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Update_AsChoirMember_Returns403_Async()
    {
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var response = await client.PutAsJsonAsync($"api/music-materials/{Guid.NewGuid()}", new { title = "t" }, _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_BlankTitle_Returns400_Async()
    {
        var client = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await client.PutAsJsonAsync($"api/music-materials/{Guid.NewGuid()}", new { title = " " }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownMaterial_Returns404MaterialNotFound_Async()
    {
        var client = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await client.PutAsJsonAsync($"api/music-materials/{Guid.NewGuid()}", new { title = "New" }, _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct);
        Assert.Equal(ErrorCodes.MaterialNotFound, error!.Code);
    }

    [Fact]
    public async Task Swagger_DescribesUploadAsMultipartWithFile_Async()
    {
        var json = await factory.CreateClient().GetStringAsync("swagger/v1/swagger.json", _ct);

        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement
            .GetProperty("paths").GetProperty("/api/music-materials").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data")
            .GetProperty("schema").GetProperty("properties");
        Assert.Equal("binary", properties.GetProperty("file").GetProperty("format").GetString());
        Assert.True(properties.TryGetProperty("SongId", out _) || properties.TryGetProperty("songId", out _));
    }
}
