FROM python:3.12-slim

# System deps (curl for healthcheck, tini for clean signals)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates tini && \
    rm -rf /var/lib/apt/lists/*

# MLflow with S3 support + boto3
RUN pip install --no-cache-dir \
    "mlflow[s3]==2.16.0" \
    "boto3==1.35.*" \
    "gunicorn==22.*"

# Entrypoint
ENTRYPOINT ["/usr/bin/tini", "--"]

# Default command will be provided by compose
