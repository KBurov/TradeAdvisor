# ADR-0005: Containerization with Docker & Kubernetes

## Context
We need consistent, reproducible environments for a hybrid Python + .NET system across
local dev (Windows + WSL2) and future cloud deployment (AWS/Azure). Services include
APIs, workers, databases, and model-serving components.

## Decision
- Use **Docker** for all services in development and CI.
- Target **Kubernetes** for staging/production deployment.
- Package infra with **Helm charts** (one chart per service; shared values for env).
- Keep a **Docker Compose** file for local dev orchestration.

## Status
Accepted.

## Consequences
- Pros:
  - Reproducible builds/environments across dev/CI/prod.
  - Clear service boundaries; easy scaling in K8s.
  - Works well with GitHub Actions, image scanning, and registries (GHCR/ECR/ACR).
- Cons:
  - Added complexity to local setup (containers, images).
  - Kubernetes adds operational overhead; mitigated by starting with Compose locally.

## Implementation Notes
- **Images**: multi-stage builds, pinned base images, minimal layers.
- **Security**: use non-root where possible; enable image scanning (Trivy).
- **Configs/Secrets**: env vars for dev; Kubernetes Secrets + sealed secrets for prod.
- **Observability**: expose Prometheus metrics; centralize logs; OpenTelemetry traces.
- **Networking**: REST over HTTP/2/3; optional MessagePack for hot endpoints.
- **GPU**: enable NVIDIA runtime where needed for training/inference images.
