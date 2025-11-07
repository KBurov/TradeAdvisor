# TradeAdvisor Roadmap (90-Day Plan)

This roadmap outlines major milestones for the first 90 days of the project.

---

## Phase 1 — Foundation (Weeks 1–3)
✅ Completed:
- GitHub repository initialized (README, docs, ADRs).
- Development environment configured on Windows + WSL2.
- Essentials installed: Git, Python, .NET 8, Docker, VS Code.
- Setup documented in [docs/setup-windows-wsl.md](docs/setup-windows-wsl.md).

**Deliverables:**
- Running dev environment with Docker/WSL2
- Baseline docs (`architecture.md`, ADRs)

---

## Phase 2 — Infrastructure Skeleton (Weeks 4–6)
✅ Completed:
- Postgres + Adminer running locally via Docker Compose.
- MinIO + MLflow stack running with persistence (bind mounts, SQLite DB).
- Kafka (KRaft mode) with UI added and verified.
- Automated DB migration script `infra/migrate.sh` implemented.

**Deliverables:**
- `docker-compose.yml`
- Running stack locally
- Initial API skeletons

---

## Phase 3 — Data & Features (Weeks 7–9)
✅ Completed:
- Defined and documented full DB schema (migrations 001–010).
- Added provider-related tables (`market.data_provider`, `exchange_provider_code`, etc.).
- Introduced helper SQL function `f_build_eodhd_symbol()` for dynamic ticker construction.
- Implemented and validated TiingoFetcher with retry logic, null-safety, and deduplication.
- Added `ensure_price_daily_partitions()` function and automated partition creation.
- Created comprehensive unit tests for `Common.Rest.RestClientUtils` and `PriceIngestor.Services.TiingoFetcher`.

In progress:
- Refactor `PriceIngestor` endpoints (retain only `healthz` and `run-today`).
- Extend `InstrumentRepository` to include last known price date for smarter fetcher selection.
- Add automatic long/short fetcher selection (Tiingo → long, future EODHD → short).

**Deliverables:**
- Verified schema (`schema.md` updated to v010)
- Fully operational Tiingo ingestion pipeline
- Partition-aware price storage
- Unit test coverage for core ingestion utilities

---

## Phase 4 — Models & Training (Weeks 10–12)
- Implement ARIMA/GARCH baselines.
- Add LSTM/Transformer models in Python.
- Export trained models to ONNX.
- Register and version models in MLflow.
- Build .NET inference service using ONNX Runtime.

**Deliverables:**
- Training scripts
- Registered models
- ONNX inference service

---

## Phase 5 — Backtesting & Evaluation (Weeks 13–14)
- Build deterministic backtesting engine (.NET).
- Add walk-forward evaluation, purged CV.
- Track metrics in Postgres & MLflow.

**Deliverables:**
- Backtesting service
- Evaluation reports

---

## Phase 6 — API & Web (Weeks 15–16)
- Implement REST endpoints:
  - Forecasts
  - Model management
  - Stock/ETF list management
- Build minimal React dashboard (charts, model leaderboard).

**Deliverables:**
- API layer (REST-only)
- React web UI

---

## Phase 7 — Cloud Migration Prep (Weeks 17–18)
- Harden Docker images, Helm charts.
- Add GitHub Actions CI/CD pipeline.
- Deploy to cloud (AWS or Azure) with managed Postgres/S3.

**Deliverables:**
- Helm manifests
- Cloud deployment prototype

---

## Notes
- Each milestone produces working, incremental deliverables.
- ADRs will be updated if major changes occur.
- Roadmap will be refined continuously.
- **Next:** finalize `PriceIngestor` refactor (long/short fetchers),  
  then start implementing the Feature Computation Service (fundamental and technical metrics) triggered by Kafka messages.
