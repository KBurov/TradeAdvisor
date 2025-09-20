# ADR-0002: REST-only (no gRPC)

## Context
In distributed systems, gRPC is often used for high-performance RPC between services.  
However, gRPC introduces extra complexity (code generation, tooling differences between Python and .NET, and client support requirements).  
For TradeAdvisor, our goals are:
- Simple integration across languages (Python, .NET, JavaScript).
- Easy debugging and inspection (human-readable JSON over HTTP).
- Broad compatibility with external consumers (browsers, scripts, trading tools).

## Decision
We will:
- Use **REST APIs** over HTTP/2 or HTTP/3 as the sole service interface.
- Default to **JSON** encoding.
- Allow **MessagePack** as an optional content type for performance-critical endpoints.
- For async workloads, rely on **Kafka (or Redis Streams)** instead of gRPC streaming.

## Status
Accepted.

## Consequences
- Easier developer onboarding and debugging.
- No need for gRPC toolchains or protobuf stubs.
- Potentially higher latency vs gRPC, but mitigated by:
  - HTTP/2/3 multiplexing.
  - Binary serialization (MessagePack) for hot paths.
- Clear separation between sync (REST) and async (Kafka/Redis) communication.
