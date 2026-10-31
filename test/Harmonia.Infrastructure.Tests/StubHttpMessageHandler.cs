using System.Net;

namespace Harmonia.Infrastructure.Tests;

/// <summary>Answers every request with a fixed status and body, and keeps the last request for assertions.</summary>
public sealed class StubHttpMessageHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
