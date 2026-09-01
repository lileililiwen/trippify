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
var normalizedOrigins = RuntimeSecurityValidator.Validate(builder.Configuration, builder.Environment);
if (builder.Environment.IsDevelopment() && normalizedOrigins.Count == 0)
{
    normalizedOrigins = new[]
    {
        "http://localhost:3000",
        "http://localhost:5000",
        "http://localhost:8080",
        "http://127.0.0.1:3000",
        "http://127.0.0.1:5000",
        "http://127.0.0.1:8080",
    };
}
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        if (normalizedOrigins.Count > 0)
        {
            p.WithOrigins(normalizedOrigins.ToArray());
            p.AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            p.SetIsOriginAllowed(_ => true);
        }
        else
        {
            p.SetIsOriginAllowed(_ => false);
        }
        p.AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("api", x => { x.PermitLimit = 100; x.Window = TimeSpan.FromMinutes(1); }));
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull; o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()); });
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Database=trippify;Username=trippify;Password=trippify_dev"));
builder.Services.AddIdentityApiEndpoints<AppUser>(options => { options.SignIn.RequireConfirmedEmail = true; options.User.RequireUniqueEmail = true; options.Password.RequiredLength = 10; options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5; }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]).AddCheck<BackgroundJobsHealthCheck>("background-jobs", tags: ["ready"]);
builder.Services.AddSingleton(new ActivitySource("Trippify.Api")); builder.Services.AddSingleton(new Meter("Trippify.Api"));
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var options = ObjectStorageOptions.Bind(configuration);
    if (string.IsNullOrWhiteSpace(options.SignedUrlSecret))
        options.SignedUrlSecret = configuration["ObjectStorage:SignedUrlSecret"] ?? Environment.GetEnvironmentVariable("TRIPPIFY_OBJECT_STORAGE_SECRET") ?? Guid.NewGuid().ToString("N");
    if (string.IsNullOrWhiteSpace(options.LocalRoot))
        options.LocalRoot = configuration["ObjectStorage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "trippify-objects");
    if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        options.PublicBaseUrl = configuration["ObjectStorage:PublicBaseUrl"] ?? $"local://trippify/{options.Provider}";
    if (string.IsNullOrWhiteSpace(options.SignedUrlHost))
        options.SignedUrlHost = configuration["ObjectStorage:SignedUrlHost"] ?? string.Empty;
    options.Validate();
    return options;
});
builder.Services.AddSingleton(sp => MapProviderOptions.Bind(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<LocalMapProvider>(sp => new LocalMapProvider(sp.GetRequiredService<MapProviderOptions>()));
builder.Services.AddHttpClient<HttpMapProvider>((sp, client) =>
{
    var options = sp.GetRequiredService<MapProviderOptions>();
    client.BaseAddress = new Uri(options.Endpoint!);
    client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
});
builder.Services.AddScoped<IMapProvider>(sp =>
{
    var options = sp.GetRequiredService<MapProviderOptions>();
    IMapProvider inner = options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<LocalMapProvider>()
        : sp.GetRequiredService<HttpMapProvider>();
    return new CachedMapProvider(inner, sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IClock>(), options);
});
builder.Services.AddSingleton<IObjectStorage>(sp =>
{
    var options = sp.GetRequiredService<ObjectStorageOptions>();
    return options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase)
        ? new LocalFileObjectStorage(options)
        : ActivatorUtilities.CreateInstance<RemoteHttpObjectStorage>(sp, options);
});
builder.Services.AddHealthChecks().AddCheck<MapProviderHealthCheck>("map-provider", tags: ["ready"]).AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);
builder.Services.AddSingleton<IEmailSender>(sp =>
{
    var resendKey = sp.GetRequiredService<IConfiguration>()["RESEND_API_KEY"];
    if (string.IsNullOrWhiteSpace(resendKey)) return new NullEmailSender();
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ResendEmailSender));
    client.BaseAddress ??= new Uri("https://api.resend.com");
    return new ResendEmailSender(resendKey, client);
});
builder.Services.AddHttpClient(nameof(ResendEmailSender));
builder.Services.AddSingleton<RestorableBackupService>();
builder.Services.AddSingleton<IPaymentGateway, LocalPaymentGateway>();
var paymentOptions = PaymentProviderOptions.Bind(builder.Configuration);
paymentOptions.Validate();
builder.Services.AddSingleton(paymentOptions);
builder.Services.AddSingleton<IPaymentWebhookVerifier>(sp =>
{
    var options = sp.GetRequiredService<PaymentProviderOptions>();
    if (options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase) || !options.Enabled)
        return new NoopPaymentWebhookVerifier();
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpPaymentGateway));
    client.BaseAddress = new Uri(options.Endpoint!);
    client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    var logger = sp.GetRequiredService<ILogger<HttpPaymentGateway>>();
    return new HttpPaymentGateway(options, client, logger);
});
builder.Services.AddHttpClient(nameof(HttpPaymentGateway));
var aiOptions = AiProviderOptions.Bind(builder.Configuration);
aiOptions.Validate();
builder.Services.AddSingleton(aiOptions);
builder.Services.AddSingleton<IAiAssistant>(sp =>
{
    var options = sp.GetRequiredService<AiProviderOptions>();
    if (options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase) || !options.Enabled)
        return new LocalAiAssistant();
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpAiAssistant));
    client.BaseAddress = new Uri(options.Endpoint!);
    client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    var logger = sp.GetRequiredService<ILogger<HttpAiAssistant>>();
    return new HttpAiAssistant(options, client, logger);
});
builder.Services.AddHttpClient(nameof(HttpAiAssistant));
builder.Services.AddScoped<IBackgroundJobQueue, DurableBackgroundJobQueue>(); builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<BackgroundJobProcessor>(); builder.Services.AddHostedService<BackgroundJobWorker>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.IEmailSender<AppUser>, IdentityEmailSender>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue("DemoSeed:Enabled", false)) builder.Services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
var app = builder.Build(); app.UseExceptionHandler(); app.Use(async (context, next) => { context.Response.Headers.Append("X-Correlation-Id", Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier); await next(); }); app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseMiddleware<ActiveSessionMiddleware>(); app.UseAuthorization(); app.UseSwagger(); app.UseSwaggerUI();
app.MapGet("/api/v1/system", () => Results.Ok(new { name = "Trippify", apiVersion = "v1" })).RequireRateLimiting("api");
app.MapGet("/api/v1/system/protected", () => Results.NoContent()).RequireAuthorization();
app.MapIdentity();
app.MapGuides();
app.MapPlanning();
app.MapDiscovery();
app.MapCommerce();
app.MapLibrary();
app.MapReview();
app.MapVerifiedTrips();
app.MapEvidenceAttachmentEndpoints();
app.MapOperations();
app.MapNotifications();
app.MapVersioning();
app.MapPlugins();
app.MapManagedSaas();
app.MapAssistedImport();
app.MapCommercialRemixes();
app.MapSelfHosted();
app.MapBackgroundJobs();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("DemoSeed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
    await seeder.SeedAsync();
}
app.Run();
public partial class Program;
public sealed class PostgresHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
}
