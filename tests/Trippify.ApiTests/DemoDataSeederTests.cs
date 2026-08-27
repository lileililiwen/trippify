using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class SeederEnabledFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoSeed:Enabled"] = "true",
        }));
        builder.ConfigureServices(services =>
        {
            var databaseName = "trippify-api-tests-" + Guid.NewGuid();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        });
    }
}

public sealed class DemoDataSeederTests(SeederEnabledFactory factory) : IClassFixture<SeederEnabledFactory>
{
    [Fact]
    public async Task SeedAsync_when_database_is_empty_populates_every_aggregate()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();

        await seeder.SeedAsync();

        Assert.Equal(7, await db.Users.CountAsync());
        Assert.Equal(7, await db.UserProfiles.CountAsync());
        Assert.Equal(1, await db.CreatorProfiles.CountAsync());
        Assert.Equal(3, await db.TravelGuides.CountAsync());
        Assert.Equal(4, await db.GuideDays.CountAsync());
        Assert.Equal(11, await db.GuideNodes.CountAsync());
        Assert.Equal(2, await db.GuideSections.CountAsync());
        Assert.Equal(3, await db.GuideReleases.CountAsync());
        Assert.Equal(3, await db.GuideDiscounts.CountAsync());
        Assert.Equal(4, await db.GuideReviews.CountAsync());
        Assert.Equal(1, await db.ReviewReplies.CountAsync());
        Assert.Equal(1, await db.GuideOrders.CountAsync(o => o.Status == OrderStatus.Paid));
        Assert.Equal(1, await db.PurchaseEntitlements.CountAsync());
        Assert.Equal(3, await db.CommerceLedgerEntries.CountAsync());
        Assert.Equal(1, await db.RevenueShares.CountAsync());
        Assert.Equal(1, await db.UserTrips.CountAsync());
        Assert.Equal(1, await db.GuideFavorites.CountAsync());
        Assert.Equal(1, await db.TripEvidence.CountAsync());
        Assert.Equal(1, await db.EvidenceReviews.CountAsync());
        Assert.Equal(1, await db.VerifiedGuideBadges.CountAsync());
        Assert.Equal(5, await db.ActualTripMetrics.CountAsync());
        Assert.Equal(2, await db.CreatorFollows.CountAsync());
        Assert.Equal(3, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.Plugins.CountAsync());
        Assert.Equal(1, await db.PluginInstallations.CountAsync());
        Assert.Equal(1, await db.PluginPermissionGrants.CountAsync());
        Assert.Equal(1, await db.Tenants.CountAsync());
        Assert.Equal(1, await db.Subscriptions.CountAsync());
        Assert.Equal(2, await db.QuotaUsages.CountAsync());
        Assert.Equal(1, await db.LicensePolicies.CountAsync());
        Assert.Equal(1, await db.FeatureFlags.CountAsync());
        Assert.Equal(1, await db.IdentityAuditEntries.CountAsync());
        Assert.Equal(1, await db.ImportJobs.CountAsync());
        Assert.Equal(1, await db.ImportDrafts.CountAsync());
        Assert.Equal(1, await db.Translations.CountAsync());
        Assert.Equal(1, await db.AiQuotaUsages.CountAsync());

        var admin = await db.Users.SingleAsync(user => user.Email == "admin@example.com");
        Assert.Contains("Administrator", await db.UserRoles
            .Where(userRole => userRole.UserId == admin.Id)
            .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
            .ToListAsync());

        Assert.Equal(5, await db.ActualTripMetrics.Select(metric => metric.UserId).Distinct().CountAsync());
    }

    [Fact]
    public async Task SeedAsync_is_idempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();

        await seeder.SeedAsync();
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(7, await db.Users.CountAsync());
        Assert.Equal(3, await db.TravelGuides.CountAsync());
        Assert.Equal(4, await db.GuideReviews.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_when_database_contains_a_user_preserves_existing_data()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();

        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "existing@example.com",
            NormalizedUserName = "EXISTING@EXAMPLE.COM",
            Email = "existing@example.com",
            NormalizedEmail = "EXISTING@EXAMPLE.COM",
        });
        await db.SaveChangesAsync();

        await seeder.SeedAsync();

        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(0, await db.TravelGuides.CountAsync());
    }
}
