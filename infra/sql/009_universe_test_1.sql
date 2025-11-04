-- 009_universe_test_1.sql
-- Create/reset a 2-ticker test universe "test-1": AAPL, MSFT (NASDAQ)
-- Idempotent; safe to re-run. Preserves history via removed_at.

BEGIN;

-- 1) Ensure universe exists (insert or update) and return its id (used via CTEs below)
--    NOTE: CTE scope is per statement, so we repeat the CTEs for UPDATE and INSERT.

-- 2) Close any currently active members not in the desired set
WITH u AS (
  INSERT INTO market.universe (code, name, description)
  VALUES ('test-1', 'Test Universe (2 tickers)', 'Two-ticker test universe for ingestion/backfill validation')
  ON CONFLICT (code) DO UPDATE
    SET name = EXCLUDED.name,
        description = EXCLUDED.description
  RETURNING universe_id
),
desired AS (
  SELECT i.instrument_id
  FROM market.instrument i
  JOIN market.exchange e ON e.exchange_id = i.exchange_id
  WHERE (i.symbol, e.code) IN (('AAPL','NASDAQ'), ('MSFT','NASDAQ'))
),
univ AS (
  SELECT universe_id FROM u
)
UPDATE market.universe_member m
SET removed_at = NOW()
FROM univ
WHERE m.universe_id = univ.universe_id
  AND m.removed_at IS NULL
  AND m.instrument_id NOT IN (SELECT instrument_id FROM desired);

-- 3) Upsert desired members as active (reactivate if previously removed)
WITH u AS (
  INSERT INTO market.universe (code, name, description)
  VALUES ('test-1', 'Test Universe (2 tickers)', 'Two-ticker test universe for ingestion/backfill validation')
  ON CONFLICT (code) DO UPDATE
    SET name = EXCLUDED.name,
        description = EXCLUDED.description
  RETURNING universe_id
),
desired AS (
  SELECT i.instrument_id
  FROM market.instrument i
  JOIN market.exchange e ON e.exchange_id = i.exchange_id
  WHERE (i.symbol, e.code) IN (('AAPL','NASDAQ'), ('MSFT','NASDAQ'))
),
univ AS (
  SELECT universe_id FROM u
)
INSERT INTO market.universe_member (universe_id, instrument_id, added_at, removed_at)
SELECT univ.universe_id, d.instrument_id, NOW(), NULL
FROM univ CROSS JOIN desired d
ON CONFLICT (universe_id, instrument_id)
DO UPDATE SET removed_at = NULL;

COMMIT;

-- Verify (optional):
-- SELECT u.code AS universe_code, i.symbol, m.added_at, m.removed_at
-- FROM market.universe_member m
-- JOIN market.universe u USING (universe_id)
-- JOIN market.instrument i USING (instrument_id)
-- WHERE u.code = 'test-1'
-- ORDER BY i.symbol;
