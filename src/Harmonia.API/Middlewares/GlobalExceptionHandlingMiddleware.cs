using Harmonia.Application.DTOs;
using Harmonia.Application.Exceptions;
using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

            // Two requests passed the same "does it exist yet?" check (e.g. a double click); the database kept one.
            // 2601 / 2627 = SQL Server unique index / constraint violation.
            case DbUpdateConcurrencyException:
            case DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }:
                logger.LogWarning(ex, "A concurrent write lost the race");
                return (
                    StatusCodes.Status409Conflict,
                    new ErrorResponse(ErrorCodes.Conflict, "The data was changed by another request. Reload and try again."));

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
