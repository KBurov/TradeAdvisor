-- 006_exchange_eodhd_suffix.sql
-- Purpose: store provider-specific symbol rules on the EXCHANGE
--          and optional per-instrument overrides for EODHD/Tiingo/etc.

BEGIN;

-- 0) Provider registry (idempotent)
CREATE TABLE IF NOT EXISTS market.data_provider (
    provider_id SERIAL PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,      -- 'EODHD', 'TIINGO', ...
    name TEXT NOT NULL,
    base_url TEXT,
    notes TEXT
);

INSERT INTO market.data_provider (code, name)
VALUES ('EODHD','EOD Historical Data'),
       ('TIINGO','Tiingo')
ON CONFLICT (code) DO NOTHING;

-- 1) Exchange → provider mapping (suffix / exchange code)
--    Example for EODHD: AAPL on NASDAQ → 'AAPL.US'
CREATE TABLE IF NOT EXISTS market.exchange_provider_code (
    exchange_id INT NOT NULL REFERENCES market.exchange(exchange_id) ON DELETE CASCADE,
    provider_id INT NOT NULL REFERENCES market.data_provider(provider_id) ON DELETE CASCADE,
    symbol_suffix TEXT,             -- e.g., 'US', 'L', 'TO', etc. (appended as .<suffix>)
    provider_exchange_code TEXT,    -- optional: the provider's exchange code if they require it
    is_default BOOLEAN NOT NULL DEFAULT TRUE, -- for providers that might have multiple variants
    notes TEXT,
    updated_at TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (exchange_id, provider_id)
);

-- 2) Optional per-instrument override of the provider symbol
CREATE TABLE IF NOT EXISTS market.instrument_provider_symbol (
    instrument_id BIGINT NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
    provider_id   INT    NOT NULL REFERENCES market.data_provider(provider_id) ON DELETE CASCADE,
    provider_symbol TEXT NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (instrument_id, provider_id)
);

CREATE INDEX IF NOT EXISTS ix_instr_provider_symbol_symbol
  ON market.instrument_provider_symbol(provider_symbol);

-- 3) Convenience function to compute the EODHD symbol
--    Rule: if override exists → use it; else symbol || '.' || exchange.suffix (when suffix is present).
CREATE OR REPLACE FUNCTION market.f_build_eodhd_symbol(p_instrument_id BIGINT)
RETURNS TEXT
LANGUAGE sql
AS $$
WITH prov AS (
  SELECT provider_id FROM market.data_provider WHERE code = 'EODHD'
),
ovr AS (
  SELECT ips.provider_symbol
  FROM market.instrument_provider_symbol ips, prov
  WHERE ips.instrument_id = p_instrument_id
    AND ips.provider_id   = prov.provider_id
),
x AS (
  SELECT i.symbol, epc.symbol_suffix
  FROM market.instrument i
  JOIN market.exchange_provider_code epc
    ON epc.exchange_id = i.exchange_id
  JOIN prov ON prov.provider_id = epc.provider_id
  WHERE i.instrument_id = p_instrument_id
)
SELECT
  COALESCE(
    (SELECT provider_symbol FROM ovr),
    CASE
      WHEN (SELECT symbol_suffix FROM x) IS NOT NULL
        THEN (SELECT symbol FROM x) || '.' || (SELECT symbol_suffix FROM x)
      ELSE (SELECT symbol FROM x)   -- fallback: bare symbol if no suffix known
    END
  );
$$;

-- 4) Minimal seed for common US venues (extend later via CSV/SQL):
--    NASDAQ, NYSE → '.US'
INSERT INTO market.exchange_provider_code (exchange_id, provider_id, symbol_suffix, provider_exchange_code, is_default, notes)
SELECT e.exchange_id, p.provider_id, 'US', NULL, TRUE, 'Default US suffix'
FROM market.exchange e
CROSS JOIN market.data_provider p
WHERE e.code IN ('NASDAQ','NYSE')
  AND p.code = 'EODHD'
ON CONFLICT (exchange_id, provider_id) DO NOTHING;

COMMIT;
