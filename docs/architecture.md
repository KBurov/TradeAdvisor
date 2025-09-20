# Architecture Overview

TradeAdvisor is a hybrid system combining Python (for data science and training) and .NET (for high-performance services).  
It is designed with **REST-only APIs**, containerization, and future cloud deployment in mind.

---

## Core Components

### 1. Data Layer
- **PostgreSQL** — main database for historical prices, features, social/news data, model metadata.
- **MinIO (S3-compatible)** — object storage for model artifacts and large datasets.
- **MLflow** — experiment tracking and model registry.

### 2. Processing & Services
- **Python services**
  - Data collection (prices, news, social media).
  - Financial analysis modules (technical indicators, chart patterns, fundamentals).
  - Model training (ARIMA/GARCH, LSTM, Transformers, ensembles).
  - Sentiment analysis (NLP, ticker linking).
- **.NET services**
  - Market data ingest gateways (fast streaming from APIs → Kafka/Redis).
  - Feature computation (SIMD-optimized rolling indicators, window statistics).
  - Low-latency inference service (ONNX Runtime).
  - Backtesting engine (deterministic, event-driven).

### 3. Integration & APIs
- **REST API (FastAPI + ASP.NET Core Minimal APIs)**
  - Stock/ETF list management.
  - Forecast requests.
  - Model registry endpoints.
- **Message bus**
  - Kafka (or Redis Streams) for async communication and scheduling.

### 4. User Interface
- **React web dashboard** (later phase)
  - Forecast visualization.
  - Model leaderboard.
  - Control panel for training/backtesting.

---

## Infrastructure

- **Containers**: Docker for development, Kubernetes (Helm charts) for cloud deployment.
- **CI/CD**: GitHub Actions → build, test, lint, scan, deploy.
- **Monitoring**: Prometheus + Grafana; logs via Serilog (C#) / logging (Python).

---

## Design Principles
- Hybrid stack: Python for flexibility, .NET for speed.
- REST-only (no gRPC).
- Modular services (extensible with new models and features).
- Start local (Windows + WSL2), then migrate to cloud (AWS/Azure).
