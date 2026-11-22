using System.Text.Json;
using Flagship.API.Contracts;
using Flagship.API.Helpers;
using Flagship.Core.Business.Evaluation;
using Flagship.Core.Business.Models;
using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flagship.API.Controllers;

[ApiController]
[Route("api/evaluate/v1")]
public sealed class EvaluationController(
    ApiKeyRepository apiKeys,
    FlagRepository flags,
    FlagEvaluator evaluator,
    ILogger<EvaluationController> logger) : ControllerBase
{
    [HttpPost("flags/{flagKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> Evaluate(
        string flagKey,
        EvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var identity = await AuthenticateAsync(cancellationToken);
        if (identity is null)
        {
            logger.LogWarning("Rejected flag evaluation with an invalid API key");
            return Unauthorized();
        }

        var definition = await flags.GetDefinitionAsync(
            identity.ProjectId, identity.EnvironmentId, flagKey, cancellationToken);
        if (definition is null)
        {
            return NotFound(new { message = $"Flag '{flagKey}' was not found." });
        }

        var result = evaluator.Evaluate(definition, Subject(request));
        logger.LogDebug(
            "Evaluated flag in environment {EnvironmentId}: {EvaluationReason}",
            identity.EnvironmentId, result.Reason);
        return Ok(result);
    }

    [HttpPost("flags")]
    [AllowAnonymous]
    public async Task<IActionResult> EvaluateAll(
        EvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var identity = await AuthenticateAsync(cancellationToken);
        if (identity is null)
        {
            logger.LogWarning("Rejected all-flags evaluation with an invalid API key");
            return Unauthorized();
        }

        var definitions = await flags.GetDefinitionsAsync(
            identity.ProjectId, identity.EnvironmentId, cancellationToken);
        var subject = Subject(request);
        return Ok(definitions.Select(definition => evaluator.Evaluate(definition, subject)).ToList());
    }

    [HttpPost("/api/internal/environments/{environmentId:guid}/evaluate/{flagKey}")]
    [Authorize]
    public async Task<IActionResult> EvaluateInternal(
        Guid environmentId,
        string flagKey,
        EvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var projectId = await flags.GetProjectIdForEnvironmentAsync(environmentId, cancellationToken);
        if (projectId is null)
        {
            return NotFound();
        }

        var definition = await flags.GetDefinitionAsync(
            projectId.Value, environmentId, flagKey, cancellationToken);
        return definition is null
            ? NotFound()
            : Ok(evaluator.Evaluate(definition, Subject(request)));
    }

    private async Task<ApiKeyIdentity?> AuthenticateAsync(
        CancellationToken cancellationToken)
    {
        var secret = ApiKeyCredentialReader.ReadBearerToken(Request);
        return secret is null ? null : await apiKeys.AuthenticateAsync(secret, cancellationToken);
    }

    private static EvaluationSubject Subject(EvaluationRequest request) =>
        new(request.SubjectKey,
            request.Attributes ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal));
}
