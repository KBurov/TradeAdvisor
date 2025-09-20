# ADR-0001: Tech Stack

## Context
We need a hybrid system that combines flexibility in data science workflows and high performance for production services.

- **Python** is the ecosystem of choice for data science, ML, and rapid prototyping.
- **.NET (C#)** is strong in performance, concurrency, and deterministic services.
- We want REST-only APIs (avoid gRPC) for simplicity and compatibility.

## Decision
We will use:
- **Python** for data science, feature engineering, model training, NLP, and classical ML.
- **.NET 8 (LTS)** for ingest gateways, high-performance feature calculations, ONNX-based inference, and backtesting.
- **PostgreSQL** as primary DB.
- **Docker** for containerization, Kubernetes as the orchestration target.
- **MLflow** for experiment tracking and model registry.
- **MinIO** (S3-compatible) for artifact storage.
- **Kafka (or Redis Streams)** for async pipelines.
- **React** for the future dashboard.

## Status
Accepted.

## Consequences
- We can leverage the strengths of both ecosystems.
- Slightly more complexity in infrastructure (need both Python and .NET services).
- We avoid vendor lock-in and keep everything open-source.
