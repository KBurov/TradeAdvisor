# Scripts

This folder contains helper scripts for development, testing, and validation.

## Current scripts

- **`tests/mlflow_smoke_test.py`**  
  Quick check that MLflow can log parameters, metrics, and artifacts into MinIO.  
  Used after first-time setup or whenever the stack is restarted.

## Dependencies

Scripts may require Python packages listed in:

```bash
scripts/requirements.txt
```

Install them into your virtual environment with:

```bash
pip install -r scripts/requirements.txt
```

## Notes

- Scripts are **not production code** — they are for diagnostics, validation, or one-off tasks.
- Keep each script self-contained and documented.
- If a script grows into a service or pipeline, it should be promoted to the appropriate package (e.g., `services/`, `analysis/`).