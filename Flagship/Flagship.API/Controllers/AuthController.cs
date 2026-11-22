using System.Security.Claims;
using Flagship.API.Contracts;
using Flagship.Core.Business.Models;
using Flagship.Core.DataAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Flagship.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AdminRepository admins,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."]
            }));
        }

        var admin = await admins.FindByEmailAsync(request.Email, cancellationToken);
        if (admin is null || new PasswordHasher<AdminUserModel>().VerifyHashedPassword(
                admin, admin.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Failed administrator login");
            return Unauthorized();
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        logger.LogInformation("Administrator signed in");
        return Ok(new { admin.Email });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new { email = User.FindFirstValue(ClaimTypes.Email) });
}
