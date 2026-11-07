using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/liturgical-days")]
[Authorize]
public class LiturgicalDaysController(ILiturgicalDayService liturgicalDayService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<LiturgicalDayDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRangeAsync(
        [FromQuery] SearchLiturgicalDaysRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalDayService.GetRangeAsync(request, cancellationToken));

    [HttpGet("{date}")]
    [ProducesResponseType<LiturgicalDayDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalDayService.GetByDateAsync(date, cancellationToken));

    /// <summary>Fetches a civil year from the Liturgical Calendar API and overwrites the cached days of that year.</summary>
    [Authorize(Roles = RoleNames.ParishPriest)]
    [HttpPost("sync")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncAsync(
        [FromBody] SyncLiturgicalDaysRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalDayService.SyncAsync(request, cancellationToken));
}