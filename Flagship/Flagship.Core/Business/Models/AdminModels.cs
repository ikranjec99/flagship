namespace Flagship.Core.Business.Models;

public sealed record AdminUserModel(Guid Id, string Email, string PasswordHash);
