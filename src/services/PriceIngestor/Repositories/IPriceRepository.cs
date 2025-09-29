using Dapper;
using Npgsql;
using PriceIngestor.Domain;

namespace PriceIngestor.Repositories;

public interface IPriceRepository
{
    Task UpsertDailyBatchAsync(int instrumentId, IEnumerable<PriceRow> rows, string source, CancellationToken ct);
}

public sealed class PriceRepository : IPriceRepository
{
    private readonly string _connStr;
    public PriceRepository(IConfiguration cfg) => _connStr = cfg.GetConnectionString("Postgres")!;

    private NpgsqlConnection Conn() => new(_connStr);

    public async Task UpsertDailyBatchAsync(int instrumentId, IEnumerable<PriceRow> rows, string source, CancellationToken ct)
    {
        var list = rows as IList<PriceRow> ?? rows.ToList();
        if (list.Count == 0) return;

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
                TradeDate = r.TradeDate,
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
