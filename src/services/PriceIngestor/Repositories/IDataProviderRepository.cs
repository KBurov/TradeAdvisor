using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PriceIngestor.Repositories;

public enum DataProvider
{
    Tiingo = 0,
    Eodhd = 1
}

public static class DataProviderExtensions
{
    public static string ToDbName(this DataProvider provider) =>
    provider switch
    {
        DataProvider.Tiingo => "TIINGO",
        DataProvider.Eodhd => "EODHD",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}

public interface IDataProviderRepository
{
    // Returns base_url from DB for given provider code (or null if not set).
    Task<string?> GetBaseUrlAsync(DataProvider provider, CancellationToken ct);
}

public sealed class DataProviderRepository(string connectionString, IMemoryCache cache) : DataRepository(connectionString), IDataProviderRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public async Task<string?> GetBaseUrlAsync(DataProvider provider, CancellationToken ct)
    {
        var key = $"provider:baseurl:{provider.ToDbName()}";
        if (cache.TryGetValue(key, out string? cached))
            return cached;

        const string sql = """
            SELECT base_url
            FROM market.data_provider
            WHERE code = @code
            LIMIT 1
        """;

        await using var cn = Conn();

        var url = await cn.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { code = provider.ToDbName() }, cancellationToken: ct));

        // Cache even nulls to avoid hammering DB if not configured (shorter TTL)
        if (url is null)
            return cache.Set<string?>(key, null, TimeSpan.FromMinutes(5));

        return cache.Set(key, url, CacheTtl);
    }
}
