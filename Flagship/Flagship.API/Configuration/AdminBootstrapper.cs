using Flagship.Core.Business.Models;
using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Identity;

namespace Flagship.API.Configuration;

public sealed class AdminBootstrapper(
    AdminRepository admins,
    IConfiguration configuration,
    ILogger<AdminBootstrapper> logger)
{
    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        var existingAdmins = await admins.CountAsync(cancellationToken);
        if (existingAdmins > 0)
        {
            return;
        }

        var email = configuration["Flagship:Admin:Email"];
        var password = configuration["Flagship:Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "An empty database requires Flagship:Admin:Email and Flagship:Admin:Password. " +
                "Set them through environment variables before first startup.");
        }

        var admin = new AdminUserModel(Guid.NewGuid(), email.Trim().ToLowerInvariant(), string.Empty);
        var hasher = new PasswordHasher<AdminUserModel>();
        var hash = hasher.HashPassword(admin, password);
        await admins.InsertAsync(admin.Id, admin.Email, hash, cancellationToken);
        logger.LogInformation("Created initial Flagship administrator");
    }
}
