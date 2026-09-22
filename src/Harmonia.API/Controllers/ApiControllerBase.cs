using Harmonia.API.Middlewares;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Turns a service <see cref="Result"/> into an HTTP response, so every controller reports
/// expected failures with the same status and the same body shape as the exception middleware.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Ok(result.Value)
            : ToErrorResult(result);

    protected IActionResult ToActionResult(Result result) =>
        result.IsSuccess
            ? NoContent()
            : ToErrorResult(result);

    private IActionResult ToErrorResult(Result result) =>
        StatusCode(
            ErrorStatusMap.StatusFor(result.Code!, StatusCodes.Status400BadRequest),
            new ErrorResponse(result.Code!, result.Message ?? result.Code!));
}
