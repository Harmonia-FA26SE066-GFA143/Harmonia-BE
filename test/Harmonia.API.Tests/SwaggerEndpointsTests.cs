using System.Net;
using System.Text.Json;

namespace Harmonia.API.Tests;

public class SwaggerEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SwaggerJson_EveryOperationDeclaresItsSuccessResponse_Async()
    {
        var response = await factory.CreateClient().GetAsync("swagger/v1/swagger.json", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct)).RootElement.GetProperty("paths");
        var undeclared = new List<string>();
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var responses = operation.Value.GetProperty("responses");
                // 200 must carry a body schema so clients can generate models; 204 has none by design.
                var declared = responses.TryGetProperty("204", out _)
                    || (responses.TryGetProperty("200", out var ok) && ok.TryGetProperty("content", out _));
                if (!declared) undeclared.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
            }
        }

        Assert.Empty(undeclared);
    }
}
