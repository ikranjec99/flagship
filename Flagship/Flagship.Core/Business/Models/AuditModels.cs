namespace Flagship.Core.Business.Models;

public sealed record AuditEventModel(
    long Id,
    Guid? ProjectId,
    Guid? EnvironmentId,
    string Actor,
    string Action,
    string EntityType,
    Guid? EntityId,
    string Summary,
    string? Note,
    DateTimeOffset OccurredAt);
