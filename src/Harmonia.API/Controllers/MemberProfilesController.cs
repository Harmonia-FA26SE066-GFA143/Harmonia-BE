using Harmonia.API.Extensions;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

[Route("api/member-profiles")]
[Authorize(Roles = RoleNames.ChoirMember)]
public class MemberProfilesController(IMemberProfileService memberProfileService) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMineAsync(CancellationToken cancellationToken) =>
        ToActionResult(await memberProfileService.GetMineAsync(User.GetUserId(), cancellationToken));
}
