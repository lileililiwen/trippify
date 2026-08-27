using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public sealed class MapProviderHealthCheck(IMapProvider map) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await map.GeocodeAsync("Tokyo", cancellationToken);
            return result.Status == GeocodeStatus.Resolved
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Map provider returned unresolved for known query.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Map provider unavailable.", ex);
        }
    }
}

public sealed class ObjectStorageHealthCheck(IObjectStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new MemoryStream(new byte[] { 0x20 });
            await storage.PutAsync($"_healthchecks/{Guid.NewGuid():N}", stream, cancellationToken);
            await storage.DeleteAsync($"_healthchecks/{Guid.NewGuid():N}", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Object storage unavailable.", ex);
        }
    }
}
