-- 012_provider_symbol_function.sql
-- Purpose: generalize provider symbol builder to accept provider code,
--          and keep f_build_eodhd_symbol() as a compatibility wrapper.

BEGIN;

-- 1) Generic function: build provider symbol by (instrument_id, provider_code)
--    Behavior:
--      - if an override exists in market.instrument_provider_symbol → use it
--      - else if an exchange suffix exists in market.exchange_provider_code → symbol.suffix
--      - else → plain instrument symbol
--
--    Works correctly even if there is no row in exchange_provider_code
--    for the given provider (falls back to base symbol).

DROP FUNCTION IF EXISTS market.f_build_provider_symbol(BIGINT, TEXT);

CREATE OR REPLACE FUNCTION market.f_build_provider_symbol(
    p_instrument_id  BIGINT,
    p_provider_code  TEXT
)
RETURNS TEXT
LANGUAGE sql
STABLE
AS $$
    SELECT
        COALESCE(
            ips.provider_symbol,
            i.symbol || COALESCE('.' || epc.symbol_suffix, '')
        ) AS provider_symbol_final
    FROM market.instrument i
    LEFT JOIN market.data_provider dp
           ON dp.code = p_provider_code
    LEFT JOIN market.instrument_provider_symbol ips
           ON ips.instrument_id = i.instrument_id
          AND ips.provider_id   = dp.provider_id
    LEFT JOIN market.exchange_provider_code epc
           ON epc.exchange_id   = i.exchange_id
          AND epc.provider_id   = dp.provider_id
    WHERE i.instrument_id = p_instrument_id
    LIMIT 1;
$$;

-- 2) Backward-compatible wrapper for EODHD-only usage.
--    Signature stays the same, but implementation delegates to the generic function.

DROP FUNCTION IF EXISTS market.f_build_eodhd_symbol(BIGINT);

CREATE OR REPLACE FUNCTION market.f_build_eodhd_symbol(
    p_instrument_id BIGINT
)
RETURNS TEXT
LANGUAGE sql
STABLE
AS $$
    SELECT market.f_build_provider_symbol(p_instrument_id, 'EODHD');
$$;

COMMIT;
