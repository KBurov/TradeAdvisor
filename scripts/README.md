# Scripts

Helper scripts for development, validation, and experimentation.

## Structure

- **`tests/`** — lightweight smoke tests and validation checks  
  (e.g., verifying that MLflow logs to MinIO).
- **`utils/`** — developer utilities and one-off tasks  
  (e.g., DB reset, data import/export).
- **`experiments/`** — exploratory ML or data scripts not yet promoted into services.

## Dependencies

All scripts share Python requirements listed in:

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