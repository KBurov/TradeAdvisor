using Npgsql;

namespace PriceIngestor.Repositories;

public abstract class DataRepository(string connectionString)
{
    protected NpgsqlConnection Conn() => new(connectionString);
}
