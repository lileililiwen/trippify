using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public sealed class MapProviderHealthCheck(IMapProvider map) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["provider"] = map.ProviderName,
        };
        try
        {
            var result = await map.GeocodeAsync("Tokyo", cancellationToken);
            data["status"] = result.Status.ToString();
            data["attribution"] = result.ProviderAttribution;
            return result.Status == GeocodeStatus.Resolved
                ? HealthCheckResult.Healthy(data: data)
                : HealthCheckResult.Degraded("Map provider returned unresolved for known query.", data: data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Map provider unavailable.", ex, data);
        }
    }
}

public sealed class ObjectStorageHealthCheck(IObjectStorage storage) : IHealthCheck
{
    private const string KeyPrefix = "_healthchecks/";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{Guid.NewGuid():N}";
        var data = new Dictionary<string, object>
        {
            ["provider"] = storage.ProviderName,
            ["key"] = key,
        };
        try
        {
            await using var stream = new MemoryStream(new byte[] { 0x20 });
            await storage.PutAsync(key, stream, cancellationToken);
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception cleanupEx)
            {
                data["cleanupError"] = cleanupEx.GetType().Name;
                return HealthCheckResult.Unhealthy(
                    $"Object storage probe cleanup failed: {cleanupEx.Message}",
                    exception: cleanupEx,
                    data: data);
            }
            return HealthCheckResult.Healthy(data: data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Object storage unavailable.", ex, data);
        }
    }
}
