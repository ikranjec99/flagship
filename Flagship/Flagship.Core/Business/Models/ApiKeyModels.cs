namespace Flagship.Core.Business.Models;

public sealed record ApiKeyModel(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record CreatedApiKeyModel(
    Guid Id,
    string Name,
    string Prefix,
    string Secret,
    DateTimeOffset CreatedAt);

public sealed record ApiKeyIdentity(
    Guid EnvironmentId,
    Guid ProjectId,
    string EnvironmentKey);
