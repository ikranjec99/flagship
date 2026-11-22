namespace Flagship.Core.Business.Models;

public sealed record EnvironmentModel(Guid Id, string Name, string Key);

public sealed record ProjectModel(
    Guid Id,
    string Name,
    string Key,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EnvironmentModel> Environments);
