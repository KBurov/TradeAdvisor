namespace PriceIngestor.Domain;

public readonly record struct PriceRow(
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjClose,
    long Volume
);
