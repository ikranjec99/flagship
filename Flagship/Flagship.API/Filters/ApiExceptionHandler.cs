using Flagship.Core.Business.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Flagship.API.Filters;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, extensions) = exception switch
        {
            RequestValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                new Dictionary<string, object?> { ["errors"] = validation.Errors }),
            EntityNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                null),
            VersionConflictException versionConflict => (
                StatusCodes.Status409Conflict,
                "Configuration conflict",
                new Dictionary<string, object?> { ["currentVersion"] = versionConflict.CurrentVersion }),
            ConflictException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                null),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", null)
        };

        if (status >= 500)
        {
            logger.LogError(
                "Unhandled API exception {ExceptionType}; trace identifier {TraceIdentifier}",
                exception.GetType().Name,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "API request failed with {StatusCode}; trace identifier {TraceIdentifier}",
                status,
                httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "An unexpected error occurred." : exception.Message,
            Instance = httpContext.Request.Path
        };
        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                problem.Extensions[extension.Key] = extension.Value;
            }
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });
    }
}
