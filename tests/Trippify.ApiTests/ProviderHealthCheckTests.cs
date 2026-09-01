using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trippify.Api;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class ProviderHealthCheckTests
{
    [Fact]
    public async Task Object_storage_probe_writes_and_deletes_the_same_key()
    {
        var storage = new RecordingObjectStorage();
        var check = new ObjectStorageHealthCheck(storage);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        var puts = storage.PutsSnapshot();
        var deletes = storage.DeletesSnapshot();
        var put = Assert.Single(puts);
        var deleteCall = Assert.Single(deletes);
        Assert.Equal(put.Key, deleteCall);
    }

    [Fact]
    public async Task Object_storage_probe_marks_unhealthy_when_delete_fails()
    {
        var storage = new RecordingObjectStorage { DeleteError = new IOException("delete refused") };
        var check = new ObjectStorageHealthCheck(storage);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("delete refused", result.Description);
        Assert.Contains("cleanup", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("cleanupError"));
        Assert.Single(storage.PutsSnapshot());
        Assert.Single(storage.DeletesSnapshot());
    }

    [Fact]
    public async Task Object_storage_probe_marks_unhealthy_when_put_fails()
    {
        var storage = new RecordingObjectStorage { PutError = new IOException("write refused") };
        var check = new ObjectStorageHealthCheck(storage);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains(result.Data!, kvp => kvp.Key == "provider");
    }

    [Fact]
    public async Task Object_storage_probe_propagates_cancellation()
    {
        var storage = new RecordingObjectStorage();
        var check = new ObjectStorageHealthCheck(storage);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(NewContext(), cts.Token));
    }

    [Fact]
    public async Task Object_storage_probe_uses_unique_keys_per_invocation()
    {
        var storage = new RecordingObjectStorage();
        var check = new ObjectStorageHealthCheck(storage);

        var first = check.CheckHealthAsync(NewContext());
        var second = check.CheckHealthAsync(NewContext());
        await Task.WhenAll(first, second);

        var puts = storage.PutsSnapshot();
        Assert.Equal(2, puts.Count);
        Assert.Equal(2, storage.DeletesSnapshot().Count);
        Assert.NotEqual(puts[0].Key, puts[1].Key);
    }

    [Fact]
    public async Task Object_storage_probe_diagnostics_expose_provider_and_key_without_secrets()
    {
        var storage = new RecordingObjectStorage { ProviderName = "s3-compatible" };
        var check = new ObjectStorageHealthCheck(storage);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("s3-compatible", result.Data!["provider"]);
        Assert.True(result.Data.ContainsKey("key"));
        foreach (var kvp in result.Data)
        {
            Assert.DoesNotContain("secret", kvp.Key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("REDACTED", kvp.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Object_storage_probe_namespace_uses_healthcheck_prefix()
    {
        var storage = new RecordingObjectStorage();
        var check = new ObjectStorageHealthCheck(storage);

        await check.CheckHealthAsync(NewContext());

        var puts = storage.PutsSnapshot();
        var put = Assert.Single(puts);
        Assert.StartsWith("_healthchecks/", put.Key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Object_storage_probe_does_not_record_any_user_payload()
    {
        var storage = new RecordingObjectStorage();
        var check = new ObjectStorageHealthCheck(storage);

        await check.CheckHealthAsync(NewContext());

        var puts = storage.PutsSnapshot();
        var put = Assert.Single(puts);
        Assert.True(put.ContentLength <= 16, $"probe payload should be a tiny sentinel but was {put.ContentLength} bytes");
    }

    [Fact]
    public async Task Map_probe_reports_degraded_for_unresolved_known_query()
    {
        var map = new StubMapProvider("offline", _ => GeocodeResult.Unresolved("offline", "stub"));
        var check = new MapProviderHealthCheck(map);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Unresolved", result.Data!["status"]);
        Assert.Equal("offline", result.Data["provider"]);
    }

    [Fact]
    public async Task Map_probe_reports_healthy_when_known_query_resolves()
    {
        var map = new StubMapProvider("stub", _ => GeocodeResult.Resolved(0, 0, "stub", "stub", "stub-id"));
        var check = new MapProviderHealthCheck(map);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Resolved", result.Data!["status"]);
    }

    [Fact]
    public async Task Map_probe_reports_unhealthy_when_provider_throws()
    {
        var map = new ThrowingMapProvider();
        var check = new MapProviderHealthCheck(map);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Map_probe_propagates_cancellation()
    {
        var map = new StubMapProvider("stub", _ => GeocodeResult.Resolved(0, 0, "stub", "stub", null));
        var check = new MapProviderHealthCheck(map);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(NewContext(), cts.Token));
    }

    [Fact]
    public async Task Map_probe_diagnostics_omit_secrets()
    {
        var map = new StubMapProvider("super-secret-map", _ => GeocodeResult.Resolved(0, 0, "super-secret-map", "REDACTED-attribution", null));
        var check = new MapProviderHealthCheck(map);

        var result = await check.CheckHealthAsync(NewContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        foreach (var kvp in result.Data!)
        {
            Assert.NotEqual("REDACTED", kvp.Value);
            Assert.NotEqual("super-secret-map-key", kvp.Value);
        }
    }

    private static HealthCheckContext NewContext() => new()
    {
        Registration = new HealthCheckRegistration("test", _ => null!, HealthStatus.Unhealthy, tags: null),
    };

    private sealed class RecordingObjectStorage : IObjectStorage
    {
        private readonly ConcurrentQueue<(string Key, int ContentLength)> _puts = new();
        private readonly ConcurrentQueue<string> _deletes = new();
        public Exception? PutError { get; set; }
        public Exception? DeleteError { get; set; }
        public string ProviderName { get; set; } = "test-storage";

        public IReadOnlyList<(string Key, int ContentLength)> PutsSnapshot() => _puts.ToArray();
        public IReadOnlyList<string> DeletesSnapshot() => _deletes.ToArray();

        public Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PutError is not null) throw PutError;
            var length = (int?)content?.Length ?? 0;
            _puts.Enqueue((key, length));
            return Task.FromResult(new Uri($"local://test/{Uri.EscapeDataString(key)}"));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _deletes.Enqueue(key);
            if (DeleteError is not null) throw DeleteError;
            return Task.CompletedTask;
        }

        public Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken)
            => Task.FromResult(new Uri($"local://test/{Uri.EscapeDataString(key)}"));

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult(_puts.Any(p => p.Key == key));
    }

    private sealed class StubMapProvider : IMapProvider
    {
        private readonly Func<string, GeocodeResult> _handler;
        public StubMapProvider(string name, Func<string, GeocodeResult> handler)
        {
            ProviderName = name;
            _handler = handler;
        }
        public string ProviderName { get; }
        public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_handler(address));
        }
    }

    private sealed class ThrowingMapProvider : IMapProvider
    {
        public string ProviderName => "throwing";
        public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken)
            => throw new IOException("map offline");
    }
}
