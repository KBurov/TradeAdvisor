# Reference Data

This folder contains static reference datasets used to **seed the database**.  
Unlike runtime volumes (`data/minio`, `data/mlflow`, `data/kafka`), files here **are tracked in Git**.

## Files

- `tickers_core.csv` — initial universe of stocks/ETFs to insert via seed SQL.

## Guidelines

- Keep files small and text-based (CSV, JSON, YAML).  
- Do not place runtime or generated data here.  
- Update seed SQL scripts (`infra/sql/seed/*.sql`) if new reference files are added.
