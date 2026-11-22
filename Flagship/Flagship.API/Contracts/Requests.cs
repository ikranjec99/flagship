using System.Text.Json;

namespace Flagship.API.Contracts;

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateProjectRequest(string Name, string? Key);
public sealed record CreateEnvironmentRequest(string Name, string? Key);
public sealed record CreateFlagRequest(string Name, string? Key, string? Description);
public sealed record CreateApiKeyRequest(string Name);
public sealed record EvaluationRequest(
    string? SubjectKey,
    IReadOnlyDictionary<string, JsonElement>? Attributes);
