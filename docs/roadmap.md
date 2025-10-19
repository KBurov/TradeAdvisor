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
- Defined and documented full DB schema (migrations 001–007).
- Added provider-related tables (`market.data_provider`, `exchange_provider_code`, etc.).
- Introduced helper SQL function `f_build_eodhd_symbol()` for dynamic ticker construction.
- Added EODHD and Tiingo integration design (ADR-0007).

In progress:
- Price Ingestor service migration from Yahoo Finance API.
- Implement logic to use Tiingo for initial fill and EODHD for batch updates.

**Deliverables:**
- Verified schema (`schema.md` updated to v007)
- Multi-provider ready database
- Initial price ingestion logic

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
- **Next:** complete Tiingo + EODHD integration in the Price Ingestor service,  
  then proceed with the feature computation and validation modules.
