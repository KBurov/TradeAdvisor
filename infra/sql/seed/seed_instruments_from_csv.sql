-- Reads data/reference/tickers_core.csv and upserts exchanges & instruments,
-- then links them to the 'core' universe (idempotent).

-- 0) Ensure 'core' universe exists (idempotent)
INSERT INTO market.universe(code, name, description)
VALUES ('core','Core universe','Primary tickers for development & testing')
ON CONFLICT (code) DO NOTHING;

-- 1) Stage CSV rows into a temp table (same session)
CREATE TEMP TABLE _incoming_instruments (
  symbol text,
  exchange_code text,
  name text,
  type text
);

\copy _incoming_instruments(symbol,exchange_code,name,type) FROM 'data/reference/tickers_core.csv' CSV HEADER

-- 2) Ensure referenced exchanges exist (minimal upsert)
INSERT INTO market.exchange(code, name)
SELECT DISTINCT exchange_code, exchange_code
FROM _incoming_instruments i
WHERE NOT EXISTS (
  SELECT 1 FROM market.exchange e WHERE e.code = i.exchange_code
);

-- 3) Upsert instruments (by (exchange_id, symbol))
INSERT INTO market.instrument(symbol, name, exchange_id, type)
SELECT i.symbol,
       i.name,
       e.exchange_id,
       i.type
FROM _incoming_instruments i
JOIN market.exchange e ON e.code = i.exchange_code
ON CONFLICT (exchange_id, symbol) DO UPDATE
SET name = EXCLUDED.name,
    type = EXCLUDED.type,
    updated_at = NOW();

-- 4) Link instruments to the 'core' universe (idempotent)
WITH core_universe AS (
  SELECT universe_id FROM market.universe WHERE code = 'core'
),
to_link AS (
  SELECT i.instrument_id, cu.universe_id
  FROM _incoming_instruments inc
  JOIN market.exchange e ON e.code = inc.exchange_code
  JOIN market.instrument i ON i.symbol = inc.symbol AND i.exchange_id = e.exchange_id
  CROSS JOIN core_universe cu
)
INSERT INTO market.universe_member (universe_id, instrument_id)
SELECT universe_id, instrument_id
FROM to_link
ON CONFLICT DO NOTHING;

-- 5) Show a quick summary (optional)
SELECT i.symbol, e.code AS exchange, i.type
FROM market.instrument i
JOIN market.exchange e ON e.exchange_id = i.exchange_id
WHERE i.symbol IN (
  SELECT DISTINCT symbol FROM _incoming_instruments
)
ORDER BY i.symbol;
