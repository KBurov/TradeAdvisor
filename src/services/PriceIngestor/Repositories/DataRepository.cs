using Npgsql;

namespace PriceIngestor.Repositories;

public abstract class DataRepository(IConfiguration cfg)
{
    private readonly string _connStr = cfg.GetConnectionString("Postgres")!;

    protected NpgsqlConnection Conn() => new(_connStr);
}
