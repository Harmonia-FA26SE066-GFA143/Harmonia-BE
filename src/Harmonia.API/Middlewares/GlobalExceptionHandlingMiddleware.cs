using System.Text.Json.Serialization;
using Harmonia.Application.Exceptions;
using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;

namespace Harmonia.API.Middlewares;

public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    // Domain error code -> HTTP status, mirroring doc/error-codes.md. Codes not listed are 409.
    private static readonly Dictionary<string, int> DomainStatusByCode = new()
    {
        [ErrorCodes.AuthRefreshTokenRevoked] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthRefreshTokenExpired] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.MemberJoinedDateInFuture] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MemberSkillRejectReasonRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.WeekStartNotMonday] = StatusCodes.Status400BadRequest,
        [ErrorCodes.EventDateOutsideWeek] = StatusCodes.Status400BadRequest,
        [ErrorCodes.EventTypeRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SeasonDateInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SongListEmpty] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReviewNotesRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PersonnelRequiredCountInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.RehearsalTimeInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeDueDateInPast] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeTargetRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.DirectorNoteTargetRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SettingValueTypeMismatch] = StatusCodes.Status400BadRequest,
    };

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
                    DomainStatusByCode.GetValueOrDefault(domain.Code, StatusCodes.Status409Conflict),
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

    private sealed record ErrorResponse(
        string Code,
        string Message,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyDictionary<string, string[]>? Errors = null);
}
