using System.Text.Json;
using Harmonia.API.Middlewares;
using Harmonia.Application.DTOs;
using Harmonia.Application.Exceptions;
using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Harmonia.API.Tests;

public class GlobalExceptionHandlingMiddlewareTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static async Task<(int Status, ErrorResponse? Body, string Raw)> RunAsync(
        Exception exception, CancellationToken requestAborted = default)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
            RequestAborted = requestAborted,
        };
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw exception, NullLogger<GlobalExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var raw = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = raw.Length == 0 ? null : JsonSerializer.Deserialize<ErrorResponse>(raw, TestJson.Options);
        return (context.Response.StatusCode, body, raw);
    }

    [Fact]
    public async Task DomainException_UsesMappedStatusAndCode_Async()
    {
        var (status, body, _) = await RunAsync(new RefreshTokenRevokedException(), _ct);

        Assert.Equal(401, status);
        Assert.Equal(ErrorCodes.AuthRefreshTokenRevoked, body!.Code);
    }

    [Fact]
    public async Task DomainException_ConflictByDefault_Async()
    {
        var (status, body, _) = await RunAsync(new SongListAlreadyApprovedException(), _ct);

        Assert.Equal(409, status);
        Assert.Equal(ErrorCodes.SongListAlreadyApproved, body!.Code);
    }

    [Fact]
    public async Task ValidationException_Returns400WithFieldErrors_Async()
    {
        var errors = new Dictionary<string, string[]> { ["email"] = [ErrorCodes.AuthEmailRequired] };

        var (status, body, _) = await RunAsync(new ValidationException(errors), _ct);

        Assert.Equal(400, status);
        Assert.Equal(ErrorCodes.ValidationFailed, body!.Code);
        Assert.Equal([ErrorCodes.AuthEmailRequired], body.Errors!["email"]);
    }

    [Fact]
    public async Task NotFoundException_Returns404_Async()
    {
        var (status, body, _) = await RunAsync(new NotFoundException("Role seed missing"), _ct);

        Assert.Equal(404, status);
        Assert.Equal(ErrorCodes.NotFound, body!.Code);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithoutLeakingDetails_Async()
    {
        var (status, body, raw) = await RunAsync(
            new InvalidOperationException("Connection string Server=prod;Password=hunter2 failed"), _ct);

        Assert.Equal(500, status);
        Assert.Equal(ErrorCodes.InternalError, body!.Code);
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("at Harmonia", raw);
    }

    [Fact]
    public async Task ClientDisconnected_WritesNothing_Async()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var (_, body, raw) = await RunAsync(new OperationCanceledException(), cts.Token);

        Assert.Null(body);
        Assert.Empty(raw);
    }
}
