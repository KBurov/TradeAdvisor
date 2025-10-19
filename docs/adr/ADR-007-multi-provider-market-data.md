# ADR-007 — Multi-Provider Market Data Architecture

**Status:** Accepted  
**Date:** 2025-10-19  

---

## Context

The TradeAdvisor system must support **multiple historical and real-time data providers** (e.g., EOD Historical Data, Tiingo, Polygon, Alpha Vantage, etc.).  
Each provider uses different ticker formats, API endpoints, and rate limits, making direct integration brittle if symbol mappings are hardcoded in services.

We also plan to mix providers — using one (e.g., Tiingo) for initial bulk data loads and another (e.g., EODHD) for ongoing incremental updates.  
To achieve this flexibly, the provider abstraction must live **in the database layer**, not scattered across ingestion services.

---

## Decision

We introduced a **provider-agnostic data schema** using three key tables and one helper function:

1. **`market.data_provider`** — registry of known data providers.  
2. **`market.exchange_provider_code`** — mapping of exchange codes to provider-specific suffixes or codes.  
3. **`market.instrument_provider_symbol`** — optional per-instrument overrides for provider-specific tickers.  
4. **`market.f_build_eodhd_symbol()`** — helper function to construct correct EODHD symbols dynamically.

This approach allows ingestion and analytics services to resolve the proper provider symbol directly in SQL without duplicating provider logic in application code.

---

## Consequences

**Advantages**
- Decouples service logic from provider-specific symbol rules.  
- Enables quick onboarding of new providers via migrations only.  
- Keeps symbol construction auditable and version-controlled.  
- Simplifies testing and troubleshooting by centralizing provider mappings.

**Trade-offs**
- Slightly increases schema complexity.  
- Requires maintaining consistent seeding of provider mappings (`exchange_provider_code`).  
- Services must always query provider symbols before API requests.

---

## Related Migrations

- `006_add_data_providers.sql` — introduces provider tables and symbol builder function.  
- `007_exchanges_global_eodhd.sql` — seeds global exchange suffixes for EODHD.

---

## Future Work

- Implement dual-ingestion strategy:
  - Tiingo for initial historical backfill.
  - EODHD for continuous updates.
- Extend same pattern to:
  - Intraday and fundamental data.
  - News, sentiment, and ETF holdings if provider-specific differences emerge.
- Add optional table for **provider credentials and API usage quotas**.

---

## Status

This ADR is **Accepted** and will serve as the architectural reference for all future data ingestion providers.
