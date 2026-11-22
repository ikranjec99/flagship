using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flagship.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/audit")]
public sealed class AuditController(AuditRepository audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid projectId,
        [FromQuery] Guid? environmentId,
        CancellationToken cancellationToken) =>
        Ok(await audit.ListAsync(projectId, environmentId, cancellationToken));
}
