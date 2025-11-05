using Dapper;

using PriceIngestor.Domain;

namespace PriceIngestor.Repositories;

public interface IInstrumentRepository
{
    Task<IReadOnlyList<Instrument>> GetByUniverseAsync(string universeCode, CancellationToken ct);
    Task<string?> TryGetCoreIfExistsAsync(CancellationToken ct); // DB default helper
}

public sealed class InstrumentRepository(string connectionString) : DataRepository(connectionString), IInstrumentRepository
{
    public async Task<IReadOnlyList<Instrument>> GetByUniverseAsync(string universeCode, CancellationToken ct)
    {
        const string sql = """
            SELECT i.instrument_id AS InstrumentId,
                   i.symbol        AS Symbol,
                   MAX(p.trade_date) AS LastTradeDate
            FROM market.v_universe_current c
            JOIN market.instrument i USING (instrument_id)
            LEFT JOIN market.price_daily p ON p.instrument_id = i.instrument_id
            WHERE c.universe_code = @code
            GROUP BY i.instrument_id, i.symbol
            ORDER BY i.symbol;
        """;

        await using var cn = Conn();
        var rows = await cn.QueryAsync<Instrument>(new CommandDefinition(sql, new { code = universeCode }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<string?> TryGetCoreIfExistsAsync(CancellationToken ct)
    {
        // If a 'core' universe exists, use it as DB-side default
        const string sql = "SELECT code FROM market.universe WHERE code = 'core' LIMIT 1;";

        await using var cn = Conn();

        return await cn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, cancellationToken: ct));
    }
}
