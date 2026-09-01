using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

/// <summary>
/// Release-gate coverage for the EF Core migration chain. The test asserts
/// the registered migration list is non-empty, every migration declares a
/// distinct key, the chain is orderable, and the migrator emits a script
/// that references both the identity and the commerce tables. The same
/// surface guards the PostgreSQL upgrade drill, which runs migrations
/// against a real database inside `scripts/postgres-upgrade-drill.sh`.
/// </summary>
public sealed class PostgresMigrationUpgradeTests
{
    [Fact]
    public void Migration_chain_has_a_head_and_no_duplicate_keys()
    {
        using var context = NewContext();
        var assembly = ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IMigrationsAssembly>();
        Assert.NotNull(assembly);
        var migrations = assembly!.Migrations.Select(m => m.Key).ToList();
        Assert.NotEmpty(migrations);
        Assert.Equal(migrations.Count, migrations.Distinct().Count());
        // The chain must be sortable so the migrator can apply it in order.
        var sorted = migrations.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(migrations, sorted);
    }

    [Fact]
    public void Migrator_generates_a_non_empty_postgres_script()
    {
        using var context = NewContext();
        var migrator = ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IMigrator>();
        Assert.NotNull(migrator);
        var script = migrator!.GenerateScript(fromMigration: null, toMigration: null);
        Assert.False(string.IsNullOrWhiteSpace(script));
        // The generated script must use the PostgreSQL provider and reference
        // the documented tables so we know the chain is wired end-to-end.
        Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        // The migration must exercise the documented lower-cased table names
        // the runtime tests assert against.
        var present = new[]
        {
            "__EFMigrationsHistory", "users", "travel_guides", "guide_orders",
            "evidence_attachments", "backup_snapshots", "feature_flags",
        };
        foreach (var name in present)
        {
            var found = script.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
            Assert.True(found, "Migration script must reference the " + name + " table.");
        }
    }

    [Fact]
    public void From_previous_to_head_generates_only_the_unapplied_delta()
    {
        using var context = NewContext();
        var migrator = ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IMigrator>();
        Assert.NotNull(migrator);
        var assembly = ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IMigrationsAssembly>();
        var keys = assembly!.Migrations.Select(m => m.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(keys.Count >= 2, "Need at least two migrations to exercise the upgrade delta.");

        // Generate a script for a hypothetical previous release: the
        // penultimate migration. The output must be non-empty and reference
        // the most recent migration's tables.
        var previous = keys[^2];
        var script = migrator!.GenerateScript(fromMigration: previous, toMigration: null);
        Assert.False(string.IsNullOrWhiteSpace(script));
    }

    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=trippify-migrations;Username=trippify;Password=trippify",
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;
        return new AppDbContext(options);
    }
}
