using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Trippify.Infrastructure;
using Trippify.Application;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Trippify.Api;
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Postgres"))) throw new InvalidOperationException("Required production database configuration is missing.");
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("api", x => { x.PermitLimit = 100; x.Window = TimeSpan.FromMinutes(1); }));
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Database=trippify;Username=trippify;Password=trippify_dev"));
builder.Services.AddIdentityApiEndpoints<AppUser>(options => { options.SignIn.RequireConfirmedEmail = true; options.User.RequireUniqueEmail = true; options.Password.RequiredLength = 10; options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5; }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddSingleton(new ActivitySource("Trippify.Api")); builder.Services.AddSingleton(new Meter("Trippify.Api"));
builder.Services.AddSingleton<LocalProviders>();
builder.Services.AddSingleton<IObjectStorage>(x => x.GetRequiredService<LocalProviders>()); builder.Services.AddSingleton<Trippify.Application.IEmailSender>(x => x.GetRequiredService<LocalProviders>()); builder.Services.AddSingleton<IMapProvider>(x => x.GetRequiredService<LocalProviders>()); builder.Services.AddSingleton<IPaymentGateway>(x => x.GetRequiredService<LocalProviders>()); builder.Services.AddSingleton<IAiAssistant>(x => x.GetRequiredService<LocalProviders>()); builder.Services.AddSingleton<IBackgroundJobQueue>(x => x.GetRequiredService<LocalProviders>()); builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.IEmailSender<AppUser>, IdentityEmailSender>();
var app = builder.Build(); app.UseExceptionHandler(); app.Use(async (context, next) => { context.Response.Headers.Append("X-Correlation-Id", Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier); await next(); }); app.UseRateLimiter(); app.UseAuthentication(); app.UseMiddleware<ActiveSessionMiddleware>(); app.UseAuthorization(); app.UseSwagger(); app.UseSwaggerUI();
app.MapGet("/api/v1/system", () => Results.Ok(new { name = "Trippify", apiVersion = "v1" })).RequireRateLimiting("api");
app.MapGet("/api/v1/system/protected", () => Results.NoContent()).RequireAuthorization();
app.MapIdentity();
app.MapGuides();
app.MapPlanning();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });
app.Run();
public partial class Program;
public sealed class PostgresHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
}
