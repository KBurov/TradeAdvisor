# Setup — Windows 11 + WSL2 (Ubuntu) + RTX

This guide records the exact steps to prepare a Windows laptop (with RTX GPU) for the TradeAdvisor project.

---

## 1) Enable WSL2 (one time, in Windows PowerShell as Admin)

```powershell
wsl --install
wsl --set-default-version 2
```

Install **Ubuntu** from Microsoft Store. Launch it and create your UNIX user.

---

## 2) Update Ubuntu (inside the Ubuntu terminal)

```bash
sudo apt-get update
sudo apt-get upgrade -y
```

---

## 3) Essentials (inside Ubuntu)

```bash
sudo apt-get install -y git build-essential make curl wget unzip jq ca-certificates pkg-config libpq-dev postgresql-client
```

---

## 4) Python (system default, 3.12+) (inside Ubuntu)

```bash
sudo apt-get install -y python3 python3-venv python3-pip
python3 --version
pip3 --version
# Always work inside a venv for project isolation
python3 -m venv .venv
source .venv/bin/activate
```

---

## 5) .NET 8 SDK (LTS) (inside Ubuntu)

```bash
wget https://packages.microsoft.com/config/ubuntu/$(. /etc/os-release; echo $VERSION_ID)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --info
dotnet --list-sdks
```

> We use **.NET 8 (LTS)** for now. We can upgrade to .NET 9 later when the official packages are broadly available for our distro.

---

## 6) Docker Desktop (Windows)
- Install **Docker Desktop** on Windows.
- Open Docker Desktop → Settings → General: ensure **Use the WSL 2 based engine** is enabled (default).
- No need to enable Windows containers.

**Verify from Ubuntu:**

```bash
docker version
```

**Test GPU pass-through (still in Ubuntu):**

```bash
docker run --rm --gpus all nvidia/cuda:12.4.1-base-ubuntu22.04 nvidia-smi
```

You should see `NVIDIA-SMI` with your graphics card.

---

## 7) VS Code (Windows) + WSL Extension
- Install **VS Code** for Windows.
- Install the **Remote – WSL** extension.
- From the project folder in Ubuntu: code .

---

## 8) Git (inside Ubuntu) — identity + SSH (recommended)

```bash
git config --global user.name  "Your Name"
git config --global user.email "your-email@example.com"
git config --global init.defaultBranch main
git config --global core.autocrlf input
git config --global color.ui auto
```

### Generate SSH key and add to GitHub

```bash
ssh-keygen -t ed25519 -C "your-email@example.com"
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/id_ed25519
cat ~/.ssh/id_ed25519.pub
```

Copy the printed key → GitHub → Settings → SSH and GPG keys → New SSH key → paste → save.

**Test:**

```bash
ssh -T git@github.com
```

You should see a success message.

---

## 9) Create repo (CLI) and clone (example)

```bash
mkdir -p ~/Projects && cd ~/Projects
gh config set git_protocol ssh
gh repo create TradeAdvisor --private --clone
cd TradeAdvisor
```

---

## 10) Quality-of-life (optional)
- Docker Desktop → Resources: allocate 8–16 GB RAM if you plan to run many containers.
- Keep the repo under **Linux path** (e.g., `/home/<you>/Projects`) for best performance (avoid `/mnt/c/...`).

---

## 11) MinIO & MLflow Setup (via Docker Compose)

As part of the infrastructure stack, we run **MinIO** (S3-compatible object storage) and **MLflow** (experiment tracking).
MLflow uses a **custom Docker image** (see `infra/mlflow/Dockerfile`) that must be **built** before first start.

### Initial step (once)

