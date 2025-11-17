-- 011_eodhd_base_url.sql
-- Purpose: ensure Eodhd has a canonical base_url in market.data_provider

INSERT INTO market.data_provider (code, name, base_url, notes)
VALUES
  ('EODHD', 'EOD Historical Data', 'https://eodhd.com', 'REST base URL for Eodhd (EOD & intraday).')
ON CONFLICT (code) DO UPDATE
SET base_url = EXCLUDED.base_url;
