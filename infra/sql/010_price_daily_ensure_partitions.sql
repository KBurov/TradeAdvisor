-- 010_price_daily_ensure_partitions.sql
-- Ensure monthly partitions exist for market.price_daily for any date range.
-- Adds: market.ensure_price_daily_partitions(p_start date, p_end date)
-- Idempotent and concurrency-safe via advisory lock.

BEGIN;

CREATE OR REPLACE FUNCTION market.ensure_price_daily_partitions(p_start date, p_end date)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
  d    date := date_trunc('month', p_start)::date;
  stop date := (date_trunc('month', p_end)::date + interval '1 month')::date;
  part text;
BEGIN
  -- Guard: nothing to do
  IF p_start IS NULL OR p_end IS NULL THEN
    RETURN;
  END IF;

  -- Avoid races across concurrent writers in the same transaction scope
  PERFORM pg_advisory_xact_lock(hashtext('market.price_daily.partitions'));

  WHILE d < stop LOOP
    part := format('market.price_daily_%s', to_char(d, 'YYYY_MM'));
    EXECUTE format(
      'CREATE TABLE IF NOT EXISTS %I PARTITION OF market.price_daily
         FOR VALUES FROM (%L) TO (%L);',
      part, d, (d + interval '1 month')::date
    );
    d := (d + interval '1 month')::date;
  END LOOP;
END
$$;

-- Optional safety net: DEFAULT partition (keeps ingestion alive if ensure isn't called)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'market' AND c.relname = 'price_daily_default'
  ) THEN
    EXECUTE 'CREATE TABLE market.price_daily_default PARTITION OF market.price_daily DEFAULT';
  END IF;
END$$;

COMMIT;