- In the MinIO Console (http://localhost:9001), manually create a **bucket named** `mlflow`.
  MLflow will use this bucket to store all run artifacts (models, metrics, logs, etc.).
  Without this bucket, artifact uploads in smoke tests or real runs will fail.

### Build (one-time, or whenever `infra/mlflow/Dockerfile` changes)

From the compose folder:

```bash
cd infra/compose
# ensure .env exists here (copy from repo root if needed)
cp ../../.env.local .env  # skip if you already have .env
docker compose build mlflow
```

### Start / Restart services

```bash
docker compose up -d
```

### Accessing services

- **MinIO Console** → http://localhost:9001
  (login / password: `minio` / `minio123`)
- **MLflow UI** → http://localhost:5001

## 11A) Database (PostgreSQL)

PostgreSQL is included in the Docker Compose stack and managed automatically with Adminer as a web interface.

- **Postgres Adminer** → http://localhost:8085
  (login / password: `trade` / `trade`, DB: `trade`)

To initialize or update the database the database schema, use the migration script:

```bash
cd infra
./migrate.sh
```

This script:
- Reads Postgres connection parameters from `infra/compose/.env` (preferred) or `.env.local`.
- Creates a tracking table `public.schema_version` (if missing).
- Applies each SQL file in order (e.g. `001_*.sql`, `002_*.sql`, …).
- Skips files that are already recorded as applied.

To verify the applied migrations:

```bash
PGPASSWORD=trade psql -h 127.0.0.1 -U trade -d trade -c \
  "SELECT version, filename, applied_at FROM public.schema_version ORDER BY version;"
```

See **docs/schema.md → Applying Database Migrations** for detailed information.

## 12) Persistence for MinIO & MLflow

By default, `docker compose down -v` deletes all volumes (including MinIO buckets and MLflow runs).
To keep data between restarts:

- We bind-mount service data into the repo’s `data/` folder:
  - `data/minio/` → MinIO object storage (all buckets and objects)
  - `data/mlflow/` → MLflow backend DB (`mlflow.db`)
  - `data/kafka/` → Kafka logs and topic data

- These folders are ignored via `.gitignore`, so they never pollute Git history.
- MLflow is configured with:
  ```yaml
  --backend-store-uri sqlite:////mlflow/mlflow.db
  ```
  which ensures its database is stored inside `data/mlflow/`.
- Artifacts are stored in MinIO (bucket `mlflow`), so both runs and artifacts now survive container restarts.

**Important:**
- Use `docker compose down` (without `-v`) to stop services while preserving data.
- Only use `-v` if you intentionally want to wipe everything (development reset).
- The `data/` folder is ignored in `.gitignore`, so it won’t pollute your repo.

You can verify persistence by:
1. Creating an experiment/run (see smoke test above).
2. Restarting with `docker compose down && docker compose up -d`.
3. Confirming that both artifacts (in MinIO) and runs (in MLflow UI) are still available.

---

## 13) MLflow ↔ MinIO Smoke Test

After starting the stack, verify that MLflow can log runs and upload artifacts to MinIO.
This requires the `mlflow` bucket to already exist (create it once in MinIO console).

1. Activate the project’s Python virtual environment:
   ```bash
   cd ~/Projects/TradeAdvisor
   source .venv/bin/activate
   ```
2. Install test requirements (once per venv):
   ```bash
   pip install -r scripts/requirements.txt   # (use this venv requirements file for smoke test)
   ```
3. Run the smoke test script:
   ```bash
   python scripts/tests/mlflow_smoke_test.py
   ```
   This script will:
   - Create an experiment called **smoke-test**
   - Log a parameter and a metric
   - Upload a small artifact (`hello_mlflow.txt`) to MinIO
4. Verify results:
   - **MLflow UI** → http://localhost:5001
     Open the `smoke-test` experiment and check the run contains parameters, metrics, and the artifact.
   - **MinIO Console** → http://localhost:9001 (user: `minio` / pass: `minio123`)
     Open the `mlflow` bucket and confirm the run’s artifacts are stored there.
5. Deactivate the `venv` when done:
   ```bash
   deactivate
   ```

---

## 14) Kafka (local dev)

We run a single-node **Kafka (KRaft mode)** and **Kafka UI** via Docker Compose.

### Start / Restart

From the compose folder:
```bash
cd infra/compose
# ensure .env exists here (copy once from repo root if needed)
cp ../../.env.local .env  # skip if already present
docker compose up -d
```

### Access

- **Kafka UI**: http://localhost:8086
- **Bootstrap servers (host)**: localhost:9092
- **Bootstrap servers (containers)**: kafka:9092
  (We use **dual listeners** so both host and containers can connect.)

### Persistence

- Data is bind-mounted to `data/kafka/` (kept across restarts).
- Use `docker compose down` (without -v) to stop while preserving data.

### Create initial topics

You can create topics in Kafka UI (Topics → Create), or via CLI:

```bash
docker exec -it tradeadvisor-kafka bash -lc '
  /opt/bitnami/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --create --if-not-exists --topic prices.raw --partitions 3 --replication-factor 1
  /opt/bitnami/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --create --if-not-exists --topic prices.features --partitions 3 --replication-factor 1
  /opt/bitnami/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --create --if-not-exists --topic models.events --partitions 1 --replication-factor 1
  /opt/bitnami/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --list
'
```

### Smoke test (produce / consume)

Produce one JSON message to `prices.raw`:

```bash
docker exec -i tradeadvisor-kafka bash -lc \
  'printf "%s\n" "{\"symbol\":\"AAPL\",\"ts\":\"2025-09-25T12:00:00Z\",\"price\":190.12}" \
   | /opt/bitnami/kafka/bin/kafka-console-producer.sh --bootstrap-server kafka:9092 --topic prices.raw --producer-property acks=all'
```

Consume it back (from beginning):

```bash
docker exec -it tradeadvisor-kafka bash -lc \
  '/opt/bitnami/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:9092 --topic prices.raw --from-beginning --max-messages 1'
```

You should see the JSON echoed by the consumer.

### Troubleshooting

- **Kafka UI shows OFFLINE**: check that it points to `kafka:9092` and the broker advertises both host (`localhost:9092`) and container (`kafka:9092`) listeners.
  - **Check listeners inside Kafka container**:
    - **Host listener (EXTERNAL)**:  
      Run on the host (Ubuntu terminal):  
      ```bash
      ss -lntp | grep -E ":9092"
      ```  
      You should see a line like:  
      ```
      LISTEN 0      4096                *:9092             *:*
      ```  
      This confirms the broker is reachable from the host at `localhost:9092`.
    - **Internal listener (INTERNAL)**:  
      The Bitnami image is minimal (no `ss`/`netstat`). Use Kafka CLI to verify the internal listener:  
      ```bash
      docker exec -it tradeadvisor-kafka bash -lc '/opt/bitnami/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --list'
      ```  
      If topics are listed (e.g., `prices.raw`, `prices.features`, `models.events`), the internal `kafka:9092` listener is healthy.
- **Permissions error on startup**: make sure `data/kafka` is owned by the image’s user:
  ```bash
  sudo chown -R 1001:1001 data/kafka
  ```
- **No messages consumed**: confirm you produced to `prices.raw` and consumed with `--from-beginning`.

---

## Environment Files (.env.local vs .env.example)

- Use **.env.local** for your own machine — it contains real credentials and ports.  
- Commit only **.env.example** to GitHub as a template for others.  
- To start containers with your local file:  
  ```bash
  docker compose --env-file ../../.env.local up -d
  ```
- Never commit `.env.local` to GitHub — it’s already ignored via `.gitignore`.
- Similarly, `.venv/` (Python virtual environment) and `data/` (service volumes for MinIO, MLflow, Kafka)
  are ignored to avoid polluting the repo.
- **Exception:** `data/reference/` is version-controlled.  
  It contains seed/reference datasets (e.g., CSVs for instruments, sectors, industries).  
  This folder is committed to Git to ensure migrations and seed scripts have reproducible inputs.

---

## Troubleshooting
- `docker version` shows only Client → Start Docker Desktop on Windows.
- GPU test fails → update NVIDIA driver on Windows; ensure Docker Desktop is latest.
- `ssh -T git@github.com` fails → re-add key to GitHub, or check you used the correct email during `ssh-keygen`.