using Harmonia.Application.DTOs;
using Harmonia.Application.Exceptions;
using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;

namespace Harmonia.API.Middlewares;

public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nobody is left to read a response.
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, body) = Map(ex);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(body, context.RequestAborted);
        }
    }

    private (int Status, ErrorResponse Body) Map(Exception ex)
    {
        switch (ex)
        {
            case DomainException domain:
                return (
                    ErrorStatusMap.StatusFor(domain.Code, StatusCodes.Status409Conflict),
                    new ErrorResponse(domain.Code, domain.Message));

            case ValidationException validation:
                return (
                    StatusCodes.Status400BadRequest,
                    new ErrorResponse(ErrorCodes.ValidationFailed, validation.Message, validation.Errors));

            case NotFoundException notFound:
                logger.LogWarning(notFound, "Expected data was not found");
                return (
                    StatusCodes.Status404NotFound,
                    new ErrorResponse(ErrorCodes.NotFound, notFound.Message));

            default:
                // Never echo the original message or stack trace to the client.
                logger.LogError(ex, "Unhandled exception");
                return (
                    StatusCodes.Status500InternalServerError,
                    new ErrorResponse(ErrorCodes.InternalError, "An unexpected error occurred"));
        }
    }
}
