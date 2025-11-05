-- 002_prices_core.sql
-- Daily OHLCV table, partitioned by month on trade_date.
-- Creates a rolling window of monthly partitions (past 24 months + next 12).

SET client_min_messages = WARNING;

-- Base partitioned table
CREATE TABLE IF NOT EXISTS market.price_daily (
  instrument_id  BIGINT NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  trade_date     DATE   NOT NULL,
  open           NUMERIC(18,6) NOT NULL,
  high           NUMERIC(18,6) NOT NULL,
  low            NUMERIC(18,6) NOT NULL,
  close          NUMERIC(18,6) NOT NULL,
  adj_close      NUMERIC(18,6),     -- optional, from providers that adjust for splits/dividends
  volume         BIGINT,
  source         TEXT,               -- optional: 'ALPACA','POLYGON','YFINANCE', etc.
  updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (instrument_id, trade_date)
) PARTITION BY RANGE (trade_date);

-- Global (partitioned) index for common date filtering
CREATE INDEX IF NOT EXISTS ix_price_daily_trade_date
  ON market.price_daily (trade_date);

-- Create monthly partitions for [now - 24 months, now + 12 months]
DO $$
DECLARE
  start_month DATE := date_trunc('month', current_date - INTERVAL '24 months')::date;
  end_month   DATE := date_trunc('month', current_date + INTERVAL '12 months')::date;
  d           DATE := start_month;
  next_d      DATE;
  part_name   TEXT;
BEGIN
  WHILE d <= end_month LOOP
    next_d := (d + INTERVAL '1 month')::date;
    part_name := format('market.price_daily_%s', to_char(d, 'YYYY_MM'));

    EXECUTE format(
      'CREATE TABLE IF NOT EXISTS market.%I PARTITION OF market.price_daily
       FOR VALUES FROM (%L) TO (%L);',
       part_name, d, next_d
    );

    -- Local indexes can be created automatically, but if you need extras per partition,
    -- put them here with EXECUTE.

    d := next_d;
  END LOOP;
END $$;

COMMENT ON TABLE market.price_daily IS 'Daily OHLCV per instrument; partitioned monthly by trade_date.';
COMMENT ON COLUMN market.price_daily.source IS 'Upstream provider/source tag (optional).';
