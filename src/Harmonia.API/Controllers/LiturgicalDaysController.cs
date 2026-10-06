using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/liturgical-days")]
[Authorize(Roles = RoleNames.ParishPriest)]
public class LiturgicalDaysController(ILiturgicalDayService liturgicalDayService) : ApiControllerBase
{
    [HttpGet("{date}")]
    public async Task<IActionResult> GetByDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalDayService.GetByDateAsync(date, cancellationToken));

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
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