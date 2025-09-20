# Database Schema (Initial Draft)

This document outlines the initial PostgreSQL schema for TradeAdvisor.
The schema will evolve as services mature.

---

## 1. Tickers
Tracks stocks and ETFs under analysis.

```sql
CREATE TABLE tickers (
    id SERIAL PRIMARY KEY,
    symbol VARCHAR(16) UNIQUE NOT NULL,
    name TEXT,
    type VARCHAR(16) NOT NULL CHECK (type IN ('stock', 'etf')),
    sector TEXT,
    industry TEXT,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);
```

---

## 2. Prices
Historical and live OHLCV (Open, High, Low, Close, Volume).

```sql
CREATE TABLE prices (
    id BIGSERIAL PRIMARY KEY,
    ticker_id INT REFERENCES tickers(id),
    ts TIMESTAMPTZ NOT NULL,
    open NUMERIC(18,6),
    high NUMERIC(18,6),
    low NUMERIC(18,6),
    close NUMERIC(18,6),
    volume BIGINT,
    UNIQUE (ticker_id, ts)
) PARTITION BY RANGE (ts);
```

Partitioning strategy: monthly partitions (e.g., `prices_2025_01`).

---

## 3. Features
Stores computed technical indicators, windowed statistics, etc.

```sql
CREATE TABLE features (
    id BIGSERIAL PRIMARY KEY,
    ticker_id INT REFERENCES tickers(id),
    ts TIMESTAMPTZ NOT NULL,
    feature JSONB NOT NULL,
    UNIQUE (ticker_id, ts)
);
```

Indexes:
- GIN index on feature for JSON queries
- btree on (ticker_id, ts)

---

## 4. News & Social
Ingested news headlines and social media posts.

```sql
CREATE TABLE news_social (
    id BIGSERIAL PRIMARY KEY,
    ticker_id INT REFERENCES tickers(id),
    ts TIMESTAMPTZ NOT NULL,
    source VARCHAR(32),
    headline TEXT,
    sentiment NUMERIC(5,4),  -- range -1.0 to 1.0
    meta JSONB
);
```

---

## 5. Models
Registered models and metadata.

```sql
CREATE TABLE models (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    version TEXT NOT NULL,
    framework TEXT,         -- e.g. pytorch, tensorflow, lightgbm
    path TEXT,              -- storage location in MinIO/S3
    created_at TIMESTAMPTZ DEFAULT now(),
    registered_by TEXT,
    UNIQUE (name, version)
);
```

---

## 6. Forecasts
Stores model forecasts for each ticker at a given timestamp.

```sql
CREATE TABLE forecasts (
    id BIGSERIAL PRIMARY KEY,
    ticker_id INT REFERENCES tickers(id),
    model_id INT REFERENCES models(id),
    ts TIMESTAMPTZ NOT NULL,
    horizon INTERVAL,       -- e.g. 1d, 1h
    forecast NUMERIC(18,6),
    confidence NUMERIC(5,4),
    created_at TIMESTAMPTZ DEFAULT now()
);
```

---

## 7. Backtests
Tracks evaluation results of models.

```sql
CREATE TABLE backtests (
    id SERIAL PRIMARY KEY,
    model_id INT REFERENCES models(id),
    strategy TEXT,
    start_ts TIMESTAMPTZ,
    end_ts TIMESTAMPTZ,
    sharpe NUMERIC(8,4),
    sortino NUMERIC(8,4),
    max_drawdown NUMERIC(8,4),
    meta JSONB,
    created_at TIMESTAMPTZ DEFAULT now()
);
```

---

## Notes
- Use uuid keys later for distributed safety if needed.
- Keep schema migrations under version control (Alembic for Python, EF Migrations for .NET).
- Revisit partitioning & indexing strategy once data grows.
