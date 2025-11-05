using Dapper;

using PriceIngestor.Domain;

namespace PriceIngestor.Repositories;

public interface IPriceRepository
{
    Task EnsurePriceDailyPartitionsAsync(DateOnly start, DateOnly end, CancellationToken ct);
    Task UpsertDailyBatchAsync(long instrumentId, IEnumerable<PriceRow> rows, string source, CancellationToken ct);
}

public sealed class PriceRepository(string connectionString) : DataRepository(connectionString), IPriceRepository
{
    public async Task EnsurePriceDailyPartitionsAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        const string sql = "SELECT market.ensure_price_daily_partitions(@start, @end);";
        await using var cn = Conn();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { start, end }, cancellationToken: ct));
    }

    public async Task UpsertDailyBatchAsync(long instrumentId, IEnumerable<PriceRow> rows, string source, CancellationToken ct)
    {
        var list = rows as IList<PriceRow> ?? rows.ToList();
        if (list.Count == 0) return;

        var start = list.Min(r => r.TradeDate);
        var end = list.Max(r => r.TradeDate);
        // Ensure partitions exist for this batch window
        await EnsurePriceDailyPartitionsAsync(start, end, ct);

        const string upsert = """
        INSERT INTO market.price_daily
          (instrument_id, trade_date, open, high, low, close, adj_close, volume, source, updated_at)
        VALUES
          (@InstrumentId, @TradeDate, @Open, @High, @Low, @Close, @AdjClose, @Volume, @Source, NOW())
        ON CONFLICT (instrument_id, trade_date) DO UPDATE
        SET open = EXCLUDED.open,
            high = EXCLUDED.high,
            low = EXCLUDED.low,
            close = EXCLUDED.close,
            adj_close = EXCLUDED.adj_close,
            volume = EXCLUDED.volume,
            source = EXCLUDED.source,
            updated_at = NOW();
        """;

        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);

        // parameterized, safe
        foreach (var r in list)
        {
            var p = new
            {
                InstrumentId = instrumentId,
                r.TradeDate,
                r.Open,
                r.High,
                r.Low,
                r.Close,
                r.AdjClose,
                r.Volume,
                Source = source
            };
            await cn.ExecuteAsync(new CommandDefinition(upsert, p, transaction: tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }
}
