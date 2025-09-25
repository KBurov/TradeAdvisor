import os, tempfile, pathlib, mlflow

# Point to MLflow tracking server (Docker service port mapped to host)
mlflow.set_tracking_uri("http://localhost:5001")

# Tell MLflow/boto3 how to reach MinIO (S3-compatible)
os.environ["MLFLOW_S3_ENDPOINT_URL"] = "http://localhost:9000"
os.environ["AWS_ACCESS_KEY_ID"] = os.getenv("MINIO_ROOT_USER", "minio")
os.environ["AWS_SECRET_ACCESS_KEY"] = os.getenv("MINIO_ROOT_PASSWORD", "minio123")

# Helpful defaults for MinIO / localhost
os.environ.setdefault("AWS_DEFAULT_REGION", "us-east-1")
os.environ.setdefault("AWS_REGION", "us-east-1")
os.environ.setdefault("AWS_S3_SIGNATURE_VERSION", "s3v4")
os.environ.setdefault("AWS_S3_ADDRESSING_STYLE", "path")  # avoid virtual-hosted style on localhost

mlflow.set_experiment("smoke-test")
with mlflow.start_run() as run:
    mlflow.log_param("foo", "bar")
    mlflow.log_metric("accuracy", 0.99)
    p = pathlib.Path(tempfile.gettempdir()) / "hello_mlflow.txt"
    p.write_text("hello, mlflow+minio")
    mlflow.log_artifact(str(p))
    print("Run ID:", run.info.run_id)
    print("Artifact URI:", mlflow.get_artifact_uri())
