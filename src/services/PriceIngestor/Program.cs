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
    [FromKeyedServices("short")] IBarFetcher shortFetcher,
    [FromKeyedServices("long")]  IBarFetcher longFetcher,
    IPriceRepository prices,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    var shortSource = cfg.GetValue<string>("Ingest:ShortSourceTag") ?? "eodhd";
    var longSource = cfg.GetValue<string>("Ingest:LongSourceTag")  ?? "tiingo";
    var resolvedUniverse = await resolver.ResolveAsync(universe, ct);

    var staleDays = cfg.GetValue<int?>("Ingest:StaleDays") ?? 183;
    var longBackfillYears = cfg.GetValue<int?>("Ingest:LongBackfillYears") ?? 3;

    var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var list  = await instruments.GetByUniverseAsync(resolvedUniverse, ct);

    var results = new List<object>(list.Count);
    var ok = 0; var fail = 0;

    foreach (var inst in list)
    {
        try
        {
            var last = inst.LastTradeDate;
            var useLong = last is null || (today.DayNumber - last.Value.DayNumber) > staleDays;
            var start = useLong
                ? new DateOnly(today.Year - longBackfillYears, today.Month, today.Day)
                : last!.Value.AddDays(1);
            var source = useLong ? longSource : shortSource;

            // nothing to do?
            if (start > today)
            {
                results.Add(new { inst.Symbol, rows = 0, status = "up-to-date" });
                continue;
            }

            var fetcher = useLong ? longFetcher : shortFetcher;
            var rows = await fetcher.GetDailyAsync(inst.Symbol, start, today, ct);

            if (rows.Count > 0)
                await prices.UpsertDailyBatchAsync(inst.InstrumentId, rows, source, ct);

            Log.Information(
                "{Fetcher} upserted {Count} rows for {Symbol} {Start}->{End} (universe={Universe}) last={Last}",
                useLong ? "long" : "short", rows.Count, inst.Symbol, start, today, resolvedUniverse, last);

            results.Add(new { inst.Symbol, rows = rows.Count, status = "ok", mode = useLong ? "long" : "short" });
            ok++;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed for {Symbol} (universe={Universe})", inst.Symbol, resolvedUniverse);
            results.Add(new { inst.Symbol, rows = 0, status = "error", error = ex.Message });
            fail++;
        }
    }

    return Results.Ok(new { universe = resolvedUniverse, date = today, instruments = list.Count, ok, fail, results });
});

// Swagger in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
