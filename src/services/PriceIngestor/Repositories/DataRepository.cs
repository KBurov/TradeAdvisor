using Npgsql;

public abstract class DataRepository
{
    private readonly string _connStr;

    protected NpgsqlConnection Conn() => new(_connStr);

    protected DataRepository(IConfiguration cfg) => _connStr = cfg.GetConnectionString("Postgres")!;
}