# ADR-0006: Messaging (Kafka vs Redis Streams)

## Context
We need asynchronous communication for:
- Market data ingestion pipelines
- Model training jobs
- Event-driven backtesting and inference

Requirements:
- High throughput (thousands of events/sec)
- Ordered partitions (time-series consistency)
- Replay/recovery in case of failure
- Integration with Python and .NET clients

## Decision
- Use **Kafka** as the primary event bus for production deployments.
- Allow **Redis Streams** as a fallback for local dev / lightweight testing.

## Status
Accepted.

## Consequences
- Kafka provides durability, ordering, and partition scalability.
- Slightly heavier to operate than Redis Streams, but more robust for real workloads.
- Redis Streams can be used with Docker Compose locally for simplicity.
- Introduces schema management: we will use **Avro or Protobuf** schemas for Kafka topics.

## Implementation Notes
- Topics:
  - `prices.raw` → market data ingest
  - `prices.features` → computed features
  - `models.events` → training / registry updates
- Schema registry:
  - Start lightweight (JSON schemas in repo)
  - Move to Confluent Schema Registry or Redpanda registry later if needed
