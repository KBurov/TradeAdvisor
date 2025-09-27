# Database Schema

This document describes the **implemented** PostgreSQL schema as of migration `002_price_daily.sql`.  
It will be updated with each new migration (`003_*`, `004_*`, …).

---

## Schema & Extension
- **Schema:** `market`
- **Extension:** `pg_trgm` (for fuzzy text search on instrument names)

---

## Tables

### 1) `market.exchange`
Reference list of trading venues.
- **PK:** `exchange_id SERIAL`
- **Unique:** `code` (e.g., `NASDAQ`, `NYSE`), optional `mic` (e.g., `XNAS`, `XNYS`)
- **Fields:** `name`, `country` (default `US`), `timezone` (default `America/New_York`), `is_active` (default `true`)
- **Purpose:** Normalize venue metadata; referenced by instruments.

---

### 2) `market.instrument`
Canonical instruments (stocks, ETFs, etc.) with a stable ID.
- **PK:** `instrument_id BIGSERIAL`
- **FK:** `exchange_id → market.exchange(exchange_id)`
- **Unique:** `(exchange_id, symbol)`; optional uniques: `figi`, `cik`, `isin`
- **Indexes:** `GIN(name gin_trgm_ops)` for fuzzy search
- **Fields:** `symbol`, `name`, `type` (`EQUITY`, `ETF`, …), `currency` (`USD`), `status` (`ACTIVE`/`DELISTED`/`SUSPENDED`), `created_at`, `updated_at`
- **Purpose:** Stable identity for instruments even if symbols change.

---

### 3) `market.instrument_alias`
Alternate identifiers and historical symbols.
- **PK:** `alias_id BIGSERIAL`
- **FK:** `instrument_id → market.instrument(instrument_id)` **ON DELETE CASCADE**
- **Unique:** `(source, value)` (e.g., `('YAHOO','AAPL')`, `('OLD_SYMBOL','GOOG')`)
- **Fields:** `source`, `value`
- **Purpose:** Clean mapping to/from external providers and legacy names.

---

### 4) `market.universe`
Named selection sets (watchlists / processing scopes).
- **PK:** `universe_id SERIAL`
- **Unique:** `code` (e.g., `core`, `etf-core`, `tech-watch`)
- **Fields:** `name`, `description`
- **Purpose:** Decouple “what exists” (instruments) from “what we process now”.

---

### 5) `market.universe_member`
Temporal membership of instruments in universes.
- **PK:** `(universe_id, instrument_id)`
- **FKs:**  
  - `universe_id → market.universe(universe_id)` **ON DELETE CASCADE**  
  - `instrument_id → market.instrument(instrument_id)` **ON DELETE CASCADE**
- **Fields:** `added_at` (default now), `removed_at` (NULL = active)
- **Purpose:** Maintain history of additions/removals without losing auditability.

---

### 6) View: `market.v_universe_current`
Convenience view of **current** (active) universe memberships.
- **Columns:** `universe_code`, `universe_id`, `instrument_id`
- **Definition:** joins `market.universe_member` to `market.universe` where `removed_at IS NULL`

---

### 7) `market.price_daily` (v002)
Daily OHLCV per instrument, **partitioned monthly** on `trade_date`.
- **PK:** `(instrument_id, trade_date)`
- **FK:** `instrument_id → market.instrument(instrument_id)` (CASCADE on delete)
- **Columns:** `open`, `high`, `low`, `close`, `adj_close`, `volume`, `source`, `updated_at`
- **Partitioning:** `PARTITION BY RANGE (trade_date)` with one child table per month
- **Index:** `ix_price_daily_trade_date` (partitioned index on `trade_date`)

**Rationale**
- Daily bars don’t need time zones → `trade_date DATE`.
- Monthly partitions keep indexes small and enable fast date-range queries.
- Easy retention/archival (detach/drop older partitions if needed).
- `PRIMARY KEY (instrument_id, trade_date)` prevents duplicates from multiple providers.

**Notes**
- Insert/upsert should use the **canonical `instrument_id`**.
- For intraday bars, a separate partitioned table will be added (e.g., `price_intraday`).

---

### 8) `market.feature_daily` (v003)

Daily feature payloads per instrument. Supports multiple pipelines per day.

- **PK:** `(instrument_id, trade_date, pipeline)`
- **FK:** `instrument_id → market.instrument(instrument_id)` (CASCADE on delete)
- **Columns:**  
  - `pipeline TEXT` — feature set name (e.g., `tech`, `nlp`, `risk`, `default`)  
  - `version TEXT` — optional pipeline version tag  
  - `features JSONB` — computed features (flexible schema)  
  - `updated_at TIMESTAMPTZ` — audit timestamp
- **Indexes:**  
  - `ix_feature_daily_trade_date` (btree)  
  - `ix_feature_daily_instrument` (btree)  
  - `ix_feature_daily_features_gin` (GIN on `features`)

**Rationale**
- JSONB keeps the schema flexible while we iterate on features.
- `pipeline` lets us store multiple independent feature sets for the same day.
- GIN index enables fast existence/containment queries on feature keys.

**Notes**
- Always join/resolve with **`instrument_id`** (canonical id).
- Consider adding **expression indexes** later for hot features (e.g., `((features->>'rsi_14')::numeric)`).

---

## Seed Data (from `001_market_core.sql`)
- Exchanges: `NASDAQ (XNAS)`, `NYSE (XNYS)`
- Instruments: `AAPL`, `MSFT` (EQUITY), `QQQ` (ETF) on NASDAQ
- Universe: `core` with `AAPL`, `MSFT`, `QQQ` as active members

---

## Common Queries

### Current members of a universe

```sql
SELECT i.instrument_id, i.symbol
FROM market.v_universe_current c
JOIN market.instrument i USING (instrument_id)
WHERE c.universe_code = 'core'
ORDER BY i.symbol;
```

### Resolve by exact symbol + exchange

```sql
SELECT i.instrument_id, i.symbol
FROM market.instrument i
JOIN market.exchange e ON e.exchange_id = i.exchange_id
WHERE i.symbol = 'AAPL' AND e.code = 'NASDAQ';
```

### Resolve via alias (e.g., from a provider)

```sql
SELECT i.instrument_id, i.symbol
FROM market.instrument_alias a
JOIN market.instrument i ON i.instrument_id = a.instrument_id
WHERE a.source = 'YAHOO' AND a.value = 'AAPL';
```

---

## General Notes & Rationale

- Use the **stable** `instrument_id` everywhere (prices, features, forecasts) to avoid identity issues on symbol changes.
- **Universes** give a flexible “selected set” mechanism with temporal history via `removed_at`.
- **Aliases** centralize cross-provider mappings.
- The **trigram index** on name supports admin/search tooling and data cleaning.
