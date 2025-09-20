# Services Overview

This document lists planned services, responsibilities, and primary REST endpoints.
Stack: Python for data science/training; .NET for high-performance paths. REST-only.

---

## API Gateway (Python, FastAPI)
**Purpose:** Single entrypoint for clients (web UI, scripts). Auth, rate limits, request fan-out.

**Endpoints (draft):**
- `GET /v1/health`
- `POST /v1/predict` { symbol, horizon, as_of? } → routes to Inference
- `POST /v1/tickers` { add/remove/reset }
- `GET /v1/models` / `POST /v1/train` / `POST /v1/backtest` (control plane)

**Notes:** JSON default; allow `application/x-msgpack` later on hot paths.

---

## Inference Service (.NET 8, ASP.NET Core + ONNX Runtime)
**Purpose:** Low-latency inference on exported ONNX models (CPU/GPU).

**Endpoints:**
- `GET /v1/health`
- `POST /v1/predict` { symbol, features|window, model_id? } → { yhat, bands, proba }

**Perf:** Kestrel tuned, Server GC, Tiered PGO, System.Text.Json source-gen, SIMD/Span<T>.

---

## Ingest – Prices (.NET)
**Purpose:** Live & historical OHLCV ingest (REST/WebSocket providers) → Postgres + Kafka.

**Endpoints (internal):**
- `POST /v1/backfill` { symbols, start, end, provider }
- `POST /v1/replay` { topic, partition, ts }

**Pipelines:** WebSocket → normalizer → dedup → Postgres (`prices`) → Kafka `prices.raw`.

---

## Feature Worker (.NET)
**Purpose:** Rolling indicators/window stats (SMA/EMA/RSI/MACD, z-scores, vol). Writes to `features`.

**Input:** Kafka `prices.raw` or DB scans.
**Output:** Postgres `features` (JSONB), Kafka `prices.features` (optional).

**Perf:** SIMD (HWIntrinsics), ArrayPool<T>, Pipelines for IO.

---

## NLP & Sentiment (Python)
**Purpose:** News/social ingest, entity & ticker linking, sentiment/topic models (FinBERT/roberta-base).

**Endpoints (internal):**
- `POST /v1/ingest/news`
- `POST /v1/ingest/social`
- `POST /v1/sentiment/batch` [{ text, ts, source }]

**Output:** Postgres `news_social` with sentiment & metadata.

---

## Train Orchestrator (Python)
**Purpose:** Schedule/launch training jobs (ARIMA/GARCH, LGBM/XGBoost, LSTM/Transformer/TFT). Log to MLflow. Export ONNX.

**Endpoints (internal):**
- `POST /v1/train` { model, symbols, window, params }
- `POST /v1/register` { model_name, version, artifact_uri }

**Artifacts:** MinIO/S3; tracking/registry via MLflow.

---

## Backtester (.NET)
**Purpose:** Deterministic, event-driven simulator; strategy & slippage models; metrics.

**Endpoints (internal):**
- `POST /v1/backtest` { model_id, symbols, period, strategy, costs }
- `GET /v1/backtest/{id}` → results (Sharpe, Sortino, MDD, equity curve)

**Output:** Postgres `backtests`, artifacts in MinIO.

---

## Metadata & Registry (shared)
- **Postgres:** tickers, prices, features, models, forecasts, backtests, news_social.
- **MLflow:** experiments, runs, model registry.
- **MinIO/S3:** datasets, ONNX, reports.

---

## Messaging
- **Kafka topics (prod) / Redis Streams (dev):**
  - `prices.raw`, `prices.features`, `models.events`
- **Schemas:** start with JSON Schema in repo; migrate to Avro/Protobuf + registry later.

---

## Observability
- Metrics: Prometheus endpoints per service.
- Logs: structured (Serilog for .NET; std logging for Python).
- Tracing: OpenTelemetry (API Gateway ↔ services).

