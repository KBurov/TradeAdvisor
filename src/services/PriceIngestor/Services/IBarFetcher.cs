using PriceIngestor.Domain;

namespace PriceIngestor.Services;

public interface IBarFetcher
{
    Task<IReadOnlyList<PriceRow>> GetDailyAsync(string symbol, DateOnly start, DateOnly end, CancellationToken ct);
}