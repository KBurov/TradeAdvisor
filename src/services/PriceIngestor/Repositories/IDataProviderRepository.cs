using System.Data;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Caching.Memory;

namespace PriceIngestor.Repositories;

public interface IDataProviderRepository
{
    /// Returns base_url from DB for given provider code (or null if not set).
    Task<string?> GetBaseUrlAsync(string providerCode, CancellationToken ct);
}

public sealed class DataProviderRepository : DataRepository, IDataProviderRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;

    public DataProviderRepository(
        IConfiguration cfg,
        IMemoryCache cache) : base(cfg) => _cache = cache;

    public async Task<string?> GetBaseUrlAsync(string providerCode, CancellationToken ct)
    {
        var key = $"provider:baseurl:{providerCode}";
        if (_cache.TryGetValue(key, out string? cached))
            return cached;

        const string sql = """
            SELECT base_url
            FROM market.data_provider
            WHERE code = @code
            LIMIT 1
        """;

        await using var cn = Conn();

        var url = await cn.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { code = providerCode }, cancellationToken: ct));

        // Cache even nulls to avoid hammering DB if not configured (shorter TTL)
        if (url is null)
            return _cache.Set<string?>(key, null, TimeSpan.FromMinutes(5));

        return _cache.Set(key, url, CacheTtl);
    }
}
