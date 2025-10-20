# Database Schema

This document describes the **implemented** PostgreSQL schema as of migration `007_exchanges_global_eodhd.sql`.  
It will be updated with each new migration (`008_*`, `009_*`, …).

---

## Table of Contents
- [Schema & Extension](#schema--extension)
- [Applying Database Migrations](#applying-database-migrations)
- [Tables](#tables)
  - [1) market.exchange](#1-marketexchange-v001)
  - [2) market.instrument](#2-marketinstrument-v001)
  - [3) market.instrument_alias](#3-marketinstrument_alias-v001)
  - [4) market.universe](#4-marketuniverse-v001)
  - [5) market.universe_member](#5-marketuniverse_member-v001)
  - [6) View: market.v_universe_current](#6-view-marketv_universe_current-v001)
  - [7) market.price_daily (v002)](#7-marketprice_daily-v002)
  - [8) market.feature_daily (v003)](#8-marketfeature_daily-v003)
  - [9) market.news_item (v004)](#9-marketnews_item-v004)
  - [10) market.news_link (v004)](#10-marketnews_link-v004)
  - [11) market.sector (v005)](#11-marketsector-v005)
  - [12) market.industry (v005)](#12-marketindustry-v005)
  - [13) market.instrument_classification (v005)](#13-marketinstrument_classification-v005)
  - [14) market.etf_holding (v005)](#14-marketetf_holding-v005)
  - [15) market.data_provider (v006)](#15-marketdata_provider-v006)
  - [16) market.exchange_provider_code (v006)](#16-marketexchange_provider_code-v006)
  - [17) market.instrument_provider_symbol (v006)](#17-marketinstrument_provider_symbol-v006)
  - [18) Function: market.f_build_eodhd_symbol (v006)](#18-function-marketf_build_eodhd_symbol-v006)
- [Seed Data](#seed-data-from-001_market_coresql)
- [Seeding: Global Exchanges — EODHD Suffixes (v007)](#seeding-global-exchanges--eodhd-suffixes-v007)
- [Data Provider Update: Tiingo Base URL (v008)](#data-provider-update-tiingo-base-url-v008)
- [Common Queries](#common-queries)
- [General Notes & Rationale](#general-notes--rationale)

---

## Schema & Extension

- **Schema:** `market`
- **Extension:** `pg_trgm` (for fuzzy text search on instrument names)

---

## Applying Database Migrations

All migration scripts are located in `infra/sql/` and are executed in numeric order by `infra/migrate.sh`.

To apply all pending migrations:

```bash
cd infra
./migrate.sh
```

This script:
- Reads Postgres connection parameters from `infra/compose/.env` (preferred) or `.env.local`.
- Creates a tracking table `public.schema_version` (if missing).
- Applies each SQL file in order (e.g. `001_*.sql`, `002_*.sql`, …).
- Skips files that are already recorded as applied.

To verify the applied migrations:

```bash
PGPASSWORD=trade psql -h 127.0.0.1 -U trade -d trade -c \
  "SELECT version, filename, applied_at FROM public.schema_version ORDER BY version;"
```

> Tip: Always create a new migration file (e.g. `007_add_new_table.sql`) instead of editing previously applied scripts.

---

## Tables

### 1) `market.exchange` (v001)

Reference list of trading venues.
- **PK:** `exchange_id SERIAL`
- **Unique:** `code` (e.g., `NASDAQ`, `NYSE`), optional `mic` (e.g., `XNAS`, `XNYS`)
- **Fields:** `name`, `country` (default `US`), `timezone` (default `America/New_York`), `is_active` (default `true`)
- **Purpose:** Normalize venue metadata; referenced by instruments.

---

### 2) `market.instrument` (v001)

Canonical instruments (stocks, ETFs, etc.) with a stable ID.
- **PK:** `instrument_id BIGSERIAL`
- **FK:** `exchange_id → market.exchange(exchange_id)`
- **Unique:** `(exchange_id, symbol)`; optional uniques: `figi`, `cik`, `isin`
- **Indexes:** `GIN(name gin_trgm_ops)` for fuzzy search
- **Fields:** `symbol`, `name`, `type` (`EQUITY`, `ETF`, …), `currency` (`USD`), `status` (`ACTIVE`/`DELISTED`/`SUSPENDED`), `created_at`, `updated_at`
- **Purpose:** Stable identity for instruments even if symbols change.

---

### 3) `market.instrument_alias` (v001)

Alternate identifiers and historical symbols.
- **PK:** `alias_id BIGSERIAL`
- **FK:** `instrument_id → market.instrument(instrument_id)` **ON DELETE CASCADE**
- **Unique:** `(source, value)` (e.g., `('YAHOO','AAPL')`, `('OLD_SYMBOL','GOOG')`)
- **Fields:** `source`, `value`
- **Purpose:** Clean mapping to/from external providers and legacy names.

---

### 4) `market.universe` (v001)

Named selection sets (watchlists / processing scopes).
- **PK:** `universe_id SERIAL`
- **Unique:** `code` (e.g., `core`, `etf-core`, `tech-watch`)
- **Fields:** `name`, `description`
- **Purpose:** Decouple “what exists” (instruments) from “what we process now”.

---

### 5) `market.universe_member` (v001)

Temporal membership of instruments in universes.
- **PK:** `(universe_id, instrument_id)`
- **FKs:**  
  - `universe_id → market.universe(universe_id)` **ON DELETE CASCADE**  
  - `instrument_id → market.instrument(instrument_id)` **ON DELETE CASCADE**
- **Fields:** `added_at` (default now), `removed_at` (NULL = active)
- **Purpose:** Maintain history of additions/removals without losing auditability.

---

### 6) View: `market.v_universe_current` (v001)

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

### 9) `market.news_item` (v004)

Unified table for **news articles** and **social posts**, partitioned **monthly** by `published_at`.

- **PK:** `(published_at, news_id)`  ← composite key, required for partitioned table
- **Partitioning:** `PARTITION BY RANGE (published_at)`; one child per month
- **Columns:**
  - `kind TEXT` — `'NEWS'` or `'SOCIAL'`
  - `source TEXT` — provider (`REUTERS`, `BLOOMBERG`, `REDDIT`, `X`, `THREADS`, …)
  - `external_id TEXT` — provider’s primary id (tweet id, reddit id, article id)
  - `published_at TIMESTAMPTZ` — event time (UTC)
  - `url TEXT`, `author TEXT`, `lang TEXT`
  - `title TEXT`, `content TEXT`, `summary TEXT`
  - `sentiment_label TEXT` (`NEG`/`NEU`/`POS`), `sentiment_score NUMERIC(6,5)`
  - `topic_tags TEXT[]`
  - `engagement JSONB` — counts (likes, retweets, upvotes, …)
  - `raw JSONB` — original provider payload
  - `tsv_en tsvector (generated)` — full-text search over title+content
- **Indexes:**
  - `ix_news_item_published_at` (btree)
  - `ix_news_item_title_trgm` (GIN trigram for fuzzy title search)
  - `ix_news_item_tsv_en` (GIN full-text)
  - `ix_news_item_raw_gin` (GIN on raw JSONB)
  - `ix_news_item_source_external` (btree)
  - `ix_news_item_url` (btree)

**Rationale**
- A single table simplifies ingestion pipelines across providers.
- Monthly partitions keep search fast and enable retention policies.
- Both **trigram** and **full-text** indexes support different search styles (fuzzy vs. linguistic).

---

### 10) `market.news_link` (v004)

Links news/social items to instruments.

- **PK:** `(news_published_at, news_id, instrument_id)`
- **FKs:**  
  - `(news_published_at, news_id)` → `market.news_item(published_at, news_id)` (CASCADE)  
  - `instrument_id` → `market.instrument(instrument_id)` (CASCADE)
- **Columns:** `relevance REAL`, `method TEXT`
- **Index:** `ix_news_link_instrument` for quick “what news affected this instrument?”

**Rationale**
- Separates content from entity linking; supports many-to-many with relevance scoring.
- Multiple linking methods (NER, hashtag parsing, `$TICKER` rules) can co-exist.

---

### 11) `market.sector` (v005)

Reference table for sectors.
- **PK:** `sector_id`
- **Unique:** `code`
- **Fields:** `name`

---

### 12) `market.industry` (v005)

Reference table for industries (optionally linked to sector).
- **PK:** `industry_id`
- **Unique:** `code`
- **FK:** `sector_id → market.sector(sector_id) [SET NULL]`
- **Fields:** `name`

---

### 13) `market.instrument_classification` (v005)

Temporal classification (SCD2-lite) of instruments into sector/industry.
- **PK:** `(instrument_id, valid_from)`
- **FKs:**  
  - `instrument_id → market.instrument(instrument_id) [CASCADE]`  
  - `sector_id → market.sector(sector_id) [SET NULL]`  
  - `industry_id → market.industry(industry_id) [SET NULL]`
- **Fields:** `valid_from DATE`, `valid_to DATE NULL` (NULL = current)
- **Index:** `ix_instr_class_current` on `(instrument_id)` where `valid_to IS NULL`

**Rationale**
- Allows reclassification over time without losing history.
- Joins cleanly with features/prices on the date dimension.

---

### 14) `market.etf_holding` (v005)

ETF composition snapshots (constituents & weights) by `as_of_date`.
- **PK:** `(etf_id, component_id, as_of_date)`
- **FKs:** both `etf_id` and `component_id` → `market.instrument(instrument_id)` [CASCADE]
- **Fields:** `weight NUMERIC(9,6)`, `shares NUMERIC(20,6)`, `market_value_usd NUMERIC(20,6)`, `source TEXT`
- **Indexes:** `ix_etf_holding_asof` (as_of_date), `ix_etf_holding_component` (component_id)

**Rationale**
- Supports ETF-driven dependencies and sector/industry rollups.
- Enables features like “ETF-weighted sentiment” or “component-weighted returns”.

---

### 15) `market.data_provider` (v006)

Registry of external data providers (EODHD, Tiingo, etc.).

- **PK:** `provider_id`
- **Unique:** `code`
- **Fields:**  
  - `name` — human-readable provider name  
  - `base_url` — optional base API URL  
  - `notes` — free-form description  
- **Seed:**  
  - `EODHD` — EOD Historical Data  
  - `TIINGO` — Tiingo  

**Rationale**  
Defines a stable reference table so that all provider-specific mappings (symbols, suffixes, credentials) can reference a canonical provider ID.

---

### 16) `market.exchange_provider_code` (v006)

Maps each **exchange** to provider-specific suffixes or codes.

- **PK:** `(exchange_id, provider_id)`
- **FKs:**  
  - `exchange_id → market.exchange(exchange_id)` **[CASCADE]**  
  - `provider_id → market.data_provider(provider_id)` **[CASCADE]**
- **Fields:**  
  - `symbol_suffix` — e.g. `'US'`, `'L'`, `'TO'`, `'DE'`  
  - `provider_exchange_code` — optional code used by provider  
  - `is_default BOOLEAN` — marks the main mapping per provider  
  - `notes`, `updated_at`
- **Seed examples:**  
  - NASDAQ → `US` (EODHD)  
  - NYSE → `US` (EODHD)

**Rationale**  
EODHD and similar APIs derive ticker format from the **exchange suffix** (`AAPL.US`, `TSLA.US`, etc.).  
This table keeps that logic data-driven instead of hardcoded.

---

### 17) `market.instrument_provider_symbol` (v006)

Optional **per-instrument override** of provider symbol.

- **PK:** `(instrument_id, provider_id)`
- **FKs:**  
  - `instrument_id → market.instrument(instrument_id)` **[CASCADE]**  
  - `provider_id → market.data_provider(provider_id)` **[CASCADE]**
- **Fields:**  
  - `provider_symbol TEXT` — the exact symbol expected by provider  
  - `updated_at`
- **Index:** `ix_instr_provider_symbol_symbol` on `provider_symbol`

**Rationale**  
Covers exceptions where a provider uses a non-standard ticker format  
(e.g., `BRK-B.US` instead of `BRK.B.US`).

---

### 18) Function: `market.f_build_eodhd_symbol` (v006)

Helper SQL function that constructs the correct EODHD symbol.

- **Signature:**  
  ```sql
  market.f_build_eodhd_symbol(p_instrument_id BIGINT) RETURNS TEXT
  ```
- **Logic:**
  1. If an explicit override exists in `market.instrument_provider_symbol` → return it.  
  2. Otherwise, combine the base symbol from `market.instrument` with `.` and the suffix from  
     `market.exchange_provider_code.symbol_suffix` (e.g., `AAPL` + `.US` → `AAPL.US`).  
  3. If neither mapping exists, return the plain symbol.

- **Example:**

  ```sql
  SELECT i.symbol,
         market.f_build_eodhd_symbol(i.instrument_id) AS eodhd_symbol
  FROM market.instrument i
  WHERE i.symbol IN ('AAPL','MSFT','QQQ');
  ```

  | symbol | eodhd_symbol |
  |--------|--------------|
  | AAPL   | AAPL.US      |
  | MSFT   | MSFT.US      |
  | QQQ    | QQQ.US       |

- **Rationale:**  
  Centralizes provider symbol construction in SQL, avoiding hard-coded suffix logic in services  
  and simplifying future provider support.

---

## Seed Data (from `001_market_core.sql`)

- Exchanges: `NASDAQ (XNAS)`, `NYSE (XNYS)`
- Instruments: `AAPL`, `MSFT` (EQUITY), `QQQ` (ETF) on NASDAQ
- Universe: `core` with `AAPL`, `MSFT`, `QQQ` as active members

---

## Seeding: Global Exchanges — EODHD Suffixes (v007)

This migration seeds additional non-US exchanges and wires their **EODHD** symbol suffixes
into `market.exchange_provider_code`.

> **Important:** suffixes are stored **without the leading dot** (e.g., `US`, `L`, `TO`).  
> The final request symbol (e.g., `AAPL.US`) is produced by  
> `market.f_build_eodhd_symbol(instrument_id)` from **v006**.

**Mappings inserted/updated (`is_default = true`):**

| Exchange Code | MIC   | Exchange Name                           | Country | Suffix (no dot) |
|---------------|-------|-----------------------------------------|---------|-----------------|
| LSE           | XLON  | London Stock Exchange                   | GB      | L               |
| TSX           | XTSE  | Toronto Stock Exchange                  | CA      | TO              |
| TSXV          | XTSX  | TSX Venture Exchange                    | CA      | V               |
| XETRA         | XETR  | Deutsche Börse XETRA                    | DE      | DE              |
| SIX           | XSWX  | SIX Swiss Exchange                      | CH      | SW              |
| ENPAR         | XPAR  | Euronext Paris                          | FR      | PA              |
| ENAMS         | XAMS  | Euronext Amsterdam                      | NL      | AS              |
| ENBRU         | XBRU  | Euronext Brussels                       | BE      | BR              |
| ENLIS         | XLIS  | Euronext Lisbon                         | PT      | LS              |
| ENMIL         | XMIL  | Euronext Milan (Borsa Italiana)         | IT      | MI              |
| TSE           | XTKS  | Tokyo Stock Exchange                    | JP      | T               |
| HKEX          | XHKG  | Hong Kong Exchanges                     | HK      | HK              |
| ASX           | XASX  | Australian Securities Exchange          | AU      | AU              |

**Verify:**

```sql
SELECT e.code, e.mic, ep.symbol_suffix
FROM market.exchange e
JOIN market.exchange_provider_code ep ON ep.exchange_id = e.exchange_id
JOIN market.data_provider p ON p.provider_id = ep.provider_id
WHERE p.code = 'EODHD'
ORDER BY e.code;
```

**Notes:**
- These mappings are provider-specific to EODHD.
  Other providers can have their own rows in `market.exchange_provider_code`.
- To build API-ready symbols like `AAPL.US` or `BHP.AU`, call:

  ```sql
  SELECT market.f_build_eodhd_symbol(i.instrument_id)
  FROM market.instrument i
  WHERE i.symbol IN ('AAPL','BHP');
  ```

---

## Data Provider Update: Tiingo Base URL (v008)

This migration ensures that the **Tiingo** provider entry in  
`market.data_provider` has the correct canonical REST endpoint URL.

**SQL:**

```sql
INSERT INTO market.data_provider (code, name, base_url, notes)
VALUES
  ('TIINGO', 'Tiingo', 'https://api.tiingo.com', 'REST base URL for Tiingo (EOD & intraday).')
ON CONFLICT (code) DO UPDATE
SET base_url = EXCLUDED.base_url;
```

**Result:**

| code   | name   | base_url                                         | notes                                     |
| ------ | ------ | ------------------------------------------------ | ----------------------------------------- |
| TIINGO | Tiingo | [https://api.tiingo.com](https://api.tiingo.com) | REST base URL for Tiingo (EOD & intraday) |

**Rationale:**
- Keeps provider connection data centralized in SQL, not code.
- Enables ingestion services to build URLs dynamically from the database.
- Keeps the migration idempotent — re-running it is safe.

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

## Reference Data (Git-tracked)

Unlike other subfolders under `data/` that are ignored, **`data/reference/` is version-controlled**.
It contains small, deterministic seed datasets used by SQL seed scripts in `infra/sql/seed/`.

- Example: `data/reference/tickers_core.csv` — input for `infra/sql/seed/seed_instruments_from_csv.sql`
  (upserts exchanges & instruments and links them to the `core` universe).
- Keep files here **small and text-based** (CSV/JSON/YAML). Do **not** place runtime data in this folder.
- When adding new reference files, update the corresponding seed SQL in `infra/sql/seed/` to read them.

---

## General Notes & Rationale

- Use the **stable** `instrument_id` everywhere (prices, features, forecasts) to avoid identity issues on symbol changes.
- **Universes** give a flexible “selected set” mechanism with temporal history via `removed_at`.
- **Aliases** centralize cross-provider mappings.
- The **trigram index** on name supports admin/search tooling and data cleaning.
