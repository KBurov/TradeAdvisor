namespace PriceIngestor.Domain;

public sealed record Instrument(long InstrumentId, string Symbol, DateOnly? LastTradeDate);
