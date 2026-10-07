using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Harmonia.API.Filters;

/// <summary>
/// Blocks a user who still has the password the Admin emailed from every action except those marked
/// <see cref="AllowWhenPasswordChangeRequiredAttribute"/>. The flag travels as a token claim, so no database
/// read is needed; changing the password revokes every session and the next sign-in issues a token without it.
/// </summary>
public class PasswordChangeRequiredFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.User.HasClaim(JwtTokenService.PasswordChangeRequiredClaim, "true")
            || context.ActionDescriptor.EndpointMetadata.OfType<AllowWhenPasswordChangeRequiredAttribute>().Any())
        {
            return;
        }

        context.Result = new ObjectResult(new ErrorResponse(
            ErrorCodes.AuthPasswordChangeRequired, "The password must be changed before continuing."))
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
