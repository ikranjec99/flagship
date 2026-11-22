using Flagship.API.Contracts;
using Flagship.API.Helpers;
using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flagship.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController(
    ProjectRepository projects,
    ILogger<ProjectsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await projects.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await projects.CreateAsync(
            request.Name, request.Key, User.Actor(), cancellationToken);
        logger.LogInformation("Created project {ProjectId}", project.Id);
        return Created($"/api/projects/{project.Id}", project);
    }

    [HttpPost("{projectId:guid}/environments")]
    public async Task<IActionResult> CreateEnvironment(
        Guid projectId,
        CreateEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        var environment = await projects.CreateEnvironmentAsync(
            projectId, request.Name, request.Key, User.Actor(), cancellationToken);
        logger.LogInformation(
            "Created environment {EnvironmentId} in project {ProjectId}", environment.Id, projectId);
        return Created($"/api/projects/{projectId}/environments/{environment.Id}", environment);
    }
}
