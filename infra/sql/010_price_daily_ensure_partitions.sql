-- 010_price_daily_ensure_partitions.sql
-- Create tolerant partition-ensurer + migrate rows out of DEFAULT and drop it.

BEGIN;

-- 1) Idempotent, overlap-tolerant creator for monthly partitions
CREATE OR REPLACE FUNCTION market.ensure_price_daily_partitions(p_start date, p_end date)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
  d    date := date_trunc('month', p_start)::date;
  stop date := (date_trunc('month', p_end)::date + interval '1 month')::date;
  part text;
BEGIN
  IF p_start IS NULL OR p_end IS NULL OR p_start > p_end THEN
    RETURN;
  END IF;

  -- Avoid DDL races across concurrent writers
  PERFORM pg_advisory_xact_lock(hashtext('market.price_daily.partitions'));

  WHILE d < stop LOOP
    part := format('market.price_daily_%s', to_char(d, 'YYYY_MM'));
    BEGIN
      EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF market.price_daily
           FOR VALUES FROM (%L) TO (%L);',
        part, d, (d + interval '1 month')::date
      );
    EXCEPTION
      WHEN SQLSTATE '42P17' THEN
        -- Overlap with an existing partition (or DEFAULT) -> safe to ignore
        NULL;
    END;

    d := (d + interval '1 month')::date;
  END LOOP;
END
$$;

-- 2) If DEFAULT partition exists, move its rows to monthly partitions and drop it
DO $$
DECLARE
  has_default boolean;
  min_d date;
  max_d date;
BEGIN
  SELECT to_regclass('market.price_daily_default') IS NOT NULL INTO has_default;

  IF has_default THEN
    SELECT MIN(trade_date), MAX(trade_date)
      INTO min_d, max_d
      FROM market.price_daily_default;

    IF min_d IS NOT NULL THEN
      -- Ensure months exist for the full span
      PERFORM market.ensure_price_daily_partitions(min_d, max_d);

      -- Move data into the partitioned parent (routes to month partitions)
      INSERT INTO market.price_daily (
        instrument_id, trade_date, open, high, low, close, adj_close, volume, source, updated_at
      )
      SELECT
        instrument_id, trade_date, open, high, low, close, adj_close, volume, source, updated_at
      FROM market.price_daily_default
      ON CONFLICT (instrument_id, trade_date) DO NOTHING;

      -- Clear leftover rows (should be none after the insert)
      TRUNCATE TABLE market.price_daily_default;
    END IF;

    -- Drop DEFAULT partition to keep tidy monthly partitions going forward
    EXECUTE 'DROP TABLE IF EXISTS market.price_daily_default';
  END IF;
END$$;

COMMIT;
