-- 008_tiingo_base_url.sql
-- Purpose: ensure Tiingo has a canonical base_url in market.data_provider

INSERT INTO market.data_provider (code, name, base_url, notes)
VALUES
  ('TIINGO', 'Tiingo', 'https://api.tiingo.com', 'REST base URL for Tiingo (EOD & intraday).')
ON CONFLICT (code) DO UPDATE
SET base_url = EXCLUDED.base_url;
