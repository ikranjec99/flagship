using Flagship.API.Contracts;
using Flagship.API.Helpers;
using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flagship.API.Controllers;

[ApiController]
[Authorize]
[Route("api/environments/{environmentId:guid}/api-keys")]
public sealed class ApiKeysController(
    ApiKeyRepository apiKeys,
    ILogger<ApiKeysController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid environmentId,
        CancellationToken cancellationToken) =>
        Ok(await apiKeys.ListAsync(environmentId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid environmentId,
        CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var key = await apiKeys.CreateAsync(
            environmentId, request.Name, User.Actor(), cancellationToken);
        logger.LogInformation(
            "Created server API key {ApiKeyId} for environment {EnvironmentId}", key.Id, environmentId);
        return Created($"/api/environments/{environmentId}/api-keys/{key.Id}", key);
    }

    [HttpDelete("{keyId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid environmentId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        await apiKeys.RevokeAsync(environmentId, keyId, User.Actor(), cancellationToken);
        logger.LogInformation(
            "Revoked server API key {ApiKeyId} for environment {EnvironmentId}", keyId, environmentId);
        return NoContent();
    }
}
