# Architecture Decision Records (ADRs)

This folder contains all **Architecture Decision Records** for the **TradeAdvisor** project.  
Each ADR documents a key design or technology choice — its **context**, **decision**, and **consequences** — to ensure long-term architectural clarity and traceability.

---

## Index

| ADR # | Title | Summary | Status |
|-------|--------|----------|---------|
| [ADR-0001](ADR-0001-tech-stack.md) | **Tech Stack** | Defines the hybrid architecture: Python for ML, .NET 8 for services, PostgreSQL, Docker, MLflow, MinIO, Kafka/Redis, React. | ✅ Accepted |
| [ADR-0002](ADR-0002-rest-only.md) | **REST-only APIs (no gRPC)** | Enforces REST/JSON for cross-language simplicity; async via Kafka/Redis instead of gRPC streams. | ✅ Accepted |
| [ADR-0003](ADR-0003-postgresql-primary-db.md) | **PostgreSQL as Primary DB** | Core OLTP + analytics store using partitioning and JSONB features. | ✅ Accepted |
| [ADR-0004](ADR-0004-onnx-model-format.md) | **ONNX Model Format** | Standardizes model exchange between Python (training) and .NET (inference). | ✅ Accepted |
| [ADR-0005](ADR-0005-containerization-docker-kubernetes.md) | **Containerization — Docker & Kubernetes** | Ensures consistent dev/prod environments; Docker Compose for local, Helm + K8s for cloud. | ✅ Accepted |
| [ADR-0006](ADR-0006-messaging.md) | **Messaging (Kafka vs Redis Streams)** | Chooses Kafka for production event streaming and Redis Streams for local dev. | ✅ Accepted |
| [ADR-0007](ADR-0007-multi-provider-market-data.md) | **Multi-Provider Market Data** | Adds a data-provider abstraction for EODHD, Tiingo, etc., with SQL-driven provider-suffix logic. | ✅ Accepted |

---

## Format

Each ADR follows this structure:

1. **Context** — background and motivation.  
2. **Decision** — selected solution or direction.  
3. **Status** — e.g., *Proposed*, *Accepted*, or *Deprecated*.  
4. **Consequences** — implications, trade-offs, and follow-ups.  
5. *(Optional)* **Implementation Notes** or **Future Work**.

---

## How to Add a New ADR

1. Create a new file in this folder with the next sequential number, e.g.:

   ```bash
   docs/adr/ADR-0008-short-title.md
   ```

2. Use the same markdown structure as existing ADRs.
3. Commit it with a message like:

   ```bash
   git add docs/adr/ADR-0008-short-title.md
   git commit -m "docs(adr): <short description>"
   ```

*Last updated: **October 2025***