using PriceIngestor.Repositories;

namespace PriceIngestor.Services;

public interface IUniverseResolver
{
    Task<string> ResolveAsync(string? requestedUniverse, CancellationToken ct);
}

public sealed class UniverseResolver : IUniverseResolver
{
    private readonly IInstrumentRepository _instruments;
    private readonly IConfiguration _cfg;

    public UniverseResolver(IInstrumentRepository instruments, IConfiguration cfg)
    {
        _instruments = instruments;
        _cfg = cfg;
    }

    public async Task<string> ResolveAsync(string? requestedUniverse, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requestedUniverse))
            return requestedUniverse!;

        var core = await _instruments.TryGetCoreIfExistsAsync(ct);
        if (!string.IsNullOrWhiteSpace(core))
            return core!;

        return _cfg.GetSection("Ingest")["UniverseCode"] ?? "core";
    }
}
