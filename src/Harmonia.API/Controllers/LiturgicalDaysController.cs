using Harmonia.Application.Common.Models;
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

    [Authorize(Roles = RoleNames.ParishPriest)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return ToActionResult(Result<int>.Failure(ErrorCodes.CalendarFileRequired));
        }

        await using var stream = file.OpenReadStream();

        return ToActionResult(
            await liturgicalDayService.ImportAsync(stream, file.FileName, file.Length, cancellationToken));
    }
}