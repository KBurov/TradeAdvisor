-- 012_provider_symbol_function.sql
-- Purpose: generalize provider symbol builder to accept provider code,
--          and keep f_build_eodhd_symbol() as a compatibility wrapper.

BEGIN;

-- 1) Generic function: build provider symbol by (instrument_id, provider_code)
--    Logic: same as old f_build_eodhd_symbol(), but parameterized by provider code.

DROP FUNCTION IF EXISTS market.f_build_provider_symbol(BIGINT, TEXT);

CREATE OR REPLACE FUNCTION market.f_build_provider_symbol(
    p_instrument_id  BIGINT,
    p_provider_code  TEXT
)
RETURNS TEXT
LANGUAGE sql
AS $$
WITH prov AS (
    SELECT provider_id
    FROM market.data_provider
    WHERE code = p_provider_code
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
    JOIN prov
      ON prov.provider_id = epc.provider_id
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

-- 2) Backward-compatible wrapper for EODHD-only usage.
--    Signature stays the same, but implementation delegates to the generic function.

DROP FUNCTION IF EXISTS market.f_build_eodhd_symbol(BIGINT);

CREATE OR REPLACE FUNCTION market.f_build_eodhd_symbol(
    p_instrument_id BIGINT
)
RETURNS TEXT
LANGUAGE sql
AS $$
SELECT market.f_build_provider_symbol(p_instrument_id, 'EODHD');
$$;

COMMIT;
