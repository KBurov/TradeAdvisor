# ADR-0003: PostgreSQL as Primary Database

## Context
We need a reliable, open-source relational database for:
- time-series OHLCV, features, news/social references
- model metadata, metrics, forecasts, backtests
- strong indexing, partitioning, JSONB for flexible features

## Decision
Use **PostgreSQL** as the primary OLTP/analytics store.
- Time-based partitioning for large tables (e.g., monthly partitions on `ts`).
- JSONB for feature vectors and flexible schemas (with GIN indexes).
- Extensions allowed (e.g., `pg_partman`, `btree_gin`, `uuid-ossp`) if needed.

## Status
Accepted.

## Consequences
- Powerful SQL + JSONB hybrid modeling with good performance and tooling.
- Single-system simplicity for early phases; later we can add OLAP (e.g., DuckDB/ClickHouse) if required.
- Requires disciplined migrations (Alembic/EF migrations) to avoid schema drift.
