-- 005_classification_etf.sql
-- Sector/industry classification + ETF holdings

SET client_min_messages = WARNING;

-- Reference: sectors (e.g., GICS-level 1 or your own taxonomy)
CREATE TABLE IF NOT EXISTS market.sector (
  sector_id   SERIAL PRIMARY KEY,
  code        TEXT UNIQUE NOT NULL,        -- e.g., 'TECH'
  name        TEXT NOT NULL                -- e.g., 'Information Technology'
);

-- Reference: industries (deeper level)
CREATE TABLE IF NOT EXISTS market.industry (
  industry_id SERIAL PRIMARY KEY,
  code        TEXT UNIQUE NOT NULL,        -- e.g., 'SOFT'
  name        TEXT NOT NULL,               -- e.g., 'Software'
  sector_id   INTEGER REFERENCES market.sector(sector_id) ON DELETE SET NULL
);

-- Temporal classification for instruments (SCD2-lite)
CREATE TABLE IF NOT EXISTS market.instrument_classification (
  instrument_id  BIGINT NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  sector_id      INTEGER REFERENCES market.sector(sector_id) ON DELETE SET NULL,
  industry_id    INTEGER REFERENCES market.industry(industry_id) ON DELETE SET NULL,
  valid_from     DATE   NOT NULL DEFAULT CURRENT_DATE,
  valid_to       DATE,                             -- NULL => current
  PRIMARY KEY (instrument_id, valid_from)
);

CREATE INDEX IF NOT EXISTS ix_instr_class_current
  ON market.instrument_classification (instrument_id)
  WHERE valid_to IS NULL;

-- ETF holdings (snapshot by as_of_date)
CREATE TABLE IF NOT EXISTS market.etf_holding (
  etf_id           BIGINT NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  component_id     BIGINT NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  as_of_date       DATE   NOT NULL,
  weight           NUMERIC(9,6),          -- 0..1 (or percent/100)
  shares           NUMERIC(20,6),         -- optional: absolute shares
  market_value_usd NUMERIC(20,6),         -- optional
  source           TEXT,                  -- provider of the basket
  PRIMARY KEY (etf_id, component_id, as_of_date)
);

CREATE INDEX IF NOT EXISTS ix_etf_holding_asof
  ON market.etf_holding (as_of_date);

CREATE INDEX IF NOT EXISTS ix_etf_holding_component
  ON market.etf_holding (component_id);

COMMENT ON TABLE market.instrument_classification IS 'Temporal sector/industry classification per instrument.';
COMMENT ON TABLE market.etf_holding IS 'ETF → component composition snapshots by as_of_date.';
