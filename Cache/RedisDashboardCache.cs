using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace CadastraAI.API.Cache;

/// Versioned per-empresa cache. Invalidating an empresa just bumps its version
/// counter — old keys orphan and expire naturally, no SCAN required.
public sealed class RedisDashboardCache(IDistributedCache cache, IConfiguration config) : IDashboardCache
{
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(
        config.GetValue<int?>("Cache:DashboardTtlSeconds") ?? 1800);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrSetAsync<T>(
        Guid empresaId,
        string keySuffix,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct) where T : class
    {
        var version = await GetOrInitVersionAsync(empresaId, ct);
        var key = BuildKey(empresaId, version, keySuffix);

        var hit = await cache.GetAsync(key, ct);
        if (hit is not null)
        {
            try
            {
                var cached = JsonSerializer.Deserialize<T>(hit, JsonOpts);
                if (cached is not null) return cached;
            }
            catch (JsonException) { /* fall through and recompute */ }
        }

        var fresh = await factory(ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(fresh, JsonOpts);
        await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl,
        }, ct);
        return fresh;
    }

    public async Task InvalidateEmpresaAsync(Guid empresaId, CancellationToken ct)
    {
        var versionKey = VersionKey(empresaId);
        var current = await GetOrInitVersionAsync(empresaId, ct);
        var next = current + 1;
        await cache.SetStringAsync(versionKey, next.ToString(), ct);
    }

    private async Task<long> GetOrInitVersionAsync(Guid empresaId, CancellationToken ct)
    {
        var versionKey = VersionKey(empresaId);
        var raw = await cache.GetStringAsync(versionKey, ct);
        if (raw is not null && long.TryParse(raw, out var v)) return v;

        await cache.SetStringAsync(versionKey, "1", ct);
        return 1;
    }

    private static string VersionKey(Guid empresaId) => $"dash:v:{empresaId:N}";

    private static string BuildKey(Guid empresaId, long version, string suffix) =>
        $"dash:{empresaId:N}:v{version}:{suffix}";
}
