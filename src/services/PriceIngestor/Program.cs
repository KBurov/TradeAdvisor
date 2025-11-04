using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

using Common.Data.Dapper;

using PriceIngestor.Repositories;
using PriceIngestor.Services;

var builder = WebApplication.CreateBuilder(args);

DapperBootstrap.EnsureRegistered();

// Serilog (structured console)
builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console();
});

// Precedence: env var overrides appsettings
var postgreSqlConnectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string not configured.");

// DI
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IInstrumentRepository>(sp => new InstrumentRepository(postgreSqlConnectionString));
builder.Services.AddSingleton<IPriceRepository>(sp => new PriceRepository(postgreSqlConnectionString));
builder.Services.AddSingleton<IDataProviderRepository>(sp =>
{
    var cache = sp.GetRequiredService<IMemoryCache>();
    return new DataProviderRepository(postgreSqlConnectionString, cache);
});
builder.Services.AddHttpClient("tiingo", http =>
{
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    http.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),   // refresh DNS/pooled connections
    ConnectTimeout = TimeSpan.FromSeconds(10)             // fail fast on dead endpoints
});
builder.Services.AddKeyedSingleton<IBarFetcher, TiingoFetcher>("long");
builder.Services.AddKeyedSingleton<IBarFetcher, TiingoFetcher>("short");
builder.Services.AddSingleton<IUniverseResolver, UniverseResolver>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapPost("/ingest/run-today", async (
    [FromQuery] string? universe,
    IUniverseResolver resolver,
    IInstrumentRepository instruments,
    [FromKeyedServices("short")] IBarFetcher fetcher,
    IPriceRepository prices,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    var source = cfg.GetSection("Ingest")["SourceTag"] ?? "tiingo";
    var resolvedUniverse = await resolver.ResolveAsync(universe, ct);
    var day = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    var list = await instruments.GetByUniverseAsync(resolvedUniverse, ct);
    var results = new List<object>();
    var ok = 0; var fail = 0;

    foreach (var inst in list)
    {
        try
        {
            var rows = await fetcher.GetDailyAsync(inst.Symbol, day, day, ct);
            await prices.UpsertDailyBatchAsync(inst.InstrumentId, rows, source, ct);
            Log.Information("Upserted {Count} rows for {Symbol} {Date} (universe={Universe})",
                rows.Count, inst.Symbol, day, resolvedUniverse);
            results.Add(new { inst.Symbol, rows = rows.Count, status = "ok" });
            ok++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed for {Symbol} {Date} (universe={Universe})", inst.Symbol, day, resolvedUniverse);
            results.Add(new { inst.Symbol, rows = 0, status = "error", error = ex.Message });
            fail++;
        }
    }

    return Results.Ok(new { universe = resolvedUniverse, date = day, instruments = list.Count, ok, fail, results });
});

app.MapPost("/ingest/backfill", async (
    [FromQuery] DateOnly start,
    [FromQuery] DateOnly? end,
    [FromQuery] string? universe,
    IUniverseResolver resolver,
    IInstrumentRepository instruments,
    [FromKeyedServices("long")] IBarFetcher fetcher,
    IPriceRepository prices,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    var source = cfg.GetSection("Ingest")["SourceTag"] ?? "yahoo";
    var to = end ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var resolvedUniverse = await resolver.ResolveAsync(universe, ct);

    var list = await instruments.GetByUniverseAsync(resolvedUniverse, ct);
    var results = new List<object>();
    var ok = 0; var fail = 0;

    foreach (var inst in list)
    {
        try
        {
            var rows = await fetcher.GetDailyAsync(inst.Symbol, start, to, ct);
            await prices.UpsertDailyBatchAsync(inst.InstrumentId, rows, source, ct);
            Log.Information("Upserted {Count} rows for {Symbol} {Start}->{End} (universe={Universe})",
                rows.Count, inst.Symbol, start, to, resolvedUniverse);
            results.Add(new { inst.Symbol, rows = rows.Count, status = "ok" });
            ok++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed for {Symbol} {Start}->{End} (universe={Universe})", inst.Symbol, start, to, resolvedUniverse);
            results.Add(new { inst.Symbol, rows = 0, status = "error", error = ex.Message });
            fail++;
        }
    }

    return Results.Ok(new { universe = resolvedUniverse, start, end = to, instruments = list.Count, ok, fail, results });
});

// Swagger in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
