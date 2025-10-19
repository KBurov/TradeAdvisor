-- 007_exchanges_global_eodhd.sql
-- Purpose: seed additional exchanges and map their EODHD suffixes

BEGIN;

-- --- Add exchanges if missing ---

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'LSE', 'XLON', 'London Stock Exchange', 'GB', 'Europe/London'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'LSE');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'TSX', 'XTSE', 'Toronto Stock Exchange', 'CA', 'America/Toronto'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'TSX');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'TSXV', 'XTSX', 'TSX Venture Exchange', 'CA', 'America/Toronto'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'TSXV');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'XETRA', 'XETR', 'Deutsche Börse XETRA', 'DE', 'Europe/Berlin'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'XETRA');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'SIX', 'XSWX', 'SIX Swiss Exchange', 'CH', 'Europe/Zurich'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'SIX');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'ENPAR', 'XPAR', 'Euronext Paris', 'FR', 'Europe/Paris'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'ENPAR');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'ENAMS', 'XAMS', 'Euronext Amsterdam', 'NL', 'Europe/Amsterdam'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'ENAMS');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'ENBRU', 'XBRU', 'Euronext Brussels', 'BE', 'Europe/Brussels'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'ENBRU');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'ENLIS', 'XLIS', 'Euronext Lisbon', 'PT', 'Europe/Lisbon'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'ENLIS');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'ENMIL', 'XMIL', 'Euronext Milan (Borsa Italiana)', 'IT', 'Europe/Rome'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'ENMIL');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'TSE', 'XTKS', 'Tokyo Stock Exchange', 'JP', 'Asia/Tokyo'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'TSE');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'HKEX', 'XHKG', 'Hong Kong Exchanges', 'HK', 'Asia/Hong_Kong'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'HKEX');

INSERT INTO market.exchange (code, mic, name, country, timezone)
SELECT 'ASX', 'XASX', 'Australian Securities Exchange', 'AU', 'Australia/Sydney'
WHERE NOT EXISTS (SELECT 1 FROM market.exchange WHERE code = 'ASX');

-- --- Map EODHD suffixes (is_default = true) ---

WITH p AS (SELECT provider_id FROM market.data_provider WHERE code = 'EODHD')
INSERT INTO market.exchange_provider_code (exchange_id, provider_id, symbol_suffix, provider_exchange_code, is_default, notes)
SELECT e.exchange_id, p.provider_id, s.suffix, NULL, TRUE, 'seed v007'
FROM (VALUES
  ('LSE',   'L'),
  ('TSX',   'TO'),
  ('TSXV',  'V'),
  ('XETRA', 'DE'),
  ('SIX',   'SW'),
  ('ENPAR', 'PA'),
  ('ENAMS', 'AS'),
  ('ENBRU', 'BR'),
  ('ENLIS', 'LS'),
  ('ENMIL', 'MI'),
  ('TSE',   'T'),
  ('HKEX',  'HK'),
  ('ASX',   'AU')
) AS s(code, suffix)
JOIN market.exchange e ON e.code = s.code
JOIN p ON TRUE
ON CONFLICT (exchange_id, provider_id)
DO UPDATE SET symbol_suffix = EXCLUDED.symbol_suffix,
              is_default   = TRUE,
              notes        = EXCLUDED.notes,
              updated_at   = now();

COMMIT;
