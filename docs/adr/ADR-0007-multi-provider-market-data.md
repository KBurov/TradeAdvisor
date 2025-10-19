# ADR-0007: Multi-Provider Market Data Architecture

## Context
The TradeAdvisor system must support multiple historical and real-time data providers (e.g., **EOD Historical Data**, **Tiingo**, **Polygon**, **Alpha Vantage**).  
Each provider uses different symbol conventions, API endpoints, rate limits, and coverage rules.  
We need a flexible way to integrate several providers for the same instruments without duplicating or hardcoding logic in ingestion services.

Requirements:
- Support **Tiingo** for initial database population (bulk backfill).
- Use **EODHD** and other providers for incremental daily updates.
- Keep provider-specific logic and mappings **data-driven**, not code-driven.
- Allow new providers to be added via SQL migrations only.

## Decision
- Introduce three normalized reference tables and one SQL helper function:
  - `market.data_provider` — registry of data providers.
  - `market.exchange_provider_code` — per-provider exchange suffix or code mapping.
  - `market.instrument_provider_symbol` — optional per-instrument symbol override.
  - `market.f_build_eodhd_symbol()` — SQL function for building canonical EODHD tickers.
- Service logic will query the correct provider symbol dynamically from the database.

## Status
Accepted.

## Consequences
- **Pros:**
  - Removes hardcoded provider logic from application services.
  - Simplifies onboarding of new providers through schema migrations only.
  - Centralizes symbol rules and suffixes for auditing and debugging.
  - Keeps provider-specific data versioned and queryable in SQL.

- **Cons:**
  - Slightly increases schema complexity.
  - Requires consistent seeding of provider mappings (`exchange_provider_code`).
  - Services must resolve provider symbols before API requests.

## Implementation Notes
- Introduced in migrations:
  - `006_add_data_providers.sql`
  - `007_exchanges_global_eodhd.sql`
- Typical lookup pattern:
  ```sql
  SELECT market.f_build_eodhd_symbol(i.instrument_id)
  FROM market.instrument i
  WHERE i.symbol = 'AAPL';
  ```
- Example EODHD mappings:
  - NASDAQ → `US`
  - LSE → `L`
  - TSX → `TO`
  - XETRA → `DE`
- Function output: `AAPL.US`, `BHP.AU`, etc.

## Future Work
- Extend to other providers (Polygon, Alpha Vantage, Yahoo Finance).
- Add support for intraday and fundamental data using the same schema.
- Introduce a table for provider credentials and API rate-limit metadata.
- Optionally cache provider symbols in Redis for high-throughput ingestion.