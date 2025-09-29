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

            // YahooFinanceApi is synchronous per request; wrap with Task.Run to honor ct minimally
            var candles = await Task.Run(() => Yahoo.GetHistoricalAsync(symbol, s, e, Period.Daily), ct);

            var list = candles
                .OrderBy(c => c.DateTime)
                .Select(c => new PriceRow(
                    DateOnly.FromDateTime(c.DateTime.Date),
                    (decimal)c.Open,
                    (decimal)c.High,
                    (decimal)c.Low,
                    (decimal)c.Close,
                    (decimal)c.AdjustedClose,
                    (long)c.Volume
                ))
                .ToList();

            return (IReadOnlyList<PriceRow>)list;
        });
    }
}
