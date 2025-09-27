-- 001_market_core.sql
-- Minimal core: schema + exchanges + instruments + aliases + universes
-- Safe to run multiple times (uses IF NOT EXISTS / ON CONFLICT)

-- Create schema
CREATE SCHEMA IF NOT EXISTS market;

-- Helpful extension for fuzzy text search
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Exchanges
CREATE TABLE IF NOT EXISTS market.exchange (
  exchange_id   SERIAL PRIMARY KEY,
  code          TEXT UNIQUE NOT NULL,         -- e.g., 'NYSE', 'NASDAQ'
  name          TEXT NOT NULL,
  country       TEXT DEFAULT 'US',
  timezone      TEXT DEFAULT 'America/New_York',
  mic           TEXT UNIQUE,                  -- e.g., XNYS, XNAS
  is_active     BOOLEAN NOT NULL DEFAULT TRUE
);

-- Instruments
CREATE TABLE IF NOT EXISTS market.instrument (
  instrument_id     BIGSERIAL PRIMARY KEY,
  symbol            TEXT NOT NULL,            -- e.g., 'AAPL'
  name              TEXT NOT NULL,
  exchange_id       INTEGER REFERENCES market.exchange(exchange_id),
  type              TEXT NOT NULL,            -- 'EQUITY','ETF',...
  currency          TEXT NOT NULL DEFAULT 'USD',
  figi              TEXT UNIQUE,
  cik               TEXT UNIQUE,
  isin              TEXT UNIQUE,
  status            TEXT NOT NULL DEFAULT 'ACTIVE',  -- 'ACTIVE','DELISTED','SUSPENDED'
  created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Unique per exchange+symbol
CREATE UNIQUE INDEX IF NOT EXISTS ux_instrument_exchange_symbol
  ON market.instrument (exchange_id, symbol);

-- Fuzzy search on name
CREATE INDEX IF NOT EXISTS ix_instrument_name_trgm
  ON market.instrument USING GIN (name gin_trgm_ops);

-- Instrument aliases (provider IDs, old symbols, etc.)
CREATE TABLE IF NOT EXISTS market.instrument_alias (
  alias_id        BIGSERIAL PRIMARY KEY,
  instrument_id   BIGINT NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  source          TEXT NOT NULL,     -- 'YAHOO','POLYGON','ALPHA_VANTAGE','OLD_SYMBOL','CUSIP','ISIN','FIGI'
  value           TEXT NOT NULL,
  UNIQUE (source, value)
);

CREATE INDEX IF NOT EXISTS ix_alias_instrument_id
  ON market.instrument_alias(instrument_id);

-- Universes (selected sets) + membership
CREATE TABLE IF NOT EXISTS market.universe (
  universe_id  SERIAL PRIMARY KEY,
  code         TEXT UNIQUE NOT NULL,      -- e.g., 'core','etf-core','tech-watch'
  name         TEXT NOT NULL,
  description  TEXT
);

CREATE TABLE IF NOT EXISTS market.universe_member (
  universe_id     INTEGER NOT NULL REFERENCES market.universe(universe_id) ON DELETE CASCADE,
  instrument_id   BIGINT  NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  added_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  removed_at      TIMESTAMPTZ,
  PRIMARY KEY (universe_id, instrument_id)
);

CREATE OR REPLACE VIEW market.v_universe_current AS
SELECT u.code AS universe_code, m.universe_id, m.instrument_id
FROM market.universe_member m
JOIN market.universe u ON u.universe_id = m.universe_id
WHERE m.removed_at IS NULL;

-- Seed a couple of rows (idempotent)
INSERT INTO market.exchange (code, name, mic) VALUES
  ('NASDAQ','NASDAQ Stock Market','XNAS'),
  ('NYSE','New York Stock Exchange','XNYS')
ON CONFLICT (code) DO NOTHING;

INSERT INTO market.instrument (symbol, name, exchange_id, type)
SELECT 'AAPL','Apple Inc.', e.exchange_id, 'EQUITY' FROM market.exchange e WHERE e.code='NASDAQ'
ON CONFLICT DO NOTHING;

INSERT INTO market.instrument (symbol, name, exchange_id, type)
SELECT 'MSFT','Microsoft Corp.', e.exchange_id, 'EQUITY' FROM market.exchange e WHERE e.code='NASDAQ'
ON CONFLICT DO NOTHING;

INSERT INTO market.instrument (symbol, name, exchange_id, type)
SELECT 'QQQ','Invesco QQQ Trust', e.exchange_id, 'ETF' FROM market.exchange e WHERE e.code='NASDAQ'
ON CONFLICT DO NOTHING;

INSERT INTO market.universe (code, name, description)
VALUES ('core','Core selection','Base instruments the system processes by default')
ON CONFLICT (code) DO NOTHING;

INSERT INTO market.universe_member (universe_id, instrument_id)
SELECT u.universe_id, i.instrument_id
FROM market.universe u
JOIN market.instrument i ON i.symbol IN ('AAPL','MSFT','QQQ')
WHERE u.code='core'
ON CONFLICT DO NOTHING;
