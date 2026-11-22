using Flagship.API.Contracts;
using Flagship.API.Helpers;
using Flagship.Core.Business.Models;
using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flagship.API.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class FlagsController(
    FlagRepository flags,
    ILogger<FlagsController> logger) : ControllerBase
{
    [HttpGet("projects/{projectId:guid}/flags")]
    public async Task<IActionResult> List(
        Guid projectId,
        [FromQuery] Guid environmentId,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken) =>
        Ok(await flags.ListAsync(projectId, environmentId, includeArchived, cancellationToken));

    [HttpPost("projects/{projectId:guid}/flags")]
    public async Task<IActionResult> Create(
        Guid projectId,
        CreateFlagRequest request,
        CancellationToken cancellationToken)
    {
        var flag = await flags.CreateAsync(
            projectId, request.Name, request.Key, request.Description, User.Actor(), cancellationToken);
        logger.LogInformation("Created flag {FlagId}", flag.Id);
        return Created($"/api/flags/{flag.Id}", flag);
    }

    [HttpGet("flags/{flagId:guid}")]
    public async Task<IActionResult> Get(
        Guid flagId,
        [FromQuery] Guid environmentId,
        CancellationToken cancellationToken) =>
        Ok(await flags.GetAsync(flagId, environmentId, cancellationToken));

    [HttpPut("flags/{flagId:guid}/configuration/{environmentId:guid}")]
    public async Task<IActionResult> UpdateConfiguration(
        Guid flagId,
        Guid environmentId,
        FlagConfigurationInput input,
        CancellationToken cancellationToken)
    {
        var flag = await flags.UpdateConfigurationAsync(
            flagId, environmentId, input, User.Actor(), cancellationToken);
        logger.LogInformation(
            "Updated flag {FlagId} in environment {EnvironmentId} to version {Version}",
            flagId, environmentId, flag.Version);
        return Ok(flag);
    }

    [HttpPost("flags/{flagId:guid}/archive")]
    public Task<IActionResult> Archive(Guid flagId, CancellationToken cancellationToken) =>
        SetArchived(flagId, true, cancellationToken);

    [HttpPost("flags/{flagId:guid}/restore")]
    public Task<IActionResult> Restore(Guid flagId, CancellationToken cancellationToken) =>
        SetArchived(flagId, false, cancellationToken);

    private async Task<IActionResult> SetArchived(
        Guid flagId,
        bool archived,
        CancellationToken cancellationToken)
    {
        await flags.SetArchivedAsync(flagId, archived, User.Actor(), cancellationToken);
        logger.LogInformation("{FlagAction} flag {FlagId}", archived ? "Archived" : "Restored", flagId);
        return NoContent();
    }
}
