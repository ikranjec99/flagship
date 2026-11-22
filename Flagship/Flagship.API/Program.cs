using System.Text.Json.Serialization;
using Flagship.API.Configuration;
using Flagship.API.Filters;
using Flagship.Core.Business.Evaluation;
using Flagship.Core.DataAccess;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Npgsql;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

DefaultTypeMap.MatchNamesWithUnderscores = true;

var connectionString = builder.Configuration.GetConnectionString("Flagship")
    ?? throw new InvalidOperationException("ConnectionStrings:Flagship is required.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.AddSingleton<AdminBootstrapper>();
builder.Services.AddSingleton<FlagEvaluator>();
builder.Services.AddSingleton<AdminRepository>();
builder.Services.AddSingleton<ProjectRepository>();
builder.Services.AddSingleton<FlagRepository>();
builder.Services.AddSingleton<ApiKeyRepository>();
builder.Services.AddSingleton<AuditRepository>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "flagship.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Flagship API",
        Version = "v1",
        Description = "Management and server-side evaluation API for Flagship."
    }));

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Flagship API v1");
    options.RoutePrefix = "swagger";
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

await app.Services.GetRequiredService<DatabaseMigrator>()
    .MigrateAsync(CancellationToken.None);
await app.Services.GetRequiredService<AdminBootstrapper>()
    .BootstrapAsync(CancellationToken.None);
app.Logger.LogInformation("Flagship startup completed");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

app.MapFallbackToFile("index.html");
app.Run();

public partial class Program
{
}
