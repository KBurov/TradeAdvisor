-- 003_features_daily.sql
-- Daily features per instrument. Supports multiple pipelines per day.

SET client_min_messages = WARNING;

CREATE TABLE IF NOT EXISTS market.feature_daily (
  instrument_id  BIGINT      NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  trade_date     DATE        NOT NULL,
  pipeline       TEXT        NOT NULL DEFAULT 'default',  -- e.g., 'tech', 'nlp', 'risk', 'default'
  version        TEXT,                                    -- optional: semantic version of the pipeline
  features       JSONB       NOT NULL,                    -- computed feature payload
  updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (instrument_id, trade_date, pipeline)
);

-- Helpful indexes
CREATE INDEX IF NOT EXISTS ix_feature_daily_trade_date
  ON market.feature_daily (trade_date);

CREATE INDEX IF NOT EXISTS ix_feature_daily_instrument
  ON market.feature_daily (instrument_id);

-- JSONB GIN index for existence/containment queries (e.g., features ? 'rsi_14')
CREATE INDEX IF NOT EXISTS ix_feature_daily_features_gin
  ON market.feature_daily
  USING GIN (features);

COMMENT ON TABLE  market.feature_daily IS 'Daily features per instrument; supports multiple pipelines per day.';
COMMENT ON COLUMN market.feature_daily.pipeline IS 'Feature pipeline name (e.g., tech/nlp/risk).';
COMMENT ON COLUMN market.feature_daily.version  IS 'Optional pipeline version tag.';
