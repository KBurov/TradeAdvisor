using Polly;
using Polly.Retry;
using PriceIngestor.Domain;
using YahooFinanceApi;

namespace PriceIngestor.Services;

public sealed class YahooFetcher : IBarFetcher
{
    // Basic retry: 3 attempts, jittered backoff
    private static readonly AsyncRetryPolicy Retry = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2.5),
            TimeSpan.FromSeconds(5)
        });

    public async Task<IReadOnlyList<PriceRow>> GetDailyAsync(string symbol, DateOnly start, DateOnly end, CancellationToken ct)
    {
        return await Retry.ExecuteAsync(async () =>
        {
            var s = new DateTime(start.Year, start.Month, start.Day, 0, 0, 0, DateTimeKind.Utc);
            var e = new DateTime(end.Year, end.Month, end.Day, 23, 59, 59, DateTimeKind.Utc);

            var candles = await Task.Run(() => Yahoo.GetHistoricalAsync(symbol, s, e, Period.Daily), ct);

            var list = candles
                .OrderBy(c => c.DateTime)
                .Select(c =>
                {
                    // Avoid null-coalescing on non-nullable decimals.
                    // Some lib versions expose AdjustedClose as decimal?; others as decimal.
                    // Treat "missing" adjusted close as equal to close.
                    decimal adjClose;
                    try
                    {
                        // If AdjustedClose is nullable:
                        // (dynamic) to avoid compile-time ambiguity across lib versions.
                        dynamic dc = c;
                        var maybeAdj = dc.AdjustedClose;
                        adjClose = maybeAdj is null ? dc.Close : (decimal)maybeAdj;
                    }
                    catch
                    {
                        // If property not present / different type, fallback safely
                        adjClose = c.Close;
                    }

                    return new PriceRow(
                        DateOnly.FromDateTime(c.DateTime.Date),
                        c.Open,
                        c.High,
                        c.Low,
                        c.Close,
                        adjClose,
                        (long)c.Volume
                    );
                })
                .ToList();

            return (IReadOnlyList<PriceRow>)list;
        });
    }
}
