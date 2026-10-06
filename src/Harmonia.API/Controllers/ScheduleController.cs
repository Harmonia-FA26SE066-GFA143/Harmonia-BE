using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/schedule")]
[Authorize]
public class ScheduleController(IUpcomingScheduleService upcomingScheduleService) : ApiControllerBase
{
    [HttpGet("events")]
    public async Task<IActionResult> GetUpcomingEventsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetUpcomingEventsAsync(cancellationToken));

    [HttpGet("rehearsals")]
    public async Task<IActionResult> GetUpcomingRehearsalsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetUpcomingRehearsalsAsync(cancellationToken));
}